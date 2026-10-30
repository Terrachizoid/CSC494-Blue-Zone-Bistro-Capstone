"""Blue Zone Bistro text-mode playtest engine.

A turn-by-turn version of the game for AI (or human) playtesters. It shows only what the Unity UI
shows (no Traffic Light answers outside sorting feedback, no formulas, no hidden customer stats)
and records every action. Game rules come from bzb_sim (a port of the C# services) plus events,
sorting, tutorials and dialogue loaded from the real CSVs.

Usage:  python play.py <session_file> <command> [args...]
        python play.py <session_file> new <seed> <content_root>
        python play.py <session_file> help
"""
import csv, math, os, pickle, random, sys, json, re
from collections import Counter, defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "BalanceSim"))
from bzb_sim import Content, evaluate

LIGHT_CODE = {"G": "Green", "Y": "Yellow", "R": "Red"}

def rows(root, name):
    with open(os.path.join(root, f"Blue_Zone_Bistro_{name}.csv"), encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))

class Data:
    def __init__(self, root):
        csvroot = os.path.join(root, "Assets/GameData/CSV")
        self.C = Content(csvroot, os.path.join(root, "Assets/GameData/BalanceConfig.asset"))
        self.b = self.C.b
        self.food_rows = {r["id"]: r for r in rows(csvroot, "Foods")}
        self.stores = {r["store_id"]: dict(name=r["display_name"], role=r["role_description"], desc=r["description"], unlock=int(r["unlock_day"]),
                       shelves=[tuple(s.split(":", 1)) for s in r["shelf_definitions"].split("|") if s]) for r in rows(csvroot, "Stores")}
        self.events = {r["event_id"]: r for r in rows(csvroot, "Events")}
        self.effects = defaultdict(list)
        for r in rows(csvroot, "EventEffects"): self.effects[r["event_id"]].append(r)
        self.seqs = {r["sequence_id"]: r for r in rows(csvroot, "DialogueSequences")}
        self.lines = defaultdict(list)
        for r in rows(csvroot, "DialogueLines"): self.lines[r["sequence_id"]].append(r)
        for v in self.lines.values(): v.sort(key=lambda r: int(r["line_order"]))
        self.tut = defaultdict(dict); self.tut_meta = {}
        for r in rows(csvroot, "Tutorials"):
            self.tut[r["tutorial_id"]][r["step_id"]] = r["text"]
            self.tut_meta[r["tutorial_id"]] = (r["trigger_flag"], r["behaviour_id"], r["activation_phase"])
        self.endings = {r["ending_id"]: r for r in rows(csvroot, "Endings")}

class Game:
    def __init__(self, root, seed, limits=None):
        self.root, self.seed = root, seed
        # Human limits: browsing actions per market, previews per guest, memory words, skimming.
        self.limits = dict(browse=99, previews=99, memory_words=400, skim=False, forecast=False)
        self.limits.update(limits or {})
        self.browse_used = 0; self.previews_used = 0; self.memory = ""; self.more_text = []
        self.rng = random.Random(seed)
        D = self.D = Data(root); b = D.b
        self.day, self.money = 1, float(b["startingMoney"])
        self.pop, self.well = float(b["startingPopularity"]), float(b["startingCommunityWellbeing"])
        self.cust = {}; self.unlocked = set(); self.inv = []; self.mastered = set()
        self.flags = set(); self.once = set(); self.last_played = {}
        self.events = []; self.cart = Counter(); self.checked_out = False
        self.phase = "market"; self.tut_done = set(); self.pending_text = []
        self.log = []; self.days = []; self.batch_id = 0
        self.first_meal = None; self.sorting = None; self.queue = []; self.qi = 0
        self.start_day()

    # ------------------------------------------------------------- helpers
    def cs(self, cid):
        if cid not in self.cust:
            d = self.D.C.customers[cid]
            self.cust[cid] = dict(health=d["health"], delight=self.D.b["startingDelight"], finance=self.D.b["startingFinance"],
                                  last_end=d["health"], hist={}, rush=0, crash=False, visits=0)
        return self.cust[cid]
    def food(self, fid): return self.D.C.foods[fid]
    def fname(self, fid): return self.D.food_rows[fid]["display_name"]
    def active_events(self): return [e for e in self.events if e["start"] <= self.day < e["expiry"]]
    def closure(self, store):
        for e in self.active_events():
            for f in e["effects"]:
                if f["effect_type"] == "Availability" and f["target_id"] == store: return f"Closed today: {self.D.events[e['id']]['headline']}"
        if self.D.stores[store]["unlock"] > self.day: return f"Opens on day {self.D.stores[store]['unlock']}"
        return None
    def price(self, l):
        add, mul = 0.0, 1.0
        for e in self.active_events():
            for f in e["effects"]:
                if f["effect_type"] != "Price": continue
                if (f["target_type"] == "Store" and f["target_id"] == l["store"]) or (f["target_type"] == "Food" and f["target_id"] == l["food"]):
                    if f["operation"] == "Add": add += float(f["value"])
                    else: mul *= float(f["value"])
        return round(max(0.0, l["price"] + add) * mul + 1e-9, 2)
    def publicity(self):
        return sum(float(f["value"]) for e in self.active_events() for f in e["effects"] if f["effect_type"] == "AttendancePopularity")
    def listings_open(self):
        return [l for l in self.D.C.listings if l["unlock"] <= self.day and self.closure(l["store"]) is None and not l["flag"]]
    def usable(self): return [x for x in self.inv if x["servings"] > 0 and (x["days"] > 0 or x["days"] == -1)]
    def pantry_counts(self):
        c = Counter()
        for x in self.usable(): c[x["food"]] += x["servings"]
        return c
    def recipes_today(self): return [r for r in self.D.C.recipes.values() if r["unlock"] <= self.day]
    def can_make(self, r, counts):
        need = Counter()
        for s in r["slots"]:
            if not s["opt"]: need[s["cat"]] += s["n"]
        have = Counter()
        for fid, n in counts.items():
            if not r["green_only"] or self.food(fid)["tl"] == "Green": have[self.food(fid)["cat"]] += n
        return all(have[c] >= n for c, n in need.items())
    def say(self, text):
        if self.limits["skim"]:
            parts = re.split(r"(?<=[.!?])\s+", text)
            head = " ".join(parts[:2] if text.startswith("[TUTORIAL]") else parts[:1])
            if head != text: self.more_text.append(text); text = head + " [...]"
        self.pending_text.append(text)
    def tutorial(self, tid, step, **tokens):
        key = (tid, step)
        if key in self.tut_done or step not in self.D.tut.get(tid, {}): return
        self.tut_done.add(key)
        t = self.D.tut[tid][step]
        for k, v in tokens.items(): t = t.replace("{" + k + "}", str(v))
        self.say("[TUTORIAL] " + t)

    # ------------------------------------------------------------- dialogue (port of DialogueService eligibility)
    def seq_ok(self, s, trigger, cid, health=-1, delight=-1, meal=None):
        if s["trigger"] != trigger or not self.D.lines.get(s["sequence_id"]): return False
        if trigger in ("RushAfterService", "CrashArrival") and s["customer_id"] != cid: return False
        rb = s.get("recipe_build_condition") or "Any"
        if rb != "Any" and (meal is None or meal["built"] != (rb == "Built")): return False
        mh = s.get("meal_health_condition") or "Any"
        if mh != "Any":
            if meal is None: return False
            hd = meal["health_delta"]
            if (mh == "Gain" and hd <= 0) or (mh == "Loss" and hd >= 0) or (mh == "Unchanged" and hd != 0): return False
        rr = s["repeat_rule"]
        if rr == "Once" and s["sequence_id"] in self.once: return False
        if rr == "OncePerDay" and self.last_played.get(s["sequence_id"]) == self.day: return False
        if self.day < int(s["min_day"] or 1) or (s["max_day"] not in ("", "-1") and self.day > int(s["max_day"])): return False
        if s["customer_id"] and s["customer_id"] != cid: return False
        def rng(v, lo, hi):
            lo = int(lo) if lo not in ("", None) else -1; hi = int(hi) if hi not in ("", None) else -1
            if lo < 0 and hi < 0: return True
            if v < 0: return False
            return (lo < 0 or v >= lo) and (hi < 0 or v <= hi)
        md = meal["delight_delta"] if meal else -1
        if not (rng(health, s["min_customer_health"], s["max_customer_health"]) and rng(delight, s["min_customer_delight"], s["max_customer_delight"])
                and rng(md, s["min_served_meal_delight"], s["max_served_meal_delight"])): return False
        if s["required_recipe_id"] and (meal is None or meal["recipe"] != s["required_recipe_id"]): return False
        if s["required_recipe_tiers"] and (meal is None or self.D.C.recipes and meal["tier"] not in s["required_recipe_tiers"].split("|")): return False
        req = [f for f in s["required_flags"].split("|") if f]; forb = [f for f in s["forbidden_flags"].split("|") if f]
        return all(f in self.flags for f in req) and not any(f in self.flags for f in forb)
    def find_seq(self, trigger, cid, meal=None):
        c = self.cust.get(cid)
        h = c["health"] if c else -1; d = c["delight"] if c else -1
        el = [s for s in self.D.seqs.values() if self.seq_ok(s, trigger, cid, h, d, meal)]
        mand = sorted([s for s in el if s["mode"] == "Mandatory"], key=lambda s: (-int(s["priority"] or 0), s["sequence_id"]))
        if mand: return mand[0]
        amb = [s for s in el if s["mode"] == "Ambient"]
        if not amb: return None
        w = [max(0.0, float(s["random_weight"] or 0)) for s in amb]
        if sum(w) <= 0: return amb[0]
        t = self.rng.random() * sum(w)
        for s, x in zip(amb, w):
            t -= x
            if t <= 0: return s
        return amb[-1]
    def play_seq(self, s, meal=None):
        if not s: return
        for ln in self.D.lines[s["sequence_id"]]:
            text = ln["text_template"]
            if meal: text = text.replace("{servedRecipe}", self.D.C.recipes[meal["recipe"]]["name"])
            sp = ln["speaker_id"]; sp = self.D.C.customers[sp]["name"] if sp in self.D.C.customers else sp
            self.say(f"{sp}: {text}")
        for f in [x for x in s["clear_flags"].split("|") if x]: self.flags.discard(f)
        for f in [x for x in s["set_flags"].split("|") if x]: self.flags.add(f)
        if s["repeat_rule"] == "Once": self.once.add(s["sequence_id"])
        self.last_played[s["sequence_id"]] = self.day
        # Generic flag-triggered text lessons (e.g. T_REQUESTS after the chef), like TutorialController.
        for tid, (flag, beh, ph) in self.D.tut_meta.items():
            if beh == "TextOnly" and flag in self.flags and tid not in ("T_CRASH", "T_RENT", "T_SORTING"): self.tutorial(tid, "explain")
        if s["trigger"] == "CrashArrival":
            for c in self.cust.values():
                pass
            self.flags.add("tutorial.first_crash_dialogue_completed")
            self.tutorial("T_CRASH", "explain")

    # ------------------------------------------------------------- day flow
    def start_day(self):
        b = self.D.b
        self.summary = dict(spent=0.0, earned=0.0, dishes=[], skipped=[], walkin=0.0, rent=getattr(self, "rent_today", 0.0), refund=0.0, sort=None)
        # morning events (port of EventService)
        if self.day >= b["eventsStartDay"]:
            r = self.rng.random(); n = 0 if r < b["noEventChance"] else (2 if r >= 1 - b["twoEventChance"] else 1)
            active = {e["id"] for e in self.active_events()}
            cands = [e for e in self.D.events.values() if int(e["min_day"]) <= self.day and (e["max_day"] in ("-1", "") or self.day <= int(e["max_day"])) and e["event_id"] not in active]
            chosen = []
            while len(chosen) < n and cands:
                w = [float(e["weight"]) for e in cands]; t = self.rng.random() * sum(w); pick = cands[-1]
                for e, x in zip(cands, w):
                    t -= x
                    if t <= 0: pick = e; break
                cands.remove(pick)
                effs = self.D.effects[pick["event_id"]]
                closes = {f["target_id"] for f in effs if f["effect_type"] == "Availability"}
                stores_now = [s for s in self.D.stores if self.D.stores[s]["unlock"] <= self.day and self.closure(s) is None]
                if closes and len([s for s in stores_now if s not in closes]) == 0: continue
                targeted = {f["target_id"] for e2 in chosen for f in self.D.effects[e2] if f["target_type"] == "Store"} | \
                           {f["target_id"] for e2 in self.active_events() for f in e2["effects"] if f["target_type"] == "Store"}
                mine = {f["target_id"] for f in effs if f["target_type"] == "Store"}
                closed_now = {f["target_id"] for e2 in chosen for f in self.D.effects[e2] if f["effect_type"] == "Availability"} | \
                             {f["target_id"] for e2 in self.active_events() for f in e2["effects"] if f["effect_type"] == "Availability"}
                if closes & targeted or (mine & closed_now): continue
                chosen.append(pick["event_id"])
                self.events.append(dict(id=pick["event_id"], start=self.day, expiry=self.day + int(pick["duration_days"]), effects=effs))
        # attendance + cravings (port of CustomerVisitService)
        morning = max(0.0, min(b["maximumPopularity"], self.pop + self.publicity()))
        cap = max(1, sum(1 for t in b["attendanceThresholds"] if morning >= t))
        elig = []
        for cid, d in sorted(self.D.C.customers.items()):
            if d["type"] == "VisitingChef" or d["unlock"] > self.day: continue
            if cid not in self.unlocked:
                if d["pop_unlock"] and self.pop < d["pop_unlock"]: continue
                self.unlocked.add(cid)
            self.cs(cid); elig.append(cid)
        guests = []
        while len(guests) < cap and elig:
            w = [max(0, min(b["maxDelight"], self.cust[c]["delight"])) for c in elig]; tot = sum(w)
            if tot <= 0 and guests: break
            t = self.rng.random() * (tot if tot > 0 else len(elig)); idx = len(elig) - 1
            for i, c in enumerate(elig):
                t -= w[i] if tot > 0 else 1
                if t < 0: idx = i; break
            guests.append(elig.pop(idx))
        cats = {self.food(l["food"])["cat"] for l in self.listings_open()} | {self.food(f)["cat"] for f in self.pantry_counts()}
        avail = [r for r in self.recipes_today() if all(s["cat"] in cats for s in r["slots"] if not s["opt"])]
        self.cravings = {}
        for g in guests:
            ch = []
            if self.day >= b["cravingsStartDay"]:
                pool = sorted(set(self.D.C.customers[g]["cravings"]))
                want = b["minimumDailyCravings"] + int(self.rng.random() * (b["maximumDailyCravings"] - b["minimumDailyCravings"] + 1))
                while len(ch) < want:
                    opts = [t for t in pool if any(all(x == "Comfort" or x in r["tags"] for x in ch + [t]) for r in avail)]
                    if not opts: break
                    t = opts[int(self.rng.random() * len(opts))]; ch.append(t); pool.remove(t)
            self.cravings[g] = ch
        self.guests, self.cap = guests, cap
        self.cart = Counter(); self.checked_out = False; self.phase = "market"; self.browse_used = 0
        if self.day == 1:
            cat = lambda c: re.sub(r"(?<!^)([A-Z])", lambda m: " " + m.group(1).lower(), c)
            andj = lambda xs: xs[0] if len(xs) == 1 else ", ".join(xs[:-1]) + " and " + xs[-1]
            req = lambda r: andj([f"{s['n']} {cat(s['cat'])}" for s in r["slots"] if not s["opt"]])
            exs = sorted(self.recipes_today(), key=lambda r: r["id"])
            parts = [f"{r['name']} needs {req(r)}" for r in exs]
            examples = parts[0] if len(parts) == 1 else "; ".join(parts[:-1]) + "; or " + parts[-1]
            self.tutorial("T_SHOPPING", "shop", recipe=exs[0]["name"], requirements=req(exs[0]), examples=examples)
        if b.get("rentPerDay", 0) > 0 and self.day == b.get("rentStartDay", 99) - 1:
            self.tutorial("T_RENT", "explain", rent=f"{b.get('rentPerDay', 0):g}")
        if b.get("rentPerDay", 0) > 0 and self.day >= b.get("rentStartDay", 99) and self.money < b["rentPerDay"]:
            self.tutorial("T_SELL", "explain", sellRate=f"{b.get('pantrySellRate', 0):.0%}", rent=f"{b['rentPerDay']:g}")

    def checkout(self):
        if self.checked_out: return "Today's checkout is already complete."
        total = 0.0
        for lid, q in self.cart.items():
            l = next(x for x in self.D.C.listings if x["id"] == lid)
            if l not in self.listings_open(): return f"{self.fname(l['food'])} is not available right now."
            total += self.price(l) * q
        total = round(total, 2)
        if total > max(0.0, self.money) + 1e-9: return f"Cannot afford the full cart (${total:.2f}) with ${self.money:.2f}."
        if self.first_meal is None:
            counts = self.pantry_counts()
            for lid, q in self.cart.items():
                l = next(x for x in self.D.C.listings if x["id"] == lid); counts[l["food"]] += l["servings"] * q
            if not any(self.can_make(r, counts) for r in self.recipes_today()):
                return "[TUTORIAL] Your cart and pantry do not yet cover any recipe on today's menu. Add enough ingredients before checkout."
        self.money -= total; self.summary["spent"] += total
        for x in self.inv: x["fresh"] = False  # only this checkout's batches share its sorting refund
        new = []
        for lid, q in self.cart.items():
            l = next(x for x in self.D.C.listings if x["id"] == lid)
            for _ in range(q):
                self.batch_id += 1
                self.inv.append(dict(id=self.batch_id, food=l["food"], servings=l["servings"], days=self.D.C.shelf_life(self.food(l["food"])["storage"]),
                                     cost=self.price(l) / l["servings"], fresh=True))
            if l["food"] not in self.mastered and l["food"] not in new: new.append(l["food"])
        self.checked_out = True; self.receipt_total = total
        out = f"Checkout complete: paid ${total:.2f}. Money now ${self.money:.2f}."
        if new:
            self.phase = "sorting"; self.sorting = new
            self.tutorial("T_SORTING", "explain", maxDiscount=f"{self.D.b['maximumSortingDiscount']:.0%}")
        else:
            out += " No new foods to sort."; self.begin_service()
        return out

    def submit_sort(self, answers):
        if set(answers) != set(self.sorting): return "Answer every new food exactly once: " + ", ".join(self.sorting)
        correct = [f for f in self.sorting if answers[f] == self.food(f)["tl"]]
        acc = len(correct) / len(self.sorting)
        # Unity FoodSortingService: maximumSortingDiscount of what was paid for each correctly sorted new food.
        rate = self.D.b["maximumSortingDiscount"]
        refund = round(sum(round(sum(x["cost"] * x["servings"] for x in self.inv if x.get("fresh") and x["food"] == f) * rate, 2) for f in correct), 2)
        self.money += refund; self.summary["spent"] -= refund; self.summary["refund"] = refund
        for x in self.inv:
            if x.get("fresh"):
                if x["food"] in correct: x["cost"] *= 1 - rate
                x["fresh"] = False
        self.mastered.update(correct)
        self.summary["sort"] = dict(asked=list(self.sorting), correct=len(correct), wrong={f: answers[f] for f in self.sorting if f not in correct})
        lines = [f"Sorting result: {len(correct)}/{len(self.sorting)} correct. Refund ${refund:.2f}. Money now ${self.money:.2f}."]
        for f in self.sorting:
            ok = f in correct
            lines.append(f"  {self.fname(f)}: you said {answers[f]}, catalogue says {self.food(f)['tl']} {'(correct)' if ok else '(wrong)'}. " +
                         (self.D.food_rows[f].get("sorting_explanation") or f"This game's food catalogue classifies {self.fname(f)} as {self.food(f)['tl']}. Detailed explanation and research are pending."))
        self.sorting = None; self.begin_service()
        return "\n".join(lines)

    def begin_service(self):
        self.phase = "service"; self.queue = []; self.qi = 0
        for s in sorted([s for s in self.D.seqs.values() if self.seq_ok(s, "ServiceEncounter", "")], key=lambda s: (-int(s["priority"] or 0), s["sequence_id"])):
            self.queue.append(("story", s["sequence_id"]))
        for g in self.guests: self.queue.append(("guest", g))
        self.advance(start=True)

    def food_aid(self):
        """Unity FoodAidService: once a day, when no unlocked recipe can be made, gift low-appeal green food for one recipe."""
        b = self.D.b
        if not b.get("foodAidEnabled") or getattr(self, "aid_day", 0) == self.day: return
        counts = self.pantry_counts(); recipes = sorted(self.recipes_today(), key=lambda r: r["id"])
        if any(self.can_make(r, counts) for r in recipes): return
        cand = lambda cat: sorted(f for f, d in self.D.C.foods.items() if d["cat"] == cat and d["tl"] == "Green" and d["appeal"] <= b.get("foodAidMaxAppeal", 4) and f in self.D.C.minprice)
        opts = [r for r in recipes if all(cand(s["cat"]) for s in r["slots"] if not s["opt"])]
        if not opts: return
        rng = random.Random(self.day * 7919 + len(opts)); r = opts[rng.randrange(len(opts))]
        need = Counter()
        for sl in r["slots"]:
            if not sl["opt"]: need[sl["cat"]] += sl["n"]
        names = []
        for cat in sorted(need):
            pool = cand(cat); f = pool[rng.randrange(len(pool))]; self.batch_id += 1
            self.inv.append(dict(id=self.batch_id, food=f, servings=need[cat], days=max(1, int(b.get("foodAidShelfLifeDays", 1))), cost=self.D.C.minprice[f], fresh=False))
            names.append(f"{self.fname(f)} ×{need[cat]}")
        self.aid_day = self.day; self.summary["aid"] = f"A local farmer dropped off {', '.join(names)}: enough for {r['name']}."
        self.tutorial("T_FARMER", "explain"); self.say("[FARMER] " + self.summary["aid"])
    def advance(self, start=False):
        if not start: self.qi += 1
        while self.qi < len(self.queue):
            kind, x = self.queue[self.qi]
            if kind == "story":
                s = self.D.seqs[x]
                if self.seq_ok(s, "ServiceEncounter", ""): self.play_seq(s)
                self.qi += 1; continue
            c = self.cs(x); c["visits"] += 1; self.previews_used = 0
            self.play_seq(self.find_seq("CrashArrival" if c["crash"] else "CustomerArrival", x))
            if c["crash"]: c["crash"] = False
            self.play_seq(self.find_seq("BeforeService", x))
            self.food_aid()
            if self.first_meal is None:
                ex = min(self.recipes_today(), key=lambda r: sum(s["n"] for s in r["slots"] if not s["opt"]))
                self.tutorial("T_COOKING", "choose", recipe=ex["name"]); self.tutorial("T_COOKING", "build", recipe=ex["name"])
            return
        self.close_service()

    def parse_meal(self, args):
        if not args: return None, "Give a recipe id and slot fillings, e.g. R002 0=P017 1=P005 2=P008"
        rid = args[0].upper()
        if rid not in self.D.C.recipes or self.D.C.recipes[rid]["unlock"] > self.day: return None, f"Recipe {rid} is not on today's menu."
        r = self.D.C.recipes[rid]; sel = [[] for _ in r["slots"]]; extras = []
        for a in args[1:]:
            if "=" not in a: return None, f"Bad slot filling '{a}'. Use slot=FOOD or slot=FOOD,FOOD (x=FOOD for extras)."
            k, v = a.split("=", 1); fids = [f.strip().upper() for f in v.split(",") if f.strip()]
            for f in fids:
                if f not in self.D.C.foods: return None, f"Unknown food {f}."
            if k.lower() == "x": extras += fids
            else:
                i = int(k)
                if not 0 <= i < len(r["slots"]): return None, f"Recipe {rid} has slots 0..{len(r['slots'])-1}."
                sel[i] += fids
        need = Counter(f for s in sel for f in s) + Counter(extras); have = self.pantry_counts()
        for f, n in need.items():
            if have[f] < n: return None, f"Not enough usable {self.fname(f)} in the pantry (have {have[f]}, need {n})."
        if not need: return None, "Select ingredients first."
        return (r, sel, extras), None

    def recent_serves(self, rid):
        win = max(1, int(self.D.b.get("recipeFatigueWindowDays", 2)))
        return sum(1 for d, x in getattr(self, "serves", []) if x == rid and d > self.day - win)
    def fatigued(self, rid):
        thr = int(self.D.b.get("recipeFatigueThreshold", 0))
        return thr > 0 and self.recent_serves(rid) >= thr
    def guest_info(self, g):
        c = self.cust[g]; dd = sorted(c.get("dd", set()))
        return f"Health {c['health']} · Delight {c['delight']} · Daily Dozen eaten {len(dd)}/11" + (f": {', '.join(dd)}" if dd else "")
    def meal_eval(self, r, sel, extras, g):
        slots = list(r["slots"]); picks = [list(s) for s in sel]
        if extras: slots = slots + [dict(cat="*extra*", n=len(extras), opt=True)]; picks = picks + [extras]
        rr = dict(r, slots=slots)
        e = evaluate(self.D.C, rr, picks, self.cust[g], self.day, self.cravings.get(g, []), self.pop, self.well, self.fatigued(r["id"]))
        e["fatigued"] = self.fatigued(r["id"])
        warn = []
        for s, p in zip(r["slots"], sel):
            if not p and s["opt"]: continue
            if any(self.food(f)["cat"] != s["cat"] for f in p): warn.append(f"{s['cat']}: contains an ingredient of another category.")
            elif len(p) != s["n"]: warn.append(f"{s['cat']}: needs exactly {s['n']} serving(s); selected {len(p)}.")
        if extras: warn.append("Extra ingredients do not belong to this recipe.")
        c = self.cust[g]
        e["health_delta"] = max(0, min(100, c["health"] + e["health"])) - c["health"]
        e["delight_delta"] = max(0, min(self.D.b["maxDelight"], c["delight"] + e["delight"])) - c["delight"]
        e["warnings"] = warn; e["recipe"] = r["id"]; e["tier"] = ""
        return e

    def dish_text(self, e, g):
        crav = self.cravings.get(g, [])
        res = "No request today" if not crav else f"{e['resolved']}/{len(crav)} of {', '.join(crav)}"
        return ((("Recipe built successfully" if e["built"] else "Recipe mismatch: reduced payment, no tip") + ("\n  " + "\n  ".join(e["warnings"]) if e["warnings"] else "")) +
                f"\nHealth {e['health_delta']:+d} · Delight {e['delight_delta']:+d}" + (" (guest was already fully delighted)" if e["delight_delta"] == 0 and e["delight"] > 0 else "") + f" · Popularity {e['pop']:+.0f}" + (" (guests are used to this recipe)" if e.get("fatigued") else "") + f"\nCravings resolved: {res}\n"
                f"Payment ${e['pay']:.2f} + tip ${e['tip']:.2f}")

    def serve(self, args, commit):
        g = self.queue[self.qi][1]
        parsed, err = self.parse_meal(args)
        if err: return err
        r, sel, extras = parsed
        e = self.meal_eval(r, sel, extras, g)
        if not commit:
            if self.limits.get("forecast"): return "PREVIEW (debug forecast, nothing used yet)\n" + self.dish_text(e, g)
            crav = self.cravings.get(g, [])
            ans = [t for t in crav if t in r["tags"]] if e["built"] else []
            return ("REVIEW (nothing used yet)\n" + f"Plate appeal {e['appeal']:.1f} (Comfort needs {self.D.b.get('comfortMinimumAppeal', 7):g})\n" + ("The plate matches the recipe." if e["built"] else "The plate does not match the recipe. A mismatched dish pays less, earns no tip and disappoints the guest.") +
                    ("\n  " + "\n  ".join(e["warnings"]) if e["warnings"] else "") +
                    ("\nRequests answered: none until the plate matches the recipe." if crav and not e["built"] else
                     (f"\nRequests answered: {len(ans)} of {len(crav)}" + (f" ({', '.join(ans)})" if ans else "") +
                      (f" · not answered: {', '.join(t for t in crav if t not in ans)}" if len(ans) < len(crav) else "")) if crav else "\nThis guest has no request today.") +
                    "\nHow the guest reacts, and what they pay, is revealed after serving.")
        if self.first_meal is None and not e["built"]:
            return "[TUTORIAL] For this first lesson, build the recipe successfully before serving. Review its slot warnings."
        for f in [f for s in sel for f in s] + extras:
            b = sorted([x for x in self.usable() if x["food"] == f], key=lambda x: (x["days"] == -1, x["days"], x["id"]))[0]; b["servings"] -= 1
        self.inv = [x for x in self.inv if x["servings"] > 0]
        c = self.cust[g]; c["health"] += e["health_delta"]; c["delight"] += e["delight_delta"]
        c.setdefault("dd", set()).update(e.get("gcov", []))
        if not hasattr(self, "serves"): self.serves = []
        self.serves.append((self.day, r["id"]))
        self.pop = max(0.0, min(self.D.b["maximumPopularity"], self.pop + e["pop"])); self.well = max(0.0, min(100.0, self.well + e["well"]))
        self.money += e["pay"] + e["tip"]; self.summary["earned"] += e["pay"] + e["tip"]
        if e["red"] > 0: c["rush"] = self.day
        foods = [f for s in sel for f in s] + extras
        self.summary["dishes"].append(dict(guest=g, recipe=r["id"], built=e["built"], foods=foods, lights=[self.food(f)["tl"] for f in foods],
                                           health=e["health_delta"], delight=e["delight_delta"], pop=round(e["pop"], 1), pay=round(e["pay"] + e["tip"], 2),
                                           cravings=self.cravings.get(g, []), resolved=e["resolved"]))
        out = "SERVED\n" + self.dish_text(e, g)
        if self.first_meal is None:
            self.first_meal = e
            dd = sorted({d for f in foods for d in self.food(f)["dd"] if self.food(f)["tl"] == "Green"})
            self.tutorial("T_COOKING", "feedback", ingredients=", ".join(f"{self.fname(f)} ×{n}" for f, n in Counter(foods).items()),
                          healthDelta=e["health_delta"], dailyDozen=", ".join(dd) or "none")
        self.play_seq(self.find_seq("RushAfterService" if e["red"] > 0 else "AfterService", g,
                                    meal=dict(built=e["built"], health_delta=e["health_delta"], delight_delta=e["delight_delta"], recipe=r["id"], tier=r.get("tier", ""))),
                      meal=dict(recipe=r["id"]))
        self.advance()
        return out

    def skip(self):
        if self.first_meal is None: return "[TUTORIAL] Complete your first meal before moving on."
        self.summary["skipped"].append(self.queue[self.qi][1]); self.advance(); return "Guest skipped."

    def close_service(self):
        b = self.D.b
        w = round(max(0.0, self.pop - b.get("walkInPopularityThreshold", 0)) * b.get("walkInIncomePerPopularity", 0), 2)
        self.money += w; self.summary["walkin"] = w; self.summary["earned"] += w
        self.phase = "results"

    def finish_day(self):
        b = self.D.b
        self.days.append(dict(day=self.day, money=round(self.money, 2), pop=round(self.pop), well=round(self.well, 1), guests=list(self.guests),
                              events=[e["id"] for e in self.active_events()], **{k: v for k, v in self.summary.items()}))
        if self.day == b["totalDays"]:
            self.phase = "ended"; self.ending = self.select_ending(); return
        spoiled = sum(x["servings"] for x in self.inv if x["days"] == 1)
        if self.days: self.days[-1]["wasted"] = spoiled  # servings that spoil tonight
        for x in self.inv:
            if x["days"] > 0: x["days"] -= 1
        self.inv = [x for x in self.inv if x["days"] != 0 and x["servings"] > 0]
        for cid, d in self.D.C.customers.items():
            if d["unlock"] <= self.day and d["type"] != "VisitingChef": self.cs(cid)
        for c in self.cust.values():
            c["hist"][self.day] = c["health"] - c["last_end"]; c["last_end"] = c["health"]
            src = c["hist"].get(self.day + 1 - b["financeHealthLagDays"])
            if src is not None: c["finance"] = max(0, min(100, c["finance"] + src))
        self.day += 1
        for c in self.cust.values():
            if c["rush"] and c["rush"] == self.day - 1: c["crash"] = True
        rent = b.get("rentPerDay", 0) if self.day >= b.get("rentStartDay", 99) else 0
        self.rent_today = rent
        if rent: self.money -= rent; self.flags.add("tutorial.rent_started")
        self.start_day()

    def select_ending(self):
        best = None
        def ok(v, lo, hi): return (lo in ("", None) or v >= float(lo)) and (hi in ("", None) or v < float(hi))
        for e in self.D.endings.values():
            if e["fallback"].lower() == "true": continue
            if ok(self.pop, e["min_popularity"], e["max_popularity"]) and ok(self.well, e["min_wellbeing"], e["max_wellbeing"]) and ok(self.money, e.get("min_money"), e.get("max_money")):
                if best is None or int(e["priority"]) > int(best["priority"]): best = e
        best = best or next(e for e in self.D.endings.values() if e["fallback"].lower() == "true")
        for ln in self.D.lines[best["dialogue_id"]]: self.say(f"{ln['speaker_id']}: {ln['text_template']}")
        return best["ending_id"]

    # ------------------------------------------------------------- screens
    def header(self):
        return f"=== BLUE ZONE BISTRO · DAY {self.day} of {self.D.b['totalDays']} · {self.phase.upper()} · Money ${self.money:.2f} · Popularity {self.pop:.0f}" + \
               (f" (+{self.publicity():.0f} publicity)" if self.publicity() else "") + " ==="
    def screen(self):
        out = [self.header()]
        if self.pending_text: out += self.pending_text; out.append("---"); self.pending_text = []
        if self.phase == "market" and self.memory and self.browse_used == 0 and not self.cart:
            out.append(f"What you remember from before: {self.memory}")
        if self.phase == "market":
            out.append(f"Today's guests ({len(self.guests)}): " + ", ".join(self.D.C.customers[g]["name"] + (f" (wants: {', '.join(self.cravings[g])})" if self.cravings[g] else "") for g in self.guests))
            if self.summary.get("rent"): out.append(f"Rent paid this morning: ${self.summary['rent']:.2f}")
            if self.active_events(): out.append("News: " + " | ".join(self.D.events[e["id"]]["headline"] + " — " + self.D.events[e["id"]]["summary"] for e in self.active_events()))
            for sid, s in self.D.stores.items():
                cl = self.closure(sid)
                out.append(f"Store {sid} {s['name']} ({s['role']}): " + (cl if cl else "open. Shelves: " + ", ".join(f"{k} ({v})" for k, v in s["shelves"])))
            out.append(self.cart_text())
            out.append("Pantry: " + self.pantry_text())
            out.append("Commands: shelf <STORE> [SHELF] · flip <LISTING> · add <LISTING> [qty] · remove <LISTING> [qty] · recipes · pantry · sell <FOOD> [n] · checkout · note <text>")
        elif self.phase == "sorting":
            out.append("Sort each NEW food you bought into Green (G), Yellow (Y) or Red (R). Flip info shown:")
            for f in self.sorting:
                r = self.D.food_rows[f]
                out.append(f"  {f} {r['display_name']} — label \"{r['marketing_label']}\" — appeal {float(r['ingredient_appeal']):g}/10 — ingredients: {r['ingredient_list']}")
            out.append("Command: sort " + " ".join(f"{f}=?" for f in self.sorting) + "   (use G, Y or R)")
        elif self.phase == "service":
            g = self.queue[self.qi][1]; d = self.D.C.customers[g]
            out.append(f"Now serving: {d['name']}" + (f" · request: {', '.join(self.cravings[g])}" if self.cravings[g] else ""))
            out.append("  " + self.guest_info(g))
            if "Comfort" in self.cravings[g]: out.append(f"  (Comfort is met by any recipe if the plate's average appeal is at least {self.D.b.get('comfortMinimumAppeal', 7):g})")
            out.append("Menu:")
            for r in self.recipes_today():
                req = ", ".join(f"[{i}] {s['n']}×{s['cat']}" + (" (optional)" if s["opt"] else "") for i, s in enumerate(r["slots"]))
                n = self.recent_serves(r["id"]); win = int(self.D.b.get("recipeFatigueWindowDays", 2))
                hist = "" if not n or not self.D.b.get("recipeFatigueThreshold") else (f" — served {n}× in {win} days" + (": guests are used to it, popularity gains halved" if self.fatigued(r["id"]) else ""))
                out.append(f"  {r['id']} {r['name']} — slots {req} — base price ${r['pay']:.2f} — good for: {', '.join(r['tags'])}{hist}")
            out.append("Pantry: " + self.pantry_text(cats=True))
            out.append("Commands: preview <RECIPE> 0=FOOD 1=FOOD,FOOD ... · serve <same> · skip · note <text>")
        elif self.phase == "results":
            s = self.summary
            out.append(f"DAY {self.day} COMPLETE. Spent ${s['spent']:.2f} · Earned ${s['earned']:.2f} · Served {len(s['dishes'])} · Skipped {len(s['skipped'])}")
            if s.get("aid"): out.append(s["aid"])
            if s.get("sold", 0) > 0: out.append(f"Sold pantry food ${s['sold']:.2f}")
            if s["walkin"] > 0: out.append(f"Walk-in takeaway ${s['walkin']:.2f} (included in earnings)")
            elif self.D.b.get("walkInIncomePerPopularity", 0) > 0: out.append(f"No walk-ins yet: passers-by start coming once popularity passes {self.D.b['walkInPopularityThreshold']:.0f}.")
            b = self.D.b
            if b.get("rentPerDay", 0) > 0 and self.day + 1 >= b.get("rentStartDay", 99) and self.day < b["totalDays"]:
                out.append(f"Notice from the landlord: rent of ${b['rentPerDay']:.2f} is due every morning from tomorrow." if self.day + 1 == b["rentStartDay"] else f"Rent due tomorrow morning ${b['rentPerDay']:.2f}")
            for dsh in s["dishes"]:
                out.append(f"  {self.D.C.customers[dsh['guest']]['name']} · {self.D.C.recipes[dsh['recipe']]['name']} · {'built' if dsh['built'] else 'mismatch'} · Health {dsh['health']:+d} · Delight {dsh['delight']:+d} · Earned ${dsh['pay']:.2f}")
            out.append(f"Money now ${self.money:.2f}. Command: next")
        elif self.phase == "ended":
            e = self.D.endings[self.ending]
            out.append(f"THE END: {e['title']}. Final money ${self.money:.2f}, popularity {self.pop:.0f}.")
        return "\n".join(out)
    def cart_text(self):
        if not self.cart: return "Cart: empty"
        parts = []; tot = 0
        for lid, q in self.cart.items():
            l = next(x for x in self.D.C.listings if x["id"] == lid); p = self.price(l) * q; tot += p
            parts.append(f"{q}× {self.fname(l['food'])} ({lid}) ${p:.2f}")
        return f"Cart (${tot:.2f}): " + "; ".join(parts)
    def pantry_text(self, cats=False):
        c = defaultdict(lambda: [0, 99])
        for x in self.usable():
            c[x["food"]][0] += x["servings"]
            if x["days"] >= 0: c[x["food"]][1] = min(c[x["food"]][1], x["days"])
        if not c: return "empty"
        return "; ".join(f"{f} {self.fname(f)}" + (f" [{self.food(f)['cat']}, appeal {float(self.D.food_rows[f]['ingredient_appeal']):g}]" if cats else "") + f" ×{n}" + (f" (use within {dmin} day{'s' if dmin != 1 else ''})" if dmin != 99 else "")
                         for f, (n, dmin) in sorted(c.items()))
    def shelf_text(self, sid, shelf=None):
        if sid not in self.D.stores: return "Unknown store."
        cl = self.closure(sid)
        if cl: return f"{self.D.stores[sid]['name']}: {cl}"
        out = [f"{self.D.stores[sid]['name']} — {self.D.stores[sid]['desc']}"]
        for k, v in self.D.stores[sid]["shelves"]:
            if shelf and k != shelf.upper(): continue
            items = [l for l in self.listings_open() if l["store"] == sid and l["shelf"] == k]
            out.append(f" {v} ({k}):" + ("" if items else " (nothing here today)"))
            for l in items:
                r = self.D.food_rows[l["food"]]; p = self.price(l)
                out.append(f"   {l['id']} {r['display_name']} — \"{r['marketing_label']}\" — appeal {float(r['ingredient_appeal']):g}/10 — ${p:.2f}" + (f" (was ${l['price']:.2f})" if abs(p - l["price"]) > 1e-9 else "") +
                           f" for {l['servings']} servings")
        return "\n".join(out)
    def flip(self, lid):
        l = next((x for x in self.D.C.listings if x["id"] == lid.upper()), None)
        if not l: return "Unknown listing."
        r = self.D.food_rows[l["food"]]; f = self.food(l["food"]); life = self.D.C.shelf_life(f["storage"])
        return (f"{l['id']} {r['display_name']} (card back)\n  Ingredients ({r['ingredient_count']}): {r['ingredient_list']}\n  Category: {f['cat']} · Storage: {f['storage']}"
                f" ({'keeps' if life < 0 else f'lasts {life} days'})\n  ${self.price(l):.2f} for {l['servings']} servings = ${self.price(l)/l['servings']:.2f} per serving")
    def sell_value(self, food):
        """Unity PantryService: rate × min(lowest shelf price per serving, paid per serving) × servings, per batch."""
        rate = self.D.b.get("pantrySellRate", 0)
        low = min(l["price"] / l["servings"] for l in self.D.C.listings if l["food"] == food)
        return round(sum(rate * min(low, x["cost"]) * x["servings"] for x in self.usable() if x["food"] == food), 2)
    def sell(self, food, n=None):
        """Sell n servings (default all) of a food, soonest-to-spoil batches first, like the game's SELL slot."""
        food = food.upper(); rate = self.D.b.get("pantrySellRate", 0)
        if rate <= 0: return "Selling pantry food is turned off."
        if self.checked_out: return "Pantry food can be sold at the market, before checkout."
        batches = sorted([x for x in self.usable() if x["food"] == food], key=lambda x: (x["days"] == -1, x["days"], x["id"]))
        have = sum(x["servings"] for x in batches)
        if not have: return "You have no usable servings of that food."
        n = have if n is None else n
        if n < 1 or n > have: return f"You can sell 1 to {have} servings of that food."
        low = min(l["price"] / l["servings"] for l in self.D.C.listings if l["food"] == food); v = 0.0; left = n
        for x in batches:
            k = min(left, x["servings"]); v += rate * min(low, x["cost"]) * k; x["servings"] -= k; left -= k
            if not left: break
        self.inv = [x for x in self.inv if x["servings"] > 0]; v = round(v, 2)
        self.money += v; self.summary["sold"] = round(self.summary.get("sold", 0) + v, 2)
        return f"Sold {self.fname(food)} ×{n} for ${v:.2f}. Money ${self.money:.2f}."
    def recipes_text(self):
        return "Menu today:\n" + "\n".join(f"  {r['id']} {r['name']} — " + ", ".join(f"[{i}] {s['n']}×{s['cat']}" + (" (optional)" if s["opt"] else "") for i, s in enumerate(r["slots"])) +
                                          f" — base price ${r['pay']:.2f} — good for: {', '.join(r['tags'])}" for r in self.recipes_today())

    # ------------------------------------------------------------- command dispatch
    def command(self, cmd, args):
        cmd = cmd.lower(); reply = ""
        if cmd == "remember":
            words = " ".join(args).split(); cap = self.limits["memory_words"]
            self.memory = " ".join(words[:cap]); self.log.append(dict(day=self.day, phase=self.phase, memory=self.memory))
            return f"Remembered ({min(len(words), cap)} words{'; the rest is forgotten' if len(words) > cap else ''})."
        if cmd == "recall": return f"What you remember: {self.memory or '(nothing)'}"
        BROWSE = ("shelf", "flip", "recipes", "pantry", "news", "more")
        if cmd in BROWSE and self.phase in ("market",):
            if self.browse_used >= self.limits["browse"]:
                self.log.append(dict(day=self.day, phase=self.phase, cmd=cmd, refused="impatient"))
                return "You're getting impatient and the shop is busy. No more browsing today: decide with what you remember (add/remove/checkout)."
            self.browse_used += 1
        if cmd == "more":
            t = "\n".join(self.more_text) or "(nothing more to read)"; self.more_text = []; return t
        if cmd == "note": self.log.append(dict(day=self.day, phase=self.phase, note=" ".join(args))); return "Noted."
        if cmd in ("screen", "look"): return self.screen()
        if cmd == "pantry":
            t = "Pantry: " + self.pantry_text(cats=True)
            if self.phase == "market" and not self.checked_out and self.D.b.get("pantrySellRate", 0) > 0 and self.usable():
                t += "\nSell values for all servings (command: sell <FOOD> [servings]): " + "; ".join(f"{f} ${self.sell_value(f):.2f}" for f in sorted({x['food'] for x in self.usable()}))
            return t
        if cmd == "recipes": return self.recipes_text()
        if cmd == "news":
            ev = self.active_events()
            return "No active news." if not ev else "\n".join(f"{self.D.events[e['id']]['headline']}: {self.D.events[e['id']]['details']} (until day {e['expiry']-1})" for e in ev)
        self.log.append(dict(day=self.day, phase=self.phase, cmd=" ".join([cmd] + args)))
        if self.phase == "market":
            if cmd == "shelf": return self.shelf_text(args[0].upper(), args[1] if len(args) > 1 else None) if args else "shelf <STORE> [SHELF]"
            if cmd == "flip": return self.flip(args[0]) if args else "flip <LISTING>"
            if cmd in ("add", "remove"):
                if not args: return f"{cmd} <LISTING> [qty]"
                lid = args[0].upper(); q = int(args[1]) if len(args) > 1 else 1
                l = next((x for x in self.listings_open() if x["id"] == lid), None)
                if not l: return "That listing is not available right now."
                if cmd == "add": self.cart[lid] += q
                else:
                    self.cart[lid] -= q
                    if self.cart[lid] <= 0: del self.cart[lid]
                return self.cart_text()
            if cmd == "sell": return self.sell(args[0], int(args[1]) if len(args) > 1 else None) if args else "sell <FOOD> [servings]"
            if cmd == "checkout":
                r = self.checkout()
                return r + "\n\n" + self.screen() if self.checked_out else r
        elif self.phase == "sorting" and cmd == "sort":
            ans = {}
            for a in args:
                if "=" not in a: return "Use FOOD=G/Y/R"
                k, v = a.split("=", 1)
                if v.upper() not in LIGHT_CODE: return "Answers must be G, Y or R."
                ans[k.upper()] = LIGHT_CODE[v.upper()]
            r = self.submit_sort(ans)
            return r + "\n\n" + self.screen() if self.sorting is None else r
        elif self.phase == "service":
            if cmd == "preview":
                if self.previews_used >= self.limits["previews"]:
                    return "The guest is waiting. No more previews: serve something or skip."
                self.previews_used += 1
                return self.serve(args, False)
            if cmd == "serve":
                r = self.serve(args, True)
                return r + "\n\n" + self.screen() if r.startswith("SERVED") else r
            if cmd == "skip":
                r = self.skip(); return r + "\n\n" + self.screen()
        elif self.phase == "results" and cmd == "next":
            self.finish_day(); return self.screen()
        elif self.phase == "ended":
            return self.screen()
        return f"'{cmd}' is not available now.\n" + self.screen()

def main():
    path, cmd, args = sys.argv[1], sys.argv[2], sys.argv[3:]
    if cmd == "new":
        lim = {}
        for a in args[2:]:
            k, v = a.split("="); lim[k] = (v.lower() == "true") if k in ("skim", "forecast") else int(v)
        g = Game(args[1] if len(args) > 1 else ".", int(args[0]), lim)
        out = g.screen(); pickle.dump(g, open(path, "wb")); print(out); return
    if cmd == "help": print(__doc__); return
    g = pickle.load(open(path, "rb"))
    out = g.command(cmd, args)
    pickle.dump(g, open(path, "wb"))
    if cmd == "export" or g.phase == "ended":
        json.dump(dict(seed=g.seed, ending=getattr(g, "ending", None), money=round(g.money, 2), pop=round(g.pop), well=round(g.well, 1),
                       days=g.days, log=g.log, customers={k: {kk: vv for kk, vv in v.items() if kk != "hist"} for k, v in g.cust.items()}),
                  open(path + ".json", "w"), indent=1, default=str)
    print(out)

if __name__ == "__main__":
    main()
