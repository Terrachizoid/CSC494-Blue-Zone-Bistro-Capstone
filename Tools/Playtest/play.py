"""Blue Zone Bistro text-mode playtest engine (the AI agent interface).

A turn-by-turn version of the game for AI (or human) playtesters. It shows only what the Unity UI
shows (no Traffic Light answers outside sorting feedback, no formulas, no hidden customer stats)
and records every action. Game rules come from bzb_sim (a port of the C# services) plus events,
sorting, tutorials, the meal plan and dialogue loaded from the real CSVs.

Updated 2026-11-22 to the current game: shop at several stores and check out more than once (each
purchase is sorted right away, then you are back at the market); open the bistro yourself; leave it
during service to shop and come back; plan dishes in the recipe book (day 1 lesson); find where a
food type is sold; all ten event effect kinds; foods in two categories (tofu); the dialogue ladder
with Guide tips and variants; the flag-driven text lessons.

Usage:  python play.py <session_file> <command> [args...]
        python play.py <session_file> new <seed> <content_root> [browse=N previews=N memory_words=N skim=true forecast=true]
        python play.py <session_file> help
Market:  shelf <STORE> [SHELF] · flip <LISTING> · add <LISTING> [qty] · remove <LISTING> [qty] · checkout ·
         recipes · plan <RECIPE> · unplan <RECIPE> · close · find <FOOD TYPE> · pantry · sell <FOOD> [n] · bistro
Sorting: sort <FOOD>=G|Y|R ...
Service: preview <RECIPE> 0=FOOD 1=FOOD,FOOD ... · serve <same> · skip · city · recipes · pantry
Results: next
Any:     screen · guests · news · info day|popularity|money · note <text> · remember <text> · recall · more
"""
import csv, math, os, pickle, random, sys, json, re
from collections import Counter, defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "BalanceSim"))
from bzb_sim import Content, evaluate

LIGHT_CODE = {"G": "Green", "Y": "Yellow", "R": "Red"}
PHASE_NAME = {"market": "Market", "sorting": "FoodSorting", "service": "Service", "results": "Results", "ended": "Completed"}
GUIDE_SPEAKER = "Blue Zone Guide"
CRAVING_ORDER = ["Sweet", "Savory", "Creamy", "Crunchy", "Spicy", "Fizzy", "Hearty", "Comfort"]  # CravingTag enum order

# What the UI's buttons are called here: appended to lessons that name a click or a drag.
TEXT_HINTS = {
    ("T_SHOPPING", "recipes"): "(Text mode: type `recipes`.)",
    ("T_SHOPPING", "plan"): "(Text mode: `plan <RECIPE>` is +; `unplan <RECIPE>` is −.)",
    ("T_SHOPPING", "close"): "(Text mode: the plan shows on the market screen. Type `close`.)",
    ("T_SHOPPING", "shop"): "(Text mode: `find <food type>` shows where one is sold; `shelf <STORE>` enters a store.)",
    ("T_SHOPPING", "store"): "(Text mode: `shelf <STORE> <SHELF>` opens one aisle.)",
    ("T_SHOPPING", "shelf"): "(Text mode: `add <LISTING> [qty]` is +, `flip <LISTING>` reads the label, then `checkout`.)",
    ("T_BISTRO", "explain"): "(Text mode: `bistro` opens your bistro.)",
    ("T_COOKING", "build"): "(Text mode: `serve <RECIPE> 0=FOOD 1=FOOD,FOOD ...`; `preview` checks the plate first.)",
    ("T_SELL", "explain"): "(Text mode: `pantry` shows sell values; `sell <FOOD> [servings]`.)",
    ("T_SEEN_PANTRY", "explain"): "(Text mode: `sell <FOOD> [servings]` at the market.)",
    ("T_CITY_DURING_SERVICE", "explain"): "(Text mode: `bistro` goes back.)",
    ("T_NEW_RECIPES", "explain"): "(Text mode: `recipes`, then `plan <RECIPE>`.)",
}

def rows(root, name):
    with open(os.path.join(root, f"Blue_Zone_Bistro_{name}.csv"), encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))

def readable(name):
    """IntroMealPlan.Readable: 'StarchyVegetable' -> 'Starchy vegetable'."""
    return re.sub(r"(?<!^)([A-Z])", lambda m: " " + m.group(1).lower(), name)

def join_and(items):
    items = list(items)
    return "" if not items else items[0] if len(items) == 1 else ", ".join(items[:-1]) + " and " + items[-1]

def num(v, default=-1.0):
    return float(v) if v not in ("", None) else default

class Data:
    def __init__(self, root):
        csvroot = os.path.join(root, "Assets/GameData/CSV")
        self.C = Content(csvroot, os.path.join(root, "Assets/GameData/BalanceConfig.asset"))
        self.b = self.C.b
        self.food_rows = {r["id"]: r for r in rows(csvroot, "Foods")}
        self.recipe_rows = {r["recipe_id"]: r for r in rows(csvroot, "Recipes")}
        self.stores = {r["store_id"]: dict(name=r["display_name"], role=r["role_description"], desc=r["description"], unlock=int(r["unlock_day"]),
                       shelves=[tuple(s.split(":", 1)) for s in r["shelf_definitions"].split("|") if s]) for r in rows(csvroot, "Stores")}
        self.events = {r["event_id"]: r for r in rows(csvroot, "Events")}
        self.effects = defaultdict(list)
        for r in rows(csvroot, "EventEffects"): self.effects[r["event_id"]].append(r)
        self.seqs = {r["sequence_id"]: r for r in rows(csvroot, "DialogueSequences")}
        self.lines = defaultdict(list)
        for r in rows(csvroot, "DialogueLines"): self.lines[r["sequence_id"]].append(r)
        for v in self.lines.values(): v.sort(key=lambda r: int(r["line_order"]))
        # Tutorials: one definition per id, steps in file order (TutorialCsvImporter).
        self.tuts = {}
        for r in rows(csvroot, "Tutorials"):
            d = self.tuts.setdefault(r["tutorial_id"], dict(id=r["tutorial_id"], flag=r["trigger_flag"], behaviour=r["behaviour_id"],
                                                           phase=r["activation_phase"], priority=int(r["priority"] or 0), steps={}, order=[],
                                                           done=[f for f in (r.get("completion_flags") or "").split("|") if f]))
            d["steps"][r["step_id"]] = r["text"]; d["order"].append(r["step_id"])
        self.endings = {r["ending_id"]: r for r in rows(csvroot, "Endings")}
        self.store_shelf_name = {(sid, k): v for sid, s in self.stores.items() for k, v in s["shelves"]}

class Game:
    def __init__(self, root, seed, limits=None):
        self.root, self.seed = root, seed
        # Human limits: browsing actions per market visit, previews per guest, memory words, skimming.
        self.limits = dict(browse=99, previews=99, memory_words=400, skim=False, forecast=False)
        self.limits.update(limits or {})
        self.browse_used = 0; self.previews_used = 0; self.memory = ""; self.more_text = []
        self.rng = random.Random(seed)
        D = self.D = Data(root); b = D.b
        self.day, self.money = 1, float(b["startingMoney"])
        self.pop, self.well = float(b["startingPopularity"]), float(b["startingCommunityWellbeing"])
        self.cust = {}; self.unlocked = set(); self.inv = []; self.mastered = set()
        self.flags = set(); self.once = set(); self.last_played = {}
        self.events = []; self.cart = Counter(); self.receipt = None
        self.phase = "market"; self.pending_text = []; self.notice = ""
        self.log = []; self.days = []; self.batch_id = 0; self.serves = []
        self.tut = {}; self.tut_active = None; self.last_meal = None; self.book_open = False
        self.first_meal = None; self.sorting = None; self.queue = []; self.qi = 0
        self.start_day()

    # ------------------------------------------------------------- helpers
    def cs(self, cid):
        if cid not in self.cust:
            d = self.D.C.customers[cid]
            self.cust[cid] = dict(health=d["health"], delight=self.D.b["startingDelight"], finance=self.D.b["startingFinance"],
                                  last_end=d["health"], hist={}, rush=0, crash=False, visits=0, served_day=0, dd=set())
        return self.cust[cid]
    def food(self, fid): return self.D.C.foods[fid]
    def fname(self, fid): return self.D.food_rows[fid]["display_name"]
    def cats(self, fid): return self.food(fid)["cats"]
    def cat_text(self, fid): return " / ".join(readable(c) for c in [self.food(fid)["cat"]] + sorted(self.cats(fid) - {self.food(fid)["cat"]}))
    def listing(self, lid): return next((x for x in self.D.C.listings if x["id"] == lid), None)
    def active_events(self): return [e for e in self.events if e["start"] <= self.day < e["expiry"]]
    def effects(self, kind=None):
        for e in self.active_events():
            for f in e["effects"]:
                if kind is None or f["effect_type"] == kind: yield e, f
    def closure(self, store):
        """Why a whole store is shut today (MarketEffects.ClosureReason, plus the store's opening day)."""
        if self.D.stores[store]["unlock"] > self.day: return f"Opens on day {self.D.stores[store]['unlock']}"
        ends = [e["expiry"] for e, f in self.effects("Availability") if f["target_type"] == "Store" and f["target_id"] == store]
        return f"Closed until day {max(ends)}" if ends else None
    def sold_out(self, l):
        """Food or shelf closures (MarketEffects.UnavailableReason)."""
        ends = [e["expiry"] for e, f in self.effects("Availability") if (f["target_type"] == "Food" and f["target_id"] == l["food"]) or
                (f["target_type"] == "Shelf" and f["target_id"] == l["store"] + "/" + l["shelf"])]
        return f"Sold out until day {max(ends)}" if ends else None
    def matches(self, f, food_id):
        return (f["target_type"] == "Food" and f["target_id"] == food_id) or (f["target_type"] == "Category" and f["target_id"] in self.cats(food_id))
    def price(self, l):
        add, mul = 0.0, 1.0
        for e, f in sorted(self.effects("Price"), key=lambda p: p[0]["id"]):
            if (f["target_type"] == "Store" and f["target_id"] == l["store"]) or self.matches(f, l["food"]):
                if f["operation"] == "Add": add += float(f["value"])
                else: mul *= float(f["value"])
        v = max(0.0, l["price"] + add) * mul
        return math.floor(v * 100 + 0.5 + 1e-9) / 100  # MidpointRounding.AwayFromZero
    def publicity(self): return sum(float(f["value"]) for e, f in self.effects("AttendancePopularity"))
    def craving_weight(self, tag):
        w = 1.0
        for e, f in self.effects("Craving"):
            if f["target_id"].lower() == tag.lower(): w *= float(f["value"])
        return w
    def refund_rate(self):
        m = 1.0
        for e, f in self.effects("SortingRefund"): m *= float(f["value"])
        return min(1.0, self.D.b["maximumSortingDiscount"] * m)
    def shelf_life(self, fid):
        days = self.D.C.shelf_life(self.food(fid)["storage"])
        if days <= 0: return days
        return max(1, days + sum(int(float(f["value"])) for e, f in self.effects("ShelfLife") if self.matches(f, fid)))
    def listing_unlocked(self, l):
        return l["unlock"] <= self.day and self.D.stores[l["store"]]["unlock"] <= self.day and (not l["flag"] or l["flag"] in self.flags)
    def listings_open(self):
        return [l for l in self.D.C.listings if self.listing_unlocked(l) and self.closure(l["store"]) is None and self.sold_out(l) is None]
    def usable(self): return [x for x in self.inv if x["servings"] > 0 and (x["days"] > 0 or x["days"] == -1)]
    def pantry_counts(self):
        c = Counter()
        for x in self.usable(): c[x["food"]] += x["servings"]
        return c
    def recipes_today(self): return sorted([r for r in self.D.C.recipes.values() if r["unlock"] <= self.day], key=lambda r: r["id"])
    def tier(self, rid): return self.D.recipe_rows.get(rid, {}).get("tier", "")
    def need(self, r):
        n = Counter()
        for s in r["slots"]:
            if not s["opt"]: n[s["cat"]] += s["n"]
        return n
    def assign(self, owned, need):
        """RecipeRequirements.Assign (ignoreSurplus): each serving counts once, toward a category that still needs it."""
        have = Counter(); left = Counter(need)
        for fid, n in sorted(owned.items(), key=lambda p: (len(self.cats(p[0])), p[0])):  # single-category foods first
            for c in sorted(self.cats(fid), key=lambda c: -left[c]):
                take = min(n, left[c])
                if take > 0: have[c] += take; left[c] -= take; n -= take
        return have
    def can_make(self, r, counts):
        owned = Counter({f: n for f, n in counts.items() if not r["green_only"] or self.food(f)["tl"] == "Green"})
        need = self.need(r); have = self.assign(owned, need)
        return all(have[c] >= n for c, n in need.items())
    def cost_to_complete(self, r):
        """IntroMealPlan.CostToComplete: cheapest packages (open listings) covering what the pantry lacks."""
        need = self.need(r)
        owned = Counter({f: n for f, n in self.pantry_counts().items() if not r["green_only"] or self.food(f)["tl"] == "Green"})
        have = self.assign(owned, need); cost = 0.0
        for cat, n in need.items():
            missing = max(0, n - have[cat])
            if not missing: continue
            offers = [l for l in self.listings_open() if cat in self.cats(l["food"]) and (not r["green_only"] or self.food(l["food"])["tl"] == "Green")]
            best = [math.inf] * (missing + 1); best[0] = 0
            for k in range(missing):
                for o in offers:
                    j = min(missing, k + o["servings"]); best[j] = min(best[j], best[k] + self.price(o))
            cost += best[missing]
        return cost
    def affordable(self, rid):
        r = self.D.C.recipes.get(rid)
        return r is not None and r["unlock"] <= self.day and self.cost_to_complete(r) <= self.money
    def say(self, text):
        if self.limits["skim"]:
            parts = re.split(r"(?<=[.!?])\s+", text)
            head = " ".join(parts[:2] if text.startswith("[GUIDE]") else parts[:1])
            if head != text: self.more_text.append(text); text = head + " [...]"
        self.pending_text.append(text)

    # ------------------------------------------------------------- meal plan (MealPlanService)
    def plan_report(self):
        served = Counter(d["recipe"] for d in self.summary["dishes"])
        req = Counter(); opt = Counter(); dishes = []
        for rid, n in sorted(self.plans.items()):
            r = self.D.C.recipes[rid]; rem = max(0, n - min(served[rid], n)); dishes.append((r, n, rem))
            if not rem: continue
            for s in r["slots"]:
                (opt if s["opt"] else req)[s["cat"]] += s["n"] * rem
        owned = self.pantry_counts()
        for lid, q in self.cart.items():
            l = self.listing(lid); owned[l["food"]] += l["servings"] * q
        have = self.assign(owned, req)
        needs = [(c, req[c], opt[c], have[c]) for c in sorted(set(req) | set(opt))]
        return dishes, needs
    def plan_missing(self):
        _, needs = self.plan_report()
        miss = [f"{r - h} {readable(c)}" for c, r, o, h in needs if r > h]
        return ", ".join(miss) if miss else None
    def plan_text(self):
        if not self.plans: return "Today's plan: nothing planned (open `recipes`, then `plan <RECIPE>`)."
        dishes, needs = self.plan_report()
        out = "Today's plan: " + ", ".join(f"{r['name']} ×{n}" + (f" ({n - rem} done)" if rem < n else "") for r, n, rem in dishes)
        if needs:
            out += "\n  Needs: " + "; ".join((f"{readable(c)} {min(h, r)}/{r}" + (" ✓" if h >= r else f" (missing {r - h})") + (f", +{o} optional" if o else "")) if r else
                                          f"{readable(c)} optional ×{o}" for c, r, o, h in needs)
        elif all(rem == 0 for _, _, rem in dishes): out += " — all planned dishes served."
        return out

    # ------------------------------------------------------------- tutorials (TutorialController + behaviours)
    def lesson_tokens(self, text):
        b = self.D.b; m = self.last_meal or {}
        cands = self.recipes_today()
        ex = min(cands, key=lambda r: (self.cost_to_complete(r), r["id"])) if cands else None
        reqs = lambda r: join_and([f"{s['n']} {readable(s['cat'])}" for s in r["slots"] if not s["opt"]])
        rep = {"recipe": ex["name"] if ex else "recipe", "requirements": reqs(ex) if ex else "",
               "examples": "; ".join(f"{r['name']} needs {reqs(r)}" for r in cands),
               "comfort": f"{b.get('comfortMinimumAppeal', 7):g}", "missedCraving": f"{b.get('popularityPerMissedCraving', 0):g}",
               "maxDiscount": f"{b['maximumSortingDiscount']:.0%}", "rent": f"{b.get('rentPerDay', 0):g}", "rentStart": str(b.get("rentStartDay", 0)),
               "totalDays": str(b["totalDays"]), "cravingsStart": str(b["cravingsStartDay"]), "maxGuests": str(max(1, len(b["attendanceThresholds"]))),
               "guestThresholds": join_and([f"{t:.0f}" for t in b["attendanceThresholds"] if t > 0]),
               "walkInThreshold": f"{b.get('walkInPopularityThreshold', 0):.0f}", "walkInPer100": f"{b.get('walkInIncomePerPopularity', 0) * 100:.2f}",
               "sellRate": f"{b.get('pantrySellRate', 0):.0%}", "healthDelta": str(m.get("health_delta", 0)),
               "dailyDozen": join_and(readable(x) for x in m.get("dd_all", [])) or "none",
               "ingredients": ", ".join(f"{self.fname(f)} ×{n}" for f, n in Counter(m.get("foods", [])).items())}
        for k, v in rep.items(): text = text.replace("{" + k + "}", v)
        return text
    def hard_active(self): return self.tut_active is not None and self.D.tuts[self.tut_active]["behaviour"] != "TextOnly"
    def set_flag(self, flag):
        if not flag or flag in self.flags: return
        # One lesson at a time: during a hard lesson a flag that would start a new text lesson is dropped.
        if self.hard_active() and any(d["behaviour"] == "TextOnly" and d["flag"] == flag and d["id"] not in self.tut for d in self.D.tuts.values()):
            return
        self.flags.add(flag); self.observe()
    def observe(self):
        for d in self.D.tuts.values():
            if d["flag"] in self.flags and d["id"] not in self.tut: self.tut[d["id"]] = dict(status="pending", step=None, seen=None)
    def feasible(self, tid, step):
        beh = self.D.tuts[tid]["behaviour"]
        if beh == "Shopping": return step in ("shop", "store", "shelf") or any(self.cost_to_complete(r) <= self.money for r in self.recipes_today())
        if beh == "FirstMeal": return step == "feedback" or any(self.can_make(r, self.pantry_counts()) for r in self.recipes_today())
        return True
    def can_activate(self, tid):
        beh = self.D.tuts[tid]["behaviour"]
        if beh == "FirstMeal": return self.phase == "service" and self.current_guest() is not None
        return True
    def show_step(self, tid):
        rec = self.tut[tid]; text = self.D.tuts[tid]["steps"].get(rec["step"], "")
        if rec["seen"] == rec["step"] or not text: return
        rec["seen"] = rec["step"]
        hint = TEXT_HINTS.get((tid, rec["step"]))
        self.say("[GUIDE] " + self.lesson_tokens(text) + (" " + hint if hint else ""))
        # Text mode has no separate "pick a recipe card" click: serving names the recipe, so "choose" runs into "build".
        if self.D.tuts[tid]["behaviour"] == "FirstMeal" and rec["step"] == "choose" and "build" in self.D.tuts[tid]["steps"]:
            rec["step"] = "build"; self.show_step(tid)
    def finish_lesson(self, tid, status="completed"):
        self.tut[tid]["status"] = status
        if self.tut_active == tid: self.tut_active = None
        if status == "completed":
            for f in self.D.tuts[tid]["done"]: self.set_flag(f)
    def sync(self):
        """TutorialController.Sync: state flags, then the top pending lesson for this screen. Text lessons are read
        and continued at once (each is one Guide dialogue); a hard lesson shows its current step and waits."""
        b = self.D.b; ph = PHASE_NAME.get(self.phase, "")
        if self.phase == "ended": return
        if self.day == 1 and self.phase == "market":
            self.set_flag("tutorial.bistro_available" if "tutorial.first_purchase" in self.flags else "tutorial.shopping_available")
        if self.phase == "sorting": self.set_flag("tutorial.sorting_available")
        if b.get("rentPerDay", 0) > 0 and self.day == b.get("rentStartDay", 99) - 1: self.set_flag("tutorial.rent_upcoming")
        if self.money < 0: self.set_flag("tutorial.money_negative")
        if self.phase == "market":
            if any(f["target_type"] == "Store" for e, f in self.effects("Availability")): self.set_flag("tutorial.store_closed_event")
            if self.day > 1 and any(r["unlock"] == self.day for r in self.recipes_today()): self.set_flag("tutorial.new_recipes")
            if any(x["days"] == 1 for x in self.usable()): self.set_flag("tutorial.food_expiring")
            checked = self.receipt is not None
            if b.get("rentPerDay", 0) > 0 and self.day >= b.get("rentStartDay", 99) and self.money < b["rentPerDay"] and not checked and not self.bistro_opened:
                self.set_flag("tutorial.money_low")
        g = self.current_guest()
        if self.phase == "service" and g:
            if self.day >= b["cravingsStartDay"] and "Comfort" in self.cravings.get(g, []): self.set_flag("tutorial.first_comfort_request")
            if "tutorial.first_served" not in self.flags: self.set_flag("tutorial.cooking_available")
        self.observe()
        for _ in range(40):
            if self.tut_active:
                rec = self.tut[self.tut_active]
                if not self.feasible(self.tut_active, rec["step"]): self.finish_lesson(self.tut_active, "skipped"); continue
                if self.D.tuts[self.tut_active]["behaviour"] == "TextOnly" or rec["step"] == "feedback":
                    # A text step is read and continued at once (FirstMeal's "feedback" step ends that lesson).
                    self.show_step(self.tut_active); self.finish_lesson(self.tut_active)
                    continue
                self.show_step(self.tut_active); return
            pick = None
            for d in sorted(self.D.tuts.values(), key=lambda d: (-d["priority"], d["id"])):
                rec = self.tut.get(d["id"])
                if not rec or rec["status"] in ("completed", "skipped"): continue
                if d["phase"] != "Any" and d["phase"] != ph: continue
                if not self.can_activate(d["id"]): continue
                pick = d; break
            if not pick: return
            rec = self.tut[pick["id"]]; rec["status"] = "active"; rec["step"] = rec["step"] or pick["order"][0]; self.tut_active = pick["id"]
            if not self.feasible(pick["id"], rec["step"]): self.finish_lesson(pick["id"], "skipped")
    def allow(self, action, recipe=None, built=None):
        """The active hard lesson's Check(): None = allowed, else the Guide's reason."""
        if not self.hard_active(): return None
        tid = self.tut_active; step = self.tut[tid]["step"]; beh = self.D.tuts[tid]["behaviour"]
        if beh == "Shopping":
            if action == "BeginService": return "Finish your first shopping trip first: buy everything your planned dish needs."
            if step == "recipes": return None if action == "OpenRecipes" else "Open your recipe book first (Recipes, in the top bar)."
            if step in ("plan", "close"):
                planned = sum(self.plans.values())
                if action == "PlanDish":
                    return "One dish is enough for your first day. Press − on it if you'd like to swap." if planned > 0 else \
                        (None if self.affordable(recipe) else "That dish costs more than you have today. Pick one that isn't greyed out.")
                if action in ("UnplanDish", "OpenRecipes"): return None
                if action == "CloseRecipes": return None if planned > 0 else "Plan one dish first: press + on its card. Then close the book."
                return "Close the recipe book to go shopping." if planned > 0 else "Plan one dish first: press + on its card."
            if action in ("PlanDish", "UnplanDish"): return "Your plan is set for this first trip. You can change it after your first purchase."
            if action == "Checkout":
                miss = self.plan_missing()
                if miss: return f"For your first checkout, let's buy everything your dish needs. Still missing: {miss}. Tip: click a food type in the plan panel to see where to find it."
            return None
        if beh == "FirstMeal":
            if step == "feedback": return "Finish reading the guide first."
            if action in ("SkipCustomer", "ContinueDish", "FinishDay", "LeaveBistro"): return "Complete your first meal before moving on." if action != "LeaveBistro" else None
            if action == "Serve" and not built: return "For your first dish, fill every slot exactly. Slots with a red outline still need something."
        return None
    def succeeded(self, action):
        if not self.hard_active(): return
        tid = self.tut_active; rec = self.tut[tid]; step = rec["step"]; beh = self.D.tuts[tid]["behaviour"]; nxt = step
        if beh == "Shopping":
            if step == "recipes" and action == "OpenRecipes": nxt = "plan"
            elif step == "plan" and action == "PlanDish": nxt = "close"
            elif step == "close" and action == "UnplanDish" and not self.plans: nxt = "plan"
            elif step == "close" and action == "CloseRecipes": nxt = "shop"
            elif step == "shop" and action == "EnterStore": nxt = "store"
            elif step == "store" and action == "OpenShelf": nxt = "shelf"
            elif step in ("shop", "store", "shelf") and action == "Checkout": nxt = None
        elif beh == "FirstMeal":
            if action == "Serve": nxt = "feedback"
        if nxt is None: self.finish_lesson(tid)
        else: rec["step"] = nxt
    def note(self, flag): self.set_flag(flag)

    # ------------------------------------------------------------- dialogue (DialogueService + EncounterScheduler)
    def context(self, cid, meal=None):
        c = self.cust.get(cid); d = self.D.C.customers.get(cid)
        ctx = dict(day=self.day, cid=cid, name=d["name"] if d else "", health=c["health"] if c else (d["health"] if d else -1),
                   delight=c["delight"] if c else -1, served=meal is not None)
        if meal:
            asked = meal["asked"]; met = meal["met"]; missed = [t for t in asked if t not in met]
            ctx.update(recipe=meal["recipe"], built=meal["built"], health_delta=meal["health_delta"], meal_delight=meal["delight_delta"],
                       req_count=len(asked), req_met=len(met), met=join_and(met), missed=join_and(missed), missed_tags=missed,
                       fatigued=meal["fatigued"], recent=meal["recent"], appeal=meal["appeal"],
                       delight_full=meal["built"] and meal["delight_delta"] == 0 and meal["dish_delight"] > 0, red=meal["red"] > 0)
        return ctx
    def seq_ok(self, s, trigger, ctx):
        if s["trigger"] != trigger or not any(ln["text_template"].strip() for ln in self.D.lines.get(s["sequence_id"], [])): return False
        if trigger in ("RushAfterService", "CrashArrival") and s["customer_id"] != ctx["cid"]: return False
        served = ctx["served"]
        rb = s.get("recipe_build_condition") or "Any"
        if rb != "Any" and (not served or ctx["built"] != (rb == "Built")): return False
        mh = s.get("meal_health_condition") or "Any"
        if mh != "Any":
            if not served: return False
            hd = ctx["health_delta"]
            if (mh == "Gain" and hd <= 0) or (mh == "Loss" and hd >= 0) or (mh == "Unchanged" and hd != 0): return False
        rc = s.get("request_condition") or "Any"
        if rc != "Any":
            if not served: return False
            a, m = ctx["req_count"], ctx["req_met"]
            ok = {"NoRequest": a == 0, "All": a > 0 and m >= a, "Some": a > 0 and 0 < m < a, "None": a > 0 and m == 0, "Missed": a > 0 and m < a}.get(rc, True)
            if not ok: return False
        missed_req = [t for t in (s.get("missed_request") or "").split("|") if t]
        sw = lambda k: (s.get(k) or "Any")
        lo, hi = num(s.get("min_ingredient_appeal")), num(s.get("max_ingredient_appeal"))
        if missed_req or sw("recipe_fatigued") != "Any" or lo >= 0 or hi >= 0 or sw("delight_full") != "Any" or sw("red_served") != "Any":
            if not served: return False
            if missed_req and not any(t in ctx["missed_tags"] for t in missed_req): return False
            for k, v in (("recipe_fatigued", ctx["fatigued"]), ("delight_full", ctx["delight_full"]), ("red_served", ctx["red"])):
                if sw(k) != "Any" and v != (sw(k) == "Yes"): return False
            ap = ctx["appeal"]
            if lo >= 0 or hi >= 0:
                if ap < 0 or (lo >= 0 and ap < lo) or (hi >= 0 and ap > hi): return False
        rr = s["repeat_rule"]
        if rr == "Once" and s["sequence_id"] in self.once: return False
        if rr == "OncePerDay" and self.last_played.get(s["sequence_id"]) == self.day: return False
        if self.day < int(s["min_day"] or 1) or (s["max_day"] not in ("", "-1") and self.day > int(s["max_day"])): return False
        if s["customer_id"] and s["customer_id"] != ctx["cid"]: return False
        def rng(v, lo, hi):
            lo = int(lo) if lo not in ("", None) else -1; hi = int(hi) if hi not in ("", None) else -1
            if lo < 0 and hi < 0: return True
            if v < 0: return False
            return (lo < 0 or v >= lo) and (hi < 0 or v <= hi)
        md = ctx.get("meal_delight", -1) if served else -1
        if not (rng(ctx["health"], s["min_customer_health"], s["max_customer_health"]) and rng(ctx["delight"], s["min_customer_delight"], s["max_customer_delight"])
                and rng(md, s["min_served_meal_delight"], s["max_served_meal_delight"])): return False
        if s["required_recipe_id"] and (not served or ctx["recipe"] != s["required_recipe_id"]): return False
        if s["required_recipe_tiers"] and (not served or self.tier(ctx["recipe"]) not in s["required_recipe_tiers"].split("|")): return False
        req = [f for f in s["required_flags"].split("|") if f]; forb = [f for f in s["forbidden_flags"].split("|") if f]
        return all(f in self.flags for f in req) and not any(f in self.flags for f in forb)
    def weighted(self, items):
        if not items: return None
        w = [max(0.0, num(s["random_weight"], 0)) for s in items]
        if sum(w) <= 0: return items[0]
        t = self.rng.random() * sum(w)
        for s, x in zip(items, w):
            t -= x
            if t <= 0: return s
        return items[-1]
    def find_seq(self, trigger, ctx):
        el = [s for s in self.D.seqs.values() if self.seq_ok(s, trigger, ctx)]
        mand = sorted([s for s in el if s["mode"] == "Mandatory"], key=lambda s: (-int(s["priority"] or 0), s["sequence_id"].lower()))
        if mand:
            group = lambda s: (s.get("variant_of") or s["sequence_id"]).lower()
            tied = [s for s in mand if int(s["priority"] or 0) == int(mand[0]["priority"] or 0) and group(s) == group(mand[0])]
            if len(tied) == 1: return tied[0]
            latest = max(self.last_played.get(s["sequence_id"], -1) for s in tied)
            if latest >= 0:
                fresh = [s for s in tied if self.last_played.get(s["sequence_id"], -1) < latest]
                if fresh: tied = fresh
            return self.weighted(tied)
        return self.weighted([s for s in el if s["mode"] == "Ambient"])
    def resolve(self, text, ctx):
        tok = dict(currentDay=str(ctx["day"]), customerName=ctx["name"], customerHealth=str(ctx["health"]), customerDelight=str(ctx["delight"]))
        if ctx["served"]:
            tok.update(servedRecipe=self.D.C.recipes[ctx["recipe"]]["name"], metRequest=ctx["met"], missedRequest=ctx["missed"],
                       requestsMet=str(ctx["req_met"]), requestCount=str(ctx["req_count"]), ingredientAppeal=f"{ctx['appeal']:.1f}",
                       recipeRecentServes=str(ctx["recent"]), servedMealDelight=str(ctx["meal_delight"]))
        else: tok.update(servedRecipe="", metRequest="", missedRequest="", requestsMet="0", requestCount="0", ingredientAppeal="", recipeRecentServes="0", servedMealDelight="-1")
        tok["appealTarget"] = f"{self.D.b.get('comfortMinimumAppeal', 7):g}"
        low = {k.lower(): v for k, v in tok.items()}
        return re.sub(r"\{(\w+)\}", lambda m: low.get(m.group(1).lower(), m.group(0)), text)
    def play_seq(self, s, ctx):
        if not s: return False
        for ln in self.D.lines[s["sequence_id"]]:
            if not ln["text_template"].strip(): continue
            sp = ln["speaker_id"]; sp = self.D.C.customers[sp]["name"] if sp in self.D.C.customers else ("Guide" if sp == GUIDE_SPEAKER else sp)
            self.say(f"{sp}: {self.resolve(ln['text_template'], ctx)}")
        for f in [x for x in s["clear_flags"].split("|") if x]: self.flags.discard(f)
        for f in [x for x in s["set_flags"].split("|") if x]: self.set_flag(f)
        if s["repeat_rule"] == "Once": self.once.add(s["sequence_id"])
        self.last_played[s["sequence_id"]] = self.day
        if s["trigger"] == "CrashArrival" and ctx["cid"] in self.cust:  # RushCrashProgress.CompleteCrash
            self.cust[ctx["cid"]]["crash"] = False; self.set_flag("tutorial.first_crash_dialogue_completed")
        return True

    # ------------------------------------------------------------- day flow
    def prepare_events(self):
        """EventService.PrepareMorning."""
        b = self.D.b
        if self.day < max(2, b["eventsStartDay"]): return
        roll = self.rng.random(); count = 0 if roll < b["noEventChance"] else 2 if roll < b["noEventChance"] + b["twoEventChance"] else 1
        active = {e["id"] for e in self.active_events()}
        easy = bool(b.get("easyMode", 0))
        cands = [e for e in self.D.events.values() if int(e["min_day"]) <= self.day and (e["max_day"] in ("-1", "") or self.day <= int(e["max_day"]))
                 and int(e["duration_days"]) > 0 and float(e["weight"]) > 0 and self.D.effects[e["event_id"]] and e["event_id"] not in active
                 and (not easy or (e.get("easy_mode") or "true").lower() == "true")]
        added = []
        while len(added) < count:
            choices = [e for e in cands if self.event_allowed(e["event_id"], added)]
            if not choices: break
            t = self.rng.random() * sum(float(e["weight"]) for e in choices); pick = choices[-1]
            for e in choices:
                t -= float(e["weight"])
                if t < 0: pick = e; break
            rec = dict(id=pick["event_id"], start=self.day, expiry=self.day + int(pick["duration_days"]), effects=self.D.effects[pick["event_id"]])
            added.append(rec); cands.remove(pick)
        self.events += added
    def event_allowed(self, eid, added):
        existing = [f for e in self.active_events() + added for f in e["effects"]]
        mine = self.D.effects[eid]; allf = existing + mine
        avail = [l for l in self.D.C.listings if self.listing_unlocked(l)]
        for f in mine:
            t, i = f["target_type"], f["target_id"]
            if t == "Store" and not any(l["store"] == i for l in avail): return False
            if t == "Food" and not any(l["food"] == i for l in avail): return False
            if t == "Category" and not any(i in self.cats(l["food"]) for l in avail): return False
            if t == "Shelf" and not any(l["store"] + "/" + l["shelf"] == i for l in avail): return False
        closed = {f["target_id"] for f in allf if f["effect_type"] == "Availability" and f["target_type"] == "Store"}
        if closed and not any(l["store"] not in closed for l in avail): return False
        if any(f["effect_type"] == "Price" and f["target_type"] == "Store" and f["target_id"] in closed for f in allf): return False
        return True
    def choose_cravings(self, g, avail):
        b = self.D.b; pool = sorted(set(self.D.C.customers[g]["cravings"]), key=lambda t: CRAVING_ORDER.index(t) if t in CRAVING_ORDER else 99)
        want = b["minimumDailyCravings"] + int(self.rng.random() * (b["maximumDailyCravings"] - b["minimumDailyCravings"] + 1)); ch = []
        while len(ch) < want:
            opts = [t for t in pool if any(all(x == "Comfort" or x in r["tags"] for x in ch + [t]) for r in avail)]
            if not opts: break
            w = [max(0.0, self.craving_weight(t)) for t in opts]; tot = sum(w)
            ticket = self.rng.random() * (tot if tot > 0 else len(opts)); tag = opts[-1]
            for t, x in zip(opts, w):
                ticket -= x if tot > 0 else 1
                if ticket < 0: tag = t; break
            ch.append(tag); pool.remove(tag)
        return ch
    def start_day(self):
        b = self.D.b
        self.summary = dict(spent=0.0, earned=0.0, dishes=[], skipped=[], walkin=0.0, rent=getattr(self, "rent_today", 0.0), refund=0.0, sort=None)
        self.prepare_events()
        # attendance + cravings (CustomerVisitService)
        morning = max(0.0, min(b["maximumPopularity"], self.pop + self.publicity()))
        cap = max(1, sum(1 for t in b["attendanceThresholds"] if morning >= t))
        elig = []
        for cid, d in sorted(self.D.C.customers.items()):
            if d["type"] == "VisitingChef" or d["unlock"] > self.day: continue
            if cid not in self.unlocked:
                if d["pop_unlock"] and self.pop < d["pop_unlock"]: continue
                self.unlocked.add(cid)
            self.cs(cid); elig.append(cid)
        cats, green = set(), set()
        for x in self.usable():
            cats |= self.cats(x["food"])
            if self.food(x["food"])["tl"] == "Green": green |= self.cats(x["food"])
        for l in self.listings_open():
            cats |= self.cats(l["food"])
            if self.food(l["food"])["tl"] == "Green": green |= self.cats(l["food"])
        avail = [r for r in self.recipes_today() if all(s["cat"] in (green if r["green_only"] else cats) for s in r["slots"] if not s["opt"])]
        guests = []; self.cravings = {}
        while len(guests) < cap and elig:
            w = [max(0, min(b["maxDelight"], self.cust[c]["delight"])) for c in elig]; tot = sum(w)
            if tot <= 0 and guests: break
            t = self.rng.random() * (tot if tot > 0 else len(elig)); idx = len(elig) - 1
            for i, c in enumerate(elig):
                t -= w[i] if tot > 0 else 1
                if t < 0: idx = i; break
            g = elig.pop(idx); guests.append(g)
            self.cravings[g] = self.choose_cravings(g, avail) if self.day >= b["cravingsStartDay"] else []
        self.guests, self.cap = guests, cap
        self.cart = Counter(); self.receipt = None; self.phase = "market"; self.browse_used = 0
        self.plans = Counter(); self.bistro_opened = False; self.queue = []; self.qi = 0; self.book_open = False
        self.notice = ""
        self.sync()

    def checkout(self):
        if not self.cart: return "Your cart is empty."
        err = self.allow("Checkout")
        if err: return "[GUIDE] " + err
        total = 0.0
        for lid, q in self.cart.items():
            l = self.listing(lid)
            if l not in self.listings_open(): return f"{self.fname(l['food'])} is not available right now."
            total += self.price(l) * q
        total = round(total, 2)
        if total > max(0.0, self.money) + 1e-9: return f"Cannot afford the full cart (${total:.2f}) with ${self.money:.2f}."
        second = self.receipt is not None
        self.money -= total; self.summary["spent"] += total
        for x in self.inv: x["fresh"] = False  # only this checkout's batches share its sorting refund
        new = []
        for lid, q in self.cart.items():
            l = self.listing(lid)
            for _ in range(q):
                self.batch_id += 1
                self.inv.append(dict(id=self.batch_id, food=l["food"], servings=l["servings"], days=self.shelf_life(l["food"]),
                                     cost=self.price(l) / l["servings"], fresh=True))
            if l["food"] not in self.mastered and l["food"] not in new: new.append(l["food"])
        self.receipt = dict(total=total, rate=self.refund_rate(), new=new)
        self.cart = Counter(); self.set_flag("tutorial.first_purchase")
        if second: self.note("tutorial.first_second_checkout")
        self.succeeded("Checkout")
        out = f"Checkout complete: paid ${total:.2f}. Money now ${self.money:.2f}."
        if new: self.phase = "sorting"; self.sorting = new
        else: out += " No new foods to sort."
        self.sync()
        return out

    def submit_sort(self, answers):
        if set(answers) != set(self.sorting): return "Answer every new food exactly once: " + ", ".join(self.sorting)
        correct = [f for f in self.sorting if answers[f] == self.food(f)["tl"]]
        rate = self.receipt["rate"]
        refund = 0.0
        for f in correct:
            paid = sum(x["cost"] * x["servings"] for x in self.inv if x.get("fresh") and x["food"] == f)
            refund += math.floor(paid * rate * 100 + 0.5 + 1e-9) / 100
        refund = min(self.receipt["total"], round(refund, 2))
        self.money += refund; self.summary["spent"] -= refund; self.summary["refund"] += refund
        for x in self.inv:
            if x.get("fresh"):
                if x["food"] in correct: x["cost"] *= 1 - rate
                x["fresh"] = False
        self.mastered.update(correct)
        prev = self.summary["sort"] or dict(asked=[], correct=0, wrong={})
        self.summary["sort"] = dict(asked=prev["asked"] + list(self.sorting), correct=prev["correct"] + len(correct),
                                    wrong={**prev["wrong"], **{f: answers[f] for f in self.sorting if f not in correct}})
        lines = [f"Sorting result: {len(correct)}/{len(self.sorting)} correct. Refund ${refund:.2f}. Money now ${self.money:.2f}."]
        for f in self.sorting:
            ok = f in correct
            lines.append(f"  {self.fname(f)}: you said {answers[f]}, catalogue says {self.food(f)['tl']} " + ("(correct)" if ok else "(wrong bin · no refund)") + ". " +
                         (self.D.food_rows[f].get("sorting_explanation") or f"This game's food catalogue classifies {self.fname(f)} as {self.food(f)['tl']}. Detailed explanation and research are pending."))
        if len(correct) < len(self.sorting): self.note("tutorial.first_sort_mistake")
        self.sync()  # the sort-mistake lesson belongs to the sorting screen
        self.sorting = None; self.phase = "market"
        self.notice = "Purchase complete. Shop more, or type `bistro` when you're ready to cook."
        self.sync()
        return "\n".join(lines)

    # ------------------------------------------------------------- service (GameLoop)
    def current_guest(self):
        if self.phase != "service" or self.qi >= len(self.queue): return None
        kind, x = self.queue[self.qi]
        return x if kind == "guest" else None
    def enter_bistro(self):
        err = self.allow("BeginService")
        if err: return "[GUIDE] " + err
        self.book_open = False; self.notice = ""
        if self.bistro_opened:
            self.phase = "service"
            if self.current_guest(): self.enter_meal_selection()
            else: self.advance(start=True)
            self.sync()
            return "Back at the bistro."
        self.bistro_opened = True; self.phase = "service"; self.queue = []; self.qi = 0
        story = sorted([s for s in self.D.seqs.values() if self.seq_ok(s, "ServiceEncounter", self.context(s["customer_id"]))],
                       key=lambda s: (-int(s["priority"] or 0), s["sequence_id"].lower()))
        self.queue = [("story", s["sequence_id"]) for s in story] + [("guest", g) for g in self.guests if self.cs(g)["served_day"] != self.day]
        self.advance(start=True)
        return "The bistro is open."
    def leave_bistro(self):
        if not self.D.b.get("cityDuringService"): return "The market is closed once the bistro opens."
        if not self.current_guest(): return "Nothing to leave: service is over."
        self.note("tutorial.first_city_during_service")
        self.phase = "market"; self.browse_used = 0
        self.notice = "Your guests will wait. Shop, then type `bistro` to go back."
        self.sync()
        return "You head into the city."
    def food_aid(self):
        """FoodAidService: once a day, when no unlocked recipe can be made (and, with the city open during service,
        money is short too), gift low-appeal green food for one recipe."""
        b = self.D.b
        if not b.get("foodAidEnabled") or getattr(self, "aid_day", 0) == self.day: return
        if b.get("cityDuringService") and self.money >= b.get("foodAidMoneyBelow", 0): return
        counts = self.pantry_counts(); recipes = self.recipes_today()
        if any(self.can_make(r, counts) for r in recipes): return
        cand = lambda cat: sorted(f for f, d in self.D.C.foods.items() if cat in d["cats"] and d["tl"] == "Green" and d["appeal"] <= b.get("foodAidMaxAppeal", 4) and f in self.D.C.minprice)
        opts = [r for r in recipes if all(cand(c) for c in self.need(r))]
        if not opts: return
        rng = random.Random(self.day * 7919 + len(opts)); r = opts[rng.randrange(len(opts))]
        names = []
        for cat, n in sorted(self.need(r).items()):
            pool = cand(cat); f = pool[rng.randrange(len(pool))]; self.batch_id += 1
            self.inv.append(dict(id=self.batch_id, food=f, servings=n, days=max(1, int(b.get("foodAidShelfLifeDays", 1))), cost=self.D.C.minprice[f], fresh=False))
            names.append(f"{self.fname(f)} ×{n}")
        self.aid_day = self.day; self.summary["aid"] = f"A local farmer dropped off {', '.join(names)}: enough for {r['name']}."
        self.say("[FARMER] " + self.summary["aid"]); self.set_flag("tutorial.food_aid_given")
    def enter_meal_selection(self):
        self.previews_used = 0
        self.food_aid()
    def advance(self, start=False):
        if not start: self.qi += 1
        while self.qi < len(self.queue):
            kind, x = self.queue[self.qi]
            if kind == "story":
                s = self.D.seqs[x]; ctx = self.context(s["customer_id"])
                if self.seq_ok(s, "ServiceEncounter", ctx): self.play_seq(s, ctx)
                self.qi += 1; continue
            c = self.cs(x); c["visits"] += 1
            # Arrival (a pending crash swaps it for the guest's CrashArrival lines), then BeforeService, then cooking.
            ctx = self.context(x)
            self.play_seq(self.find_seq("CrashArrival" if c["crash"] else "CustomerArrival", ctx), ctx)
            ctx = self.context(x)
            self.play_seq(self.find_seq("BeforeService", ctx), ctx)
            self.enter_meal_selection()
            self.sync()
            return
        self.close_service()

    def parse_meal(self, args):
        if not args: return None, "Give a recipe id and slot fillings, e.g. R002 0=P017 1=P005 2=P008"
        rid = args[0].upper()
        if rid not in self.D.C.recipes or self.D.C.recipes[rid]["unlock"] > self.day: return None, f"Recipe {rid} is not on today's menu."
        r = self.D.C.recipes[rid]; sel = [[] for _ in r["slots"]]
        for a in args[1:]:
            if "=" not in a: return None, f"Bad slot filling '{a}'. Use slot=FOOD or slot=FOOD,FOOD."
            k, v = a.split("=", 1); fids = [f.strip().upper() for f in v.split(",") if f.strip()]
            for f in fids:
                if f not in self.D.C.foods: return None, f"Unknown food {f}."
            if not k.isdigit() or not 0 <= int(k) < len(r["slots"]): return None, f"Recipe {rid} has slots 0..{len(r['slots'])-1}."
            sel[int(k)] += fids
        need = Counter(f for s in sel for f in s); have = self.pantry_counts()
        for f, n in need.items():
            if have[f] < n: return None, f"No more servings of {self.fname(f)} in your pantry (have {have[f]}, need {n})."
        if not need: return None, "Add ingredients to the slots first."
        return (r, sel), None

    def recent_serves(self, rid):
        win = max(1, int(self.D.b.get("recipeFatigueWindowDays", 2)))
        return sum(1 for d, x in self.serves if x == rid and self.day - win < d <= self.day)
    def fatigued(self, rid):
        thr = int(self.D.b.get("recipeFatigueThreshold", 0))
        return thr > 0 and self.recent_serves(rid) >= thr
    def guest_info(self, g):
        c = self.cust[g]; dd = sorted(c.get("dd", set()))
        return f"Health {c['health']} · Delight {c['delight']} · Daily Dozen eaten {len(dd)}/11" + (f": {join_and(readable(x) for x in dd)}" if dd else "")
    def meal_eval(self, r, sel, g):
        crav = self.cravings.get(g, []) if self.day >= self.D.b["cravingsStartDay"] else []
        e = evaluate(self.D.C, r, [list(s) for s in sel], self.cust[g], self.day, crav, self.pop, self.well, self.fatigued(r["id"]))
        e["fatigued"] = self.fatigued(r["id"]); e["recent"] = self.recent_serves(r["id"])
        warn = []
        # MealAllocation.SlotWarning-style messages for each slot (a two-category food fits either slot).
        for s, p in zip(r["slots"], sel):
            if not p and s["opt"]: continue
            if any(s["cat"] not in self.cats(f) for f in p): warn.append(f"{readable(s['cat'])}: contains an ingredient of another category.")
            elif len(p) != s["n"]: warn.append(f"{readable(s['cat'])}: needs exactly {s['n']} serving(s); selected {len(p)}.")
        if r["green_only"]:
            for f in sorted({f for p in sel for f in p if self.food(f)["tl"] != "Green"}): warn.append(f"{self.fname(f)}: this recipe requires green ingredients.")
        c = self.cust[g]
        e["health_delta"] = max(0, min(100, c["health"] + e["health"])) - c["health"]
        e["delight_delta"] = max(0, min(self.D.b["maxDelight"], c["delight"] + e["delight"])) - c["delight"]
        e["warnings"] = warn; e["recipe"] = r["id"]; e["asked"] = crav
        e["met"] = [t for t in crav if e["built"] and (t in r["tags"] or (t == "Comfort" and e["appeal"] >= self.D.b.get("comfortMinimumAppeal", 7)))]
        return e

    def dish_text(self, e, g):
        crav = e["asked"]
        res = "No request today" if not crav else f"{', '.join(e['met'])} ({len(e['met'])}/{len(crav)})"
        dd = sorted({d for f in e["foods"] for d in self.food(f)["dd"]})
        return ((("Recipe built successfully" if e["built"] else "Recipe mismatch — reduced payment; no tip") + ("\n  " + "\n  ".join(e["warnings"]) if e["warnings"] else "")) +
                f"\nHealth {e['health_delta']:+d} · Delight {e['delight_delta']:+d}" + (" (guest was already fully delighted)" if e["delight_delta"] == 0 and e["delight"] > 0 else "") +
                f"\nIngredient appeal {e['appeal']:.1f} · Popularity {e['pop']:+.0f}" + (" (guests are used to this recipe)" if e.get("fatigued") else "") +
                f"\nCravings resolved: {res}\nPayment ${e['pay']:.2f} + tip ${e['tip']:.2f}\nPaid ingredient cost ${e['cost']:.2f}\nDaily Dozen: {join_and(readable(x) for x in dd) or 'none'}")

    def serve(self, args, commit):
        g = self.current_guest()
        parsed, err = self.parse_meal(args)
        if err: return err
        r, sel = parsed
        e = self.meal_eval(r, sel, g)
        foods = [f for s in sel for f in s]; e["foods"] = foods
        if not commit:
            if self.limits.get("forecast"):
                e["cost"] = self.plate_cost(foods); return "PREVIEW (debug forecast, nothing used yet)\n" + self.dish_text(e, g)
            crav = e["asked"]
            return ("REVIEW (nothing used yet)\n" + ("The plate matches the recipe." if e["built"] else "The plate does not match the recipe. A mismatched dish pays less, earns no tip and disappoints the guest.") +
                    ("\n  " + "\n  ".join(e["warnings"]) if e["warnings"] else "") +
                    f"\nOn the plate: {', '.join(f'{self.fname(f)} ×{n}' for f, n in Counter(foods).items())} · plate appeal {e['appeal']:.1f}" +
                    ("\nThis guest has no request today." if not crav else "\nRequests answered: none until the plate matches the recipe." if not e["built"] else
                     f"\nRequests answered: {len(e['met'])} of {len(crav)}" + (f" ({', '.join(e['met'])})" if e["met"] else "") +
                     (f" · not answered: {', '.join(t for t in crav if t not in e['met'])}" if len(e["met"]) < len(crav) else "")) +
                    "\nHow the guest reacts, and what they pay, is revealed after serving.")
        err = self.allow("Serve", recipe=r["id"], built=e["built"])
        if err: return "[GUIDE] " + err
        e["cost"] = self.plate_cost(foods, consume=True)
        self.inv = [x for x in self.inv if x["servings"] > 0]
        c = self.cust[g]; c["health"] += e["health_delta"]; c["delight"] += e["delight_delta"]; c["served_day"] = self.day
        c["dd"].update(e.get("gcov", []))
        self.serves.append((self.day, r["id"]))
        self.pop = max(0.0, min(self.D.b["maximumPopularity"], self.pop + e["pop"])); self.well = max(0.0, min(100.0, self.well + e["well"]))
        self.money += e["pay"] + e["tip"]; self.summary["earned"] += e["pay"] + e["tip"]
        if e["red"] > 0: c["rush"] = self.day
        self.summary["dishes"].append(dict(guest=g, recipe=r["id"], built=e["built"], foods=foods, lights=[self.food(f)["tl"] for f in foods],
                                           health=e["health_delta"], delight=e["delight_delta"], pop=round(e["pop"], 1), pay=round(e["pay"] + e["tip"], 2),
                                           cravings=e["asked"], resolved=e["resolved"], appeal=round(e["appeal"], 2)))
        self.set_flag("tutorial.first_served")
        if e["built"]: self.set_flag("tutorial.first_recipe_built")
        if not e["built"]: self.note("tutorial.first_incomplete_dish")
        elif e["pop"] < 0: self.note("tutorial.first_popularity_drop")
        self.last_meal = dict(health_delta=e["health_delta"], foods=foods, dd_all=sorted({d for f in foods for d in self.food(f)["dd"]}))
        if self.first_meal is None: self.first_meal = e
        self.succeeded("Serve")
        out = "SERVED\n" + self.dish_text(e, g)
        # The closing dialogue is chosen at serve time (GameLoop.EvaluateServe) and plays after the dish result.
        meal = dict(recipe=r["id"], built=e["built"], health_delta=e["health_delta"], delight_delta=e["delight_delta"], dish_delight=e["delight"],
                    asked=e["asked"], met=e["met"], fatigued=e["fatigued"], recent=e["recent"], appeal=e["appeal"], red=e["red"])
        ctx = self.context(g, meal)
        self.play_seq(self.find_seq("AfterService", ctx), ctx)
        self.sync()  # first-meal feedback and dish lessons: after the dish result and closing dialogue
        self.advance()
        return out
    def plate_cost(self, foods, consume=False):
        cost = 0.0; used = Counter()
        for f in foods:
            batches = sorted([x for x in self.usable() if x["food"] == f and x["servings"] - (0 if consume else used[x["id"]]) > 0],
                             key=lambda x: (x["days"] == -1, x["days"], x["id"]))
            b = batches[0]; cost += b["cost"]
            if consume: b["servings"] -= 1
            else: used[b["id"]] += 1
        return cost

    def skip(self):
        err = self.allow("SkipCustomer")
        if err: return "[GUIDE] " + err
        self.summary["skipped"].append(self.current_guest()); self.note("tutorial.first_skip_customer"); self.advance(); return "Guest skipped."

    def close_service(self):
        b = self.D.b
        w = max(0.0, self.pop - b.get("walkInPopularityThreshold", 0)) * b.get("walkInIncomePerPopularity", 0)
        w = math.floor(w * 100 + 0.5 + 1e-9) / 100
        self.money += w; self.summary["walkin"] = w; self.summary["earned"] += w
        self.phase = "results"; self.sync()

    def finish_day(self):
        b = self.D.b
        self.days.append(dict(day=self.day, money=round(self.money, 2), pop=round(self.pop), well=round(self.well, 1), guests=list(self.guests),
                              events=[e["id"] for e in self.active_events()], plan=dict(self.plans), **{k: v for k, v in self.summary.items()}))
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
        for c in self.cust.values():  # RushCrashProgress.StartDay: a crash stays pending until its dialogue plays
            if c["rush"] and c["rush"] == self.day - 1: c["crash"] = True
        rent = b.get("rentPerDay", 0) if self.day >= b.get("rentStartDay", 99) else 0
        self.rent_today = rent
        if rent: self.money -= rent; self.set_flag("tutorial.rent_started")
        self.start_day()

    def select_ending(self):
        def ok(v, lo, hi): lo, hi = num(lo), num(hi); return (lo < 0 or v >= lo) and (hi < 0 or v < hi)  # EndingService.InRange
        rows_ = sorted(self.D.endings.values(), key=lambda e: (-int(e["priority"]), e["ending_id"]))
        best = next((e for e in rows_ if ok(self.pop, e["min_popularity"], e["max_popularity"]) and ok(self.well, e["min_wellbeing"], e["max_wellbeing"])
                     and (e.get("min_money") in ("", None) or self.money >= float(e["min_money"])) and (e.get("max_money") in ("", None) or self.money < float(e["max_money"]))), None)
        best = best or next(e for e in rows_ if e["fallback"].lower() == "true")
        ctx = self.context(None)
        for ln in self.D.lines[best["dialogue_id"]]: self.say(f"{'Guide' if ln['speaker_id'] == GUIDE_SPEAKER else ln['speaker_id']}: {self.resolve(ln['text_template'], ctx)}")
        return best["ending_id"]

    # ------------------------------------------------------------- screens
    def header(self):
        return f"=== BLUE ZONE BISTRO · DAY {self.day} of {self.D.b['totalDays']} · {self.phase.upper()} · Money ${self.money:.2f} · Popularity {self.pop:.0f}" + \
               (f" (+{self.publicity():.0f} publicity)" if self.publicity() else "") + " ==="
    def guests_text(self):
        names = []
        for g in self.guests:
            c = self.cust[g]; n = self.D.C.customers[g]["name"]
            names.append(n + (f" (wants: {', '.join(self.cravings[g])})" if self.cravings[g] else "") + (" — served" if c["served_day"] == self.day else ""))
        return f"Today's guests ({len(self.guests)}): " + ", ".join(names)
    def screen(self):
        out = [self.header()]
        if self.pending_text: out += self.pending_text; out.append("---"); self.pending_text = []
        if self.notice: out.append(self.notice)
        if self.phase == "market" and self.memory and self.browse_used == 0 and not self.cart and self.receipt is None:
            out.append(f"What you remember from before: {self.memory}")
        if self.phase == "market":
            out.append(self.guests_text())
            if self.bistro_opened and self.current_guest_waiting(): out.append(f"Waiting at your bistro: {self.D.C.customers[self.current_guest_waiting()]['name']}.")
            if self.summary.get("rent"): out.append(f"Rent paid this morning: ${self.summary['rent']:.2f}")
            if self.active_events(): out.append("News: " + " | ".join(self.D.events[e["id"]]["headline"] + " — " + self.D.events[e["id"]]["summary"] for e in self.active_events()))
            for sid, s in self.D.stores.items():
                cl = self.closure(sid)
                out.append(f"Store {sid} {s['name']} ({s['role']}): " + (cl if cl else "open. Shelves: " + ", ".join(f"{k} ({v})" for k, v in s["shelves"])))
            out.append(self.plan_text())
            out.append(self.cart_text())
            out.append("Pantry: " + self.pantry_text())
            out.append("Commands: shelf <STORE> [SHELF] · flip <LISTING> · add/remove <LISTING> [qty] · checkout · recipes · plan/unplan <RECIPE> · find <FOOD TYPE> · pantry · sell <FOOD> [n] · guests · news · bistro · note <text>")
        elif self.phase == "sorting":
            out.append("Sort each NEW food you bought into Green (G), Yellow (Y) or Red (R). Flip info shown:")
            for f in self.sorting:
                r = self.D.food_rows[f]
                out.append(f"  {f} {r['display_name']} — label \"{r['marketing_label']}\" — appeal {float(r['ingredient_appeal']):g}/10 — ingredients: {r['ingredient_list']}")
            out.append("Command: sort " + " ".join(f"{f}=?" for f in self.sorting) + "   (use G, Y or R)")
        elif self.phase == "service":
            g = self.current_guest(); d = self.D.C.customers[g]
            crav = self.cravings.get(g, [])
            out.append(f"Now serving: {d['name']} · " + ("No special request today" if not crav else "Hoping for: " + ", ".join(crav)))
            out.append("  " + self.guest_info(g))
            out.append("Menu:")
            for r in self.recipes_today():
                req = ", ".join(f"[{i}] {s['n']}×{readable(s['cat'])}" + (" (optional)" if s["opt"] else "") for i, s in enumerate(r["slots"]))
                n = self.recent_serves(r["id"]); win = int(self.D.b.get("recipeFatigueWindowDays", 2))
                hist = "" if not n or not self.D.b.get("recipeFatigueThreshold") else (f" — served {n}× in {win} days" + (f" · popularity ×{self.D.b.get('recipeFatigueMultiplier', 0.5):g}" if self.fatigued(r["id"]) else ""))
                fit = self.request_fit(r, crav)
                ready = "" if self.can_make(r, self.pantry_counts()) else " — not enough in your pantry"
                out.append(f"  {r['id']} {r['name']} — slots {req} — base price ${r['pay']:.2f} — good for: {', '.join(r['tags'])}" + (f" — {fit}" if fit else "") + hist + ready)
            out.append(self.plan_text())
            out.append("Pantry: " + self.pantry_text(cats=True))
            out.append("Commands: preview <RECIPE> 0=FOOD 1=FOOD,FOOD ... · serve <same> · skip" + (" · city" if self.D.b.get("cityDuringService") else "") + " · guests · note <text>")
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
                out.append(f"  {self.D.C.customers[dsh['guest']]['name']} · {self.D.C.recipes[dsh['recipe']]['name']} · {'Recipe built' if dsh['built'] else 'Recipe mismatch'} · Health {dsh['health']:+d} · Delight {dsh['delight']:+d} · Earned ${dsh['pay']:.2f}")
            out.append(f"Money now ${self.money:.2f}. Command: next")
        elif self.phase == "ended":
            e = self.D.endings[self.ending]
            out.append(f"THE END: {e['title']}. Final money ${self.money:.2f}, popularity {self.pop:.0f}.")
        return "\n".join(out)
    def current_guest_waiting(self):
        if self.qi >= len(self.queue): return None
        kind, x = self.queue[self.qi]
        return x if kind == "guest" else None
    def request_fit(self, r, asked):
        """The service recipe card's footer (BistroUIScreen.Service.RequestFit)."""
        if not asked: return None
        fits = sum(1 for t in asked if t == "Comfort" or t in r["tags"]); allf = fits == len(asked)
        comfort = f" (Comfort: appeal {self.D.b.get('comfortMinimumAppeal', 7):g}+)" if "Comfort" in asked else ""
        return (("Fits the request" if len(asked) == 1 else "Fits every request") if allf else
                ("Doesn't fit the request" + ("" if len(asked) == 1 else "s")) if fits == 0 else f"Fits {fits} of {len(asked)} requests") + comfort
    def cart_text(self):
        if not self.cart: return "Cart: empty"
        parts = []; tot = 0
        for lid, q in self.cart.items():
            l = self.listing(lid); p = self.price(l) * q; tot += p
            parts.append(f"{q}× {self.fname(l['food'])} ({lid}) ${p:.2f}")
        return f"Cart (${tot:.2f}): " + "; ".join(parts)
    def pantry_text(self, cats=False):
        c = defaultdict(lambda: [0, 99])
        for x in self.usable():
            c[x["food"]][0] += x["servings"]
            if x["days"] >= 0: c[x["food"]][1] = min(c[x["food"]][1], x["days"])
        if not c: return "empty"
        return "; ".join(f"{f} {self.fname(f)}" + (f" [{self.cat_text(f)}, appeal {float(self.D.food_rows[f]['ingredient_appeal']):g}]" if cats else "") + f" ×{n}" + (f" (use within {dmin} day{'s' if dmin != 1 else ''})" if dmin != 99 else "")
                         for f, (n, dmin) in sorted(c.items()))
    def shelf_text(self, sid, shelf=None):
        if sid not in self.D.stores: return "Unknown store."
        cl = self.closure(sid)
        if cl:
            self.note("tutorial.clicked_locked_store" if self.D.stores[sid]["unlock"] > self.day else "tutorial.clicked_closed_store")
            self.sync(); msg = f"{self.D.stores[sid]['name']}: {cl}"
            if self.pending_text: msg += "\n" + "\n".join(self.pending_text); self.pending_text = []
            return msg
        err = self.allow("EnterStore") or (self.allow("OpenShelf") if shelf else None)
        if err: return "[GUIDE] " + err
        self.succeeded("EnterStore")
        if shelf: self.succeeded("OpenShelf")
        out = [f"{self.D.stores[sid]['name']} — {self.D.stores[sid]['desc']}"]
        for k, v in self.D.stores[sid]["shelves"]:
            if shelf and k != shelf.upper(): continue
            items = [l for l in self.D.C.listings if self.listing_unlocked(l) and l["store"] == sid and l["shelf"] == k]
            shut = self.sold_out(dict(food="", store=sid, shelf=k))
            out.append(f" {v} ({k}):" + (f" {shut}" if shut else "" if items else " (nothing here today)"))
            if shut: continue
            for l in items:
                r = self.D.food_rows[l["food"]]; p = self.price(l); so = self.sold_out(l)
                out.append(f"   {l['id']} {r['display_name']} — \"{r['marketing_label']}\" — appeal {float(r['ingredient_appeal']):g}/10 — " +
                           (so if so else f"${p:.2f}" + (f" (was ${l['price']:.2f})" if abs(p - l["price"]) > 1e-9 else "") + f" for {l['servings']} servings"))
        self.sync()
        if self.pending_text: out = self.pending_text + ["---"] + out; self.pending_text = []
        return "\n".join(out)
    def flip(self, lid):
        l = self.listing(lid.upper())
        if not l: return "Unknown listing."
        r = self.D.food_rows[l["food"]]; f = self.food(l["food"]); life = self.shelf_life(l["food"])
        return (f"{l['id']} {r['display_name']} (card back)\n  Ingredients ({r['ingredient_count']}): {r['ingredient_list']}\n  Category: {self.cat_text(l['food'])} · Storage: {readable(f['storage'])}"
                f" ({'keeps' if life < 0 else f'lasts {life} days'})\n  ${self.price(l):.2f} for {l['servings']} servings = ${self.price(l)/l['servings']:.2f} per serving")
    def find(self, words):
        """Navigation helper: click a food type in the plan panel → one place that sells it."""
        key = "".join(words).replace(" ", "").lower()
        cat = next((c for c in sorted({c for f in self.D.C.foods.values() for c in f["cats"]}) if c.lower() == key), None)
        if not cat: return "Unknown food type. Food types: " + ", ".join(sorted({readable(c) for f in self.D.C.foods.values() for c in f["cats"]}))
        opts = [l for l in self.listings_open() if cat in self.cats(l["food"])]
        if not opts: return f"No open store sells {readable(cat)} today."
        cheap = [l for l in opts if self.price(l) <= self.money]
        l = self.rng.choice(cheap) if cheap else min(opts, key=lambda l: self.price(l))
        return f"{readable(cat)}: try {self.fname(l['food'])} ({l['id']}) at {self.D.stores[l['store']]['name']}, {self.D.store_shelf_name[(l['store'], l['shelf'])]} aisle — ${self.price(l):.2f} for {l['servings']} servings."
    def sell_value(self, food):
        """Unity PantryService: rate × min(lowest shelf price per serving, paid per serving) × servings, per batch."""
        rate = self.D.b.get("pantrySellRate", 0)
        low = min(l["price"] / l["servings"] for l in self.D.C.listings if l["food"] == food)
        return round(sum(rate * min(low, x["cost"]) * x["servings"] for x in self.usable() if x["food"] == food), 2)
    def sell(self, food, n=None):
        """Sell n servings (default all) of a food, soonest-to-spoil batches first, like the game's SELL slot."""
        food = food.upper(); rate = self.D.b.get("pantrySellRate", 0)
        if rate <= 0: return "Selling pantry food is turned off."
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
        self.money += v; self.summary["sold"] = round(self.summary.get("sold", 0) + v, 2); self.set_flag("tutorial.first_pantry_sale")
        return f"Sold {self.fname(food)} ×{n} for ${v:.2f}. Money ${self.money:.2f}."
    def recipes_text(self):
        lines = []
        for r in self.recipes_today():
            n = self.plans.get(r["id"], 0)
            grey = self.hard_active() and self.D.tuts[self.tut_active]["behaviour"] == "Shopping" and not self.affordable(r["id"])
            lines.append(f"  {r['id']} {r['name']} — " + ", ".join(f"[{i}] {s['n']}×{readable(s['cat'])}" + (" (optional)" if s["opt"] else "") for i, s in enumerate(r["slots"])) +
                         f" — base price ${r['pay']:.2f} — good for: {', '.join(r['tags'])}" + (f" — planned ×{n}" if n else "") + (" — (greyed: costs more than you have)" if grey else ""))
        return f"Recipe book (plan up to {len(self.guests)} dish{'es' if len(self.guests) != 1 else ''}, one per guest):\n" + "\n".join(lines)
    def plan_cmd(self, cmd, args):
        if not args: return f"{cmd} <RECIPE>"
        rid = args[0].upper(); r = self.D.C.recipes.get(rid)
        if not r or r["unlock"] > self.day: return f"Recipe {rid} is not in today's book."
        action = "PlanDish" if cmd == "plan" else "UnplanDish"
        err = self.allow(action, recipe=rid)
        if err: return "[GUIDE] " + err
        if cmd == "plan":
            if sum(self.plans.values()) >= len(self.guests): return f"You can plan one dish per guest today ({len(self.guests)})."
            self.plans[rid] += 1
        else:
            if not self.plans.get(rid): return f"{r['name']} is not planned."
            self.plans[rid] -= 1
            if not self.plans[rid]: del self.plans[rid]
        self.succeeded(action); self.sync()
        out = self.plan_text()
        if self.pending_text: out = "\n".join(self.pending_text) + "\n---\n" + out; self.pending_text = []
        return out

    # ------------------------------------------------------------- command dispatch
    def command(self, cmd, args):
        reply = self.dispatch(cmd.lower(), args)
        # Guide lessons and dialogue raised by this command but not shown by a screen yet come first.
        if self.pending_text: reply = "\n".join(self.pending_text) + "\n---\n" + reply; self.pending_text = []
        return reply
    def dispatch(self, cmd, args):
        if cmd == "remember":
            words = " ".join(args).split(); cap = self.limits["memory_words"]
            self.memory = " ".join(words[:cap]); self.log.append(dict(day=self.day, phase=self.phase, memory=self.memory))
            return f"Remembered ({min(len(words), cap)} words{'; the rest is forgotten' if len(words) > cap else ''})."
        if cmd == "recall": return f"What you remember: {self.memory or '(nothing)'}"
        BROWSE = ("shelf", "flip", "recipes", "pantry", "news", "more", "find", "guests")
        if cmd in BROWSE and self.phase in ("market",):
            if self.browse_used >= self.limits["browse"]:
                self.log.append(dict(day=self.day, phase=self.phase, cmd=cmd, refused="impatient"))
                return "You're getting impatient and the shop is busy. No more browsing today: decide with what you remember (add/remove/checkout)."
            self.browse_used += 1
        if cmd == "more":
            t = "\n".join(self.more_text) or "(nothing more to read)"; self.more_text = []; return t
        if cmd == "note": self.log.append(dict(day=self.day, phase=self.phase, note=" ".join(args))); return "Noted."
        if cmd in ("screen", "look"): self.sync(); return self.screen()
        self.log.append(dict(day=self.day, phase=self.phase, cmd=" ".join([cmd] + args)))
        # The recipe book is a panel at the market: plan, unplan and close work inside it; anything else closes it first.
        BOOK = ("recipes", "plan", "unplan", "close", "note", "guests", "info", "news", "pantry")
        if self.book_open and cmd not in BOOK:
            err = self.allow("CloseRecipes")
            if err: return "[GUIDE] " + err
            self.book_open = False; self.succeeded("CloseRecipes"); self.sync()
        def with_lessons(text):
            self.sync()
            if self.pending_text: text = "\n".join(self.pending_text) + "\n---\n" + text; self.pending_text = []
            return text
        if cmd == "pantry":
            self.note("tutorial.seen_pantry")
            t = "Pantry: " + self.pantry_text(cats=True)
            if self.phase == "market" and self.D.b.get("pantrySellRate", 0) > 0 and self.usable():
                t += "\nSell values for all servings (command: sell <FOOD> [servings]): " + "; ".join(f"{f} ${self.sell_value(f):.2f}" for f in sorted({x['food'] for x in self.usable()}))
            return with_lessons(t)
        if cmd == "guests":
            self.note("tutorial.seen_guests")
            return with_lessons(self.guests_text())
        if cmd == "info":
            item = (args[0].lower() if args else "")
            key = {"day": "day", "popularity": "popularity", "money": "wallet", "wallet": "wallet"}.get(item)
            if not key: return "info day | popularity | money"
            self.note("tutorial.toolbar_" + key)
            return with_lessons(f"Day {self.day} of {self.D.b['totalDays']} · Popularity {self.pop:.0f} · Money ${self.money:.2f}")
        if cmd == "recipes":
            if self.phase == "market":
                err = self.allow("OpenRecipes")
                if err: return "[GUIDE] " + err
                self.book_open = True; self.succeeded("OpenRecipes")
            return with_lessons(self.recipes_text())
        if cmd == "news":
            ev = self.active_events()
            return "No active news." if not ev else "\n".join(f"{self.D.events[e['id']]['headline']}: {self.D.events[e['id']]['details']} (until day {e['expiry']-1})" for e in ev)
        if self.phase == "market":
            if cmd in ("plan", "unplan"):
                if not self.book_open: return "Open the recipe book first (`recipes`)."
                return self.plan_cmd(cmd, args)
            if cmd == "close":
                if not self.book_open: return "The recipe book is not open."
                err = self.allow("CloseRecipes")
                if err: return "[GUIDE] " + err
                self.book_open = False; self.succeeded("CloseRecipes"); return with_lessons("Recipe book closed.\n" + self.plan_text())
            if cmd == "shelf": return self.shelf_text(args[0].upper(), args[1].upper() if len(args) > 1 else None) if args else "shelf <STORE> [SHELF]"
            if cmd == "flip": return self.flip(args[0]) if args else "flip <LISTING>"
            if cmd == "find": return self.find(args) if args else "find <FOOD TYPE>, e.g. find Grain"
            if cmd in ("add", "remove"):
                if not args: return f"{cmd} <LISTING> [qty]"
                lid = args[0].upper(); q = int(args[1]) if len(args) > 1 else 1
                l = next((x for x in self.listings_open() if x["id"] == lid), None)
                if not l: return "That listing is not available right now."
                if cmd == "add": self.cart[lid] += q
                else:
                    self.cart[lid] -= q
                    if self.cart[lid] <= 0: del self.cart[lid]
                return self.cart_text() + "\n" + self.plan_text()
            if cmd == "sell": return self.sell(args[0], int(args[1]) if len(args) > 1 else None) if args else "sell <FOOD> [servings]"
            if cmd == "checkout":
                r = self.checkout()
                return r + "\n\n" + self.screen() if r.startswith("Checkout complete") else r
            if cmd == "bistro":
                r = self.enter_bistro()
                return r + "\n\n" + self.screen() if not r.startswith("[GUIDE]") else r
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
                r = self.skip(); return r + "\n\n" + self.screen() if not r.startswith("[GUIDE]") else r
            if cmd == "city":
                r = self.leave_bistro(); return r + "\n\n" + self.screen()
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
                       days=g.days, log=g.log, customers={k: {kk: (sorted(vv) if isinstance(vv, set) else vv) for kk, vv in v.items() if kk != "hist"} for k, v in g.cust.items()}),
                  open(path + ".json", "w"), indent=1, default=str)
    print(out)

if __name__ == "__main__":
    main()
