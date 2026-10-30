"""Blue Zone Bistro headless balance simulator.

Python port of the C# rules (MealService, CommunityProgress, CustomerVisitService,
DayService, FoodSortingService, PurchaseService, EndingService) driven by the real CSVs
and BalanceConfig.asset. Bots play six days with different shopping/cooking styles.
Events are off by default (they are random +/- noise around the baseline).
"""
import csv, math, random, re, sys, os, json, statistics as st
from collections import defaultdict, Counter
from itertools import combinations_with_replacement

def load_csv(root, name):
    with open(os.path.join(root, f"Blue_Zone_Bistro_{name}.csv"), encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))

def load_balance(path):
    b = {}
    text = open(path, encoding="utf-8").read()
    for m in re.finditer(r"^  (\w+): ([-\d.]+)\s*$", text, re.M):
        v = m.group(2); b[m.group(1)] = float(v) if "." in v else int(v)
    th = re.search(r"attendanceThresholds:\n((?:  - [\d.]+\n)+)", text)
    b["attendanceThresholds"] = [float(x) for x in re.findall(r"- ([\d.]+)", th.group(1))]
    b["customerPopularityUnlocks"] = {c: float(p) for c, p in re.findall(r"customerId: (\w+)\n\s+popularity: ([\d.]+)", text)}
    return b

class Content:
    def __init__(self, csv_root, balance_path, overrides=None, csv_patch=None):
        self.b = load_balance(balance_path)
        self.b.update(overrides or {})
        foods = load_csv(csv_root, "Foods"); prices = load_csv(csv_root, "Prices")
        recipes = load_csv(csv_root, "Recipes"); customers = load_csv(csv_root, "Customers")
        stores = load_csv(csv_root, "Stores"); endings = load_csv(csv_root, "Endings")
        if csv_patch: csv_patch(foods=foods, prices=prices, recipes=recipes, customers=customers, stores=stores)
        self.foods = {}
        for f in foods:
            dd = [x for x in f["daily_dozen_categories"].split("|") if x and x != "None"]
            self.foods[f["id"]] = dict(id=f["id"], name=f["display_name"], cat=f["recipe_category"], dd=dd,
                                       tl=f["traffic_light"], storage=f["storage_type"], appeal=float(f["ingredient_appeal"]))
        self.stores = {s["store_id"]: dict(id=s["store_id"], unlock=int(s["unlock_day"])) for s in stores}
        self.listings = []
        for p in prices:
            self.listings.append(dict(id=p["listing_id"], food=p["food_id"], store=p["store_id"], shelf=p["shelf_id"],
                                      unlock=int(p["unlock_day"]), flag=p["required_flag"], servings=int(p["servings_per_package"]),
                                      price=float(p["game_price"])))
        self.minprice = {}
        for l in self.listings:
            pps = l["price"] / l["servings"]
            self.minprice[l["food"]] = min(self.minprice.get(l["food"], 1e9), pps)
        self.recipes = {}
        for r in recipes:
            slots = []
            for col, opt in (("required_slots", False), ("optional_slots", True)):
                for part in [x for x in r[col].split("|") if x]:
                    c, n = part.split(":"); slots.append(dict(cat=c, n=int(n), opt=opt))
            self.recipes[r["recipe_id"]] = dict(id=r["recipe_id"], name=r["display_name"], slots=slots, unlock=int(r["unlock_day"]),
                                                pay=float(r["base_sale_price"]), delight=int(r["delight_value"]),
                                                tags=[t for t in r["craving_tags"].split("|") if t],
                                                green_only=r["allow_non_green_choices"].strip().lower() != "true")
        self.customers = {}
        for c in customers:
            self.customers[c["customer_id"]] = dict(id=c["customer_id"], name=c["display_name"], type=c["customer_type"],
                unlock=int(c["unlock_day"]), health=int(c["starting_health"]),
                pop_unlock=float(c["unlock_popularity"]) if c.get("unlock_popularity") not in (None, "") else self.b["customerPopularityUnlocks"].get(c["customer_id"]),
                cravings=[t for t in c["craving_tags"].split("|") if t])
        self.endings = endings

    def shelf_life(self, storage):
        return {"Fresh": self.b["freshDays"], "Frozen": self.b["frozenDays"], "Canned": self.b["cannedDays"],
                "Dry": self.b["dryDays"], "ShelfStable": self.b["shelfStableDays"]}[storage]

# ------------------------------------------------------------------ dish evaluation (MealService port)

def evaluate(C, recipe, foods_by_slot, cust, day, cravings, popularity, wellbeing, fatigued=False):
    """foods_by_slot: list aligned with recipe slots; each entry a list of food ids (len == n) or [] for empty optional."""
    b = C.b
    built = True
    green = yellow = red = 0; appeal = 0.0; total = 0
    gcov = set(); minval = 0.0
    for slot, picks in zip(recipe["slots"], foods_by_slot):
        if not picks:
            if not slot["opt"]: built = False
            continue
        if len(picks) != slot["n"]: built = False
        for fid in picks:
            f = C.foods[fid]
            if f["cat"] != slot["cat"]: built = False
            total += 1; appeal += f["appeal"]; minval += C.minprice[fid]
            if f["tl"] == "Green": green += 1; gcov.update(f["dd"])
            elif f["tl"] == "Yellow": yellow += 1
            else: red += 1
            if recipe["green_only"] and f["tl"] != "Green": built = False
    newcov = gcov - set(cust.get("dd", ())) if b.get("dailyDozenNewGroupsOnly") else gcov
    hchange = green * b["healthPerGreenServing"] + len(newcov) * b["healthPerGreenDailyDozenCategory"] \
        - yellow * b["healthPenaltyPerYellowServing"] - red * b["healthPenaltyPerRedServing"]
    hchange = min(b["maxHealthPerDish"], hchange)
    mean_appeal = appeal / total if total else 0.0
    # Comfort is answered by plate appeal (Unity MealService), any recipe.
    resolved = [t for t in cravings if t in recipe["tags"] or (t == "Comfort" and mean_appeal >= b.get("comfortMinimumAppeal", 7))] if built else []
    if built:
        dchange = min(b["maxDelightPerDish"], math.floor(mean_appeal * b["ingredientAppealMultiplier"] +
                      recipe["delight"] * b["recipeDelightMultiplier"] + len(resolved) * b["delightPerResolvedCraving"]))
    else:
        dchange = -b["failedRecipeDelightPenalty"]
    # Optional experiment: a busier bistro charges more (pay multiplier rises with popularity).
    pmul = b.get("payBase", 1.0) + b.get("paySlope", 0.0) * popularity / b["maximumPopularity"]
    pay = recipe["pay"] * pmul if built else minval * b["failedRecipePaymentRate"]
    tip = recipe["pay"] * max(0, min(100, cust["finance"])) / 100 if built else 0
    if built:
        frac = 1 if day < b["cravingsStartDay"] else (0 if not cravings else len(resolved) / len(cravings))
        # Experiment: dishes below a delight threshold lose popularity; ignored cravings cost popularity.
        raw = (dchange - b.get("popularityDelightThreshold", 0)) * b["popularityPerDelight"] \
            + max(0.0, mean_appeal - b.get("popularityAppealPivot", 5)) * b.get("popularityPerAppealPoint", 0)
        pgain = min(b["maximumPopularityGainPerDish"], raw * frac) if raw > 0 else raw
        if day >= b["cravingsStartDay"] and cravings:
            pgain -= b.get("popularityPerMissedCraving", 0) * (len(cravings) - len(resolved))
    else:
        pgain = -b["failedRecipePopularityPenalty"]
    pdelta = max(0, min(b["maximumPopularity"], popularity + pgain)) - popularity
    if fatigued and pdelta > 0: pdelta *= b.get("recipeFatigueMultiplier", 0.5)  # global recipe fatigue
    wdelta = max(0, min(b["maximumCommunityWellbeing"], wellbeing + hchange * b["wellbeingPerHealthPoint"])) - wellbeing
    return dict(built=built, health=hchange, delight=dchange, pay=pay, tip=tip, pop=pdelta, well=wdelta,
                red=red, yellow=yellow, green=green, cov=len(gcov), resolved=len(resolved), servings=total, appeal=mean_appeal, gcov=sorted(gcov))

# ------------------------------------------------------------------ bots

STRATEGIES = {
    # Bots see what a player sees before serving (2026-10-24: Review dish no longer forecasts outcomes):
    # food appeal, Traffic Light belief (green good, red bad), requests the recipe answers, base price, cost.
    # weights: light = (green - yellow - 2*red) servings, appeal = mean appeal, crave = requests answered,
    #          price = recipe base price, cost = ingredient cost; lights = colours the bot is willing to cook
    "health":   dict(w=dict(light=3, appeal=0.3, crave=1, price=0.1, cost=0.3), lights={"Green"}),
    "balanced": dict(w=dict(light=1, appeal=1, crave=1.5, price=0.3, cost=0.5), lights={"Green", "Yellow", "Red"}),
    "frugal":   dict(w=dict(light=1, appeal=0.5, crave=1, price=0.3, cost=2), lights={"Green", "Yellow", "Red"}),
    "popular":  dict(w=dict(light=0, appeal=2, crave=1.5, price=0.2, cost=0.2), lights={"Green", "Yellow", "Red"}),
    "thrift":   dict(w=dict(light=0.5, appeal=0.1, crave=0.5, price=0.3, cost=3), lights={"Green"}),
    # extreme cases
    "luxury":   dict(w=dict(light=1, appeal=3, crave=1, price=0.1, cost=0), lights={"Green", "Yellow"}),
    "junk":     dict(w=dict(light=-1, appeal=2, crave=1.5, price=0.3, cost=1.0), lights={"Green", "Yellow", "Red"}),
    "noshop":   dict(w=dict(light=0.5, appeal=0.1, crave=0.5, price=0.3, cost=3), lights={"Green"}),
}

def visible_score(C, recipe, sel, cravings, w, cost):
    picks = [f for p in sel for f in p]
    if not picks: return -1e9
    g = sum(C.foods[f]["tl"] == "Green" for f in picks); y = sum(C.foods[f]["tl"] == "Yellow" for f in picks)
    r = len(picks) - g - y
    appeal = sum(C.foods[f]["appeal"] for f in picks) / len(picks)
    crave = sum(1 for t in cravings if t in recipe["tags"] or (t == "Comfort" and appeal >= C.b.get("comfortMinimumAppeal", 7)))
    return (w["light"] * (g - y - 2 * r) + w["appeal"] * appeal + w["crave"] * crave + w["price"] * recipe["pay"] - w["cost"] * cost)

def best_build(C, recipe, avail, cust, day, cravings, pop, well, strat, cost_of, fatigued=False):
    w = STRATEGIES[strat]["w"]; lights = STRATEGIES[strat]["lights"]
    bycat = defaultdict(list)
    for fid in avail:
        f = C.foods[fid]
        if f["tl"] in lights and (not recipe["green_only"] or f["tl"] == "Green"): bycat[f["cat"]].append(fid)
    options = []
    for s in recipe["slots"]:
        cands = bycat.get(s["cat"], [])
        opts = [list(c) for c in combinations_with_replacement(sorted(cands), s["n"])] if cands else []
        if s["opt"]: opts = [[]] + opts
        if not opts: return None
        options.append(opts)
    def score(sel):
        e = evaluate(C, recipe, sel, cust, day, cravings, pop, well)
        if not e["built"]: return -1e9, e  # plate validity is visible (Review dish)
        cost = sum(cost_of(f) for picks in sel for f in picks)
        return visible_score(C, recipe, sel, cravings, w, cost) - (2.0 * w["crave"] if fatigued else 0), e  # recipe table shows fatigue
    # coordinate ascent from the first option set
    sel = [o[-1] if o != [[]] else [] for o in options]
    best, _ = score(sel)
    for _ in range(3):
        improved = False
        for i, opts in enumerate(options):
            for o in opts:
                trial = sel[:i] + [o] + sel[i + 1:]
                v, _ = score(trial)
                if v > best + 1e-9: best, sel, improved = v, trial, True
        if not improved: break
    v, e = score(sel)
    return (v, sel, e) if v > -1e9 else None

class Run:
    def __init__(self, C, strat, seed, sort_accuracy=1.0, tutorial=True, log=False, mistake=0.0):
        self.C, self.strat, self.rng, self.acc, self.tutorial, self.log = C, strat, random.Random(seed), sort_accuracy, tutorial, log
        self.mistake = mistake  # chance a planned dish is served with a slot wrong (human error)
        b = C.b
        self.day, self.money = 1, b["startingMoney"]
        self.pop, self.well = b["startingPopularity"], b["startingCommunityWellbeing"]
        self.cust = {}; self.unlocked = set(); self.inv = []; self.mastered = set()
        self.stats = defaultdict(list); self.meals = Counter(); self.first_dish_done = False

    def cstate(self, cid):
        if cid not in self.cust:
            d = self.C.customers[cid]
            self.cust[cid] = dict(health=d["health"], delight=self.C.b["startingDelight"], finance=self.C.b["startingFinance"],
                                  last_end=d["health"], hist={})
        return self.cust[cid]

    def accessible(self):
        return [l for l in self.C.listings if l["unlock"] <= self.day and self.C.stores[l["store"]]["unlock"] <= self.day and not l["flag"]]

    def attendance(self):
        b = self.C.b
        cap = max(1, sum(1 for t in b["attendanceThresholds"] if self.pop >= t))
        elig = []
        for cid, d in sorted(self.C.customers.items()):
            if d["type"] == "VisitingChef" or d["unlock"] > self.day: continue
            if cid not in self.unlocked:
                if d["pop_unlock"] is not None and self.pop < d["pop_unlock"]: continue
                self.unlocked.add(cid)
            self.cstate(cid); elig.append(cid)
        chosen = []
        while len(chosen) < cap and elig:
            ws = [max(0, min(b["maxDelight"], self.cust[c]["delight"])) for c in elig]
            tot = sum(ws)
            if tot <= 0 and chosen: break
            t = self.rng.random() * (tot if tot > 0 else len(elig)); idx = len(elig) - 1
            for i, c in enumerate(elig):
                t -= ws[i] if tot > 0 else 1
                if t < 0: idx = i; break
            chosen.append(elig.pop(idx))
        return chosen, cap

    def choose_cravings(self, cid, recipes_avail):
        b = self.C.b
        if self.day < b["cravingsStartDay"]: return []
        pool = sorted(set(self.C.customers[cid]["cravings"]))
        wanted = b["minimumDailyCravings"] + int(self.rng.random() * (b["maximumDailyCravings"] - b["minimumDailyCravings"] + 1))
        chosen = []
        while len(chosen) < wanted:
            opts = [t for t in pool if any(all(x == "Comfort" or x in r["tags"] for x in chosen + [t]) for r in recipes_avail)]
            if not opts: break
            t = opts[int(self.rng.random() * len(opts))]; chosen.append(t); pool.remove(t)
        return chosen

    def play_day(self):
        C, b = self.C, self.C.b
        rent = b.get("rentPerDay", 0) if self.day >= b.get("rentStartDay", 99) else 0
        self.money -= rent; self.rent_paid = getattr(self, "rent_paid", 0) + rent
        if self.money < 0: self.stats["negative_days"].append(self.day)
        guests, cap = self.attendance()
        listings = self.accessible()
        buy_price = {}
        for l in listings:
            pps = l["price"] / l["servings"]
            if l["food"] not in buy_price or pps < buy_price[l["food"]][0]: buy_price[l["food"]] = (pps, l)
        pantry = Counter()
        for e in self.inv: pantry[e["food"]] += e["servings"]
        avail = set(buy_price) | set(pantry)
        recipes = [r for r in C.recipes.values() if r["unlock"] <= self.day]
        cats = {C.foods[f]["cat"] for f in avail}
        recipes_avail = [r for r in recipes if all(s["cat"] in cats for s in r["slots"] if not s["opt"])]
        cravings = {g: self.choose_cravings(g, recipes_avail) for g in guests}
        cost_of = lambda f: 0.0 if pantry[f] > 0 else buy_price.get(f, (99,))[0]
        # plan dishes
        plan = []; planned = Counter()
        serves = getattr(self, "serves", []); win = max(1, int(b.get("recipeFatigueWindowDays", 2))); thr = int(b.get("recipeFatigueThreshold", 0))
        recent = lambda rid: sum(1 for d, x in serves if x == rid and d > self.day - win)
        fat = lambda rid: thr > 0 and recent(rid) + planned[rid] >= thr
        for i, g in enumerate(guests):
            cs = self.cust[g]
            cands = recipes
            if self.tutorial and not self.first_dish_done and i == 0: cands = [C.recipes["R001"]]
            best = None
            for r in cands:
                strat = self.strat
                if self.tutorial and not self.first_dish_done and i == 0 and strat in ("health", "thrift"): strat = "health"
                res = best_build(C, r, avail, cs, self.day, cravings[g], self.pop, self.well, strat, cost_of, fat(r["id"]))
                if res and (best is None or res[0] > best[0]): best = (res[0], r, res[1], res[2])
            if best: plan.append((g, best)); planned[best[1]["id"]] += 1
        # shopping: buy what pantry lacks; downgrade or drop dishes if unaffordable
        thrifted = set()
        while True:
            need = Counter()
            for g, (v, r, sel, e) in plan:
                for picks in sel:
                    for f in picks: need[f] += 1
            cart = []; total = 0.0
            for f, n in need.items():
                missing = n - pantry[f]
                if missing <= 0: continue
                if f not in buy_price: cart = None; break
                best_l = min((l for l in listings if l["food"] == f), key=lambda l: math.ceil(missing / l["servings"]) * l["price"])
                k = math.ceil(missing / best_l["servings"]); cart.append((best_l, k)); total += k * best_l["price"]
            if self.strat == "noshop" and self.first_dish_done: plan = [p for p in plan if all(pantry[f] >= n for f, n in Counter(f for pk in p[1][2] for f in pk).items())]; cart, total = [], 0.0
            if cart is not None and total <= max(0.0, self.money) + 1e-9: break
            if not plan: cart, total = [], 0.0; break
            self.stats["money_short"].append(self.day)
            # Sell pantry servings the plan does not use, soonest-to-spoil first (Unity PantryService rule).
            if b.get("pantrySellRate", 0) > 0 and not getattr(self, "_sold_today", False):
                self._sold_today = True; keep = Counter(need); gained = 0.0
                for x in sorted(self.inv, key=lambda x: (x["days"] == -1, x["days"])):
                    spare = x["servings"] - keep[x["food"]]
                    keep[x["food"]] = max(0, keep[x["food"]] - x["servings"])
                    if spare <= 0: continue
                    gained += spare * b["pantrySellRate"] * min(C.minprice[x["food"]], x["cost"]); x["servings"] -= spare
                self.inv = [x for x in self.inv if x["servings"] > 0]; pantry = Counter()
                for e2 in self.inv: pantry[e2["food"]] += e2["servings"]
                if gained > 0: self.money += round(gained, 2); self.stats["sold"].append(round(gained, 2)); continue
            # A real player first swaps the priciest dish for a thrift build, then drops a guest.
            def dish_cost(p): return sum(buy_price.get(f, (0,))[0] for picks in p[1][2] for f in picks)
            swapped = False
            for idx in sorted(range(len(plan)), key=lambda i: -dish_cost(plan[i])):
                g = plan[idx][0]
                if g in thrifted: continue
                thrifted.add(g); best = None
                for r in recipes:
                    res = best_build(C, r, avail, self.cust[g], self.day, cravings[g], self.pop, self.well, "thrift", cost_of)
                    if res and (best is None or res[0] > best[0]): best = (res[0], r, res[1], res[2])
                if best: plan[idx] = (g, best); swapped = True; break
            if swapped: continue
            plan.sort(key=lambda p: p[1][0]); dropped = plan.pop(0)
            self.stats["dropped_for_money"].append(self.day)
        self._sold_today = False
        # checkout + sorting refund
        self.money -= total
        # Unity FoodSortingService: maximumSortingDiscount of what was paid for each correctly sorted new food.
        new_foods = sorted({l["food"] for l, k in cart} - self.mastered)
        correct = {f for f in new_foods if self.rng.random() < self.acc}
        rate = b["maximumSortingDiscount"]
        refund = round(sum(round(sum(l["price"] * k for l, k in cart if l["food"] == f) * rate, 2) for f in correct), 2)
        self.mastered.update(correct)
        self.money += refund
        for l, k in cart:
            for _ in range(k):
                self.inv.append(dict(food=l["food"], servings=l["servings"], days=C.shelf_life(C.foods[l["food"]]["storage"]),
                                     cost=l["price"] / l["servings"] * ((1 - rate) if l["food"] in correct else 1)))
        spent = total - refund
        # service
        earned = 0.0; day_meals = []
        for g, (v, r, sel, e) in plan:
            cs = self.cust[g]; fz = thr > 0 and recent(r["id"]) >= thr
            e = evaluate(C, r, sel, cs, self.day, cravings[g], self.pop, self.well, fz)
            if self.mistake and self.rng.random() < self.mistake:
                e = evaluate(C, r, [p[:-1] if p and not s["opt"] else p for p, s in zip(sel, r["slots"])], cs, self.day, cravings[g], self.pop, self.well, fz)
                self.stats["mistakes"].append(self.day)
            for picks in sel:
                for f in picks:
                    batches = sorted([x for x in self.inv if x["food"] == f and x["servings"] > 0], key=lambda x: (x["days"] == -1, x["days"]))
                    batches[0]["servings"] -= 1
            self.inv = [x for x in self.inv if x["servings"] > 0]
            cs["health"] = max(0, min(100, cs["health"] + e["health"]))
            cs.setdefault("dd", set()).update(e["gcov"])
            cs["delight"] = max(0, min(b["maxDelight"], cs["delight"] + e["delight"]))
            self.pop += e["pop"]; self.well += e["well"]
            earned += e["pay"] + e["tip"]; self.money += e["pay"] + e["tip"]
            self.meals[g] += 1; self.first_dish_done = True
            serves.append((self.day, r["id"])); self.serves = serves
            day_meals.append((g, r["id"], e, sel))
        # Farmer's food aid (Unity FoodAidService): once a day, for a guest left unserved because nothing can be made.
        served_ids = {m[0] for m in day_meals}
        for g in guests:
            if g in served_ids or not b.get("foodAidEnabled") or getattr(self, "aid_day", 0) == self.day: continue
            have = Counter()
            for x in self.inv: have[x["food"]] += x["servings"]
            if any(best_build(C, r, set(have), self.cust[g], self.day, cravings[g], self.pop, self.well, "thrift", lambda f: 0) for r in recipes): continue
            cand = lambda cat: sorted(f for f, d in C.foods.items() if d["cat"] == cat and d["tl"] == "Green" and d["appeal"] <= b.get("foodAidMaxAppeal", 4) and f in C.minprice)
            opts = sorted([r for r in recipes if all(cand(s["cat"]) for s in r["slots"] if not s["opt"])], key=lambda r: r["id"])
            if not opts: break
            rng = random.Random(self.day * 7919 + len(opts)); r = opts[rng.randrange(len(opts))]; sel = []
            for s in r["slots"]:
                if s["opt"]: sel.append([]); continue
                pool = cand(s["cat"]); f = pool[rng.randrange(len(pool))]; sel.append([f] * s["n"])
            self.aid_day = self.day; self.stats["aid"].append(self.day)
            cs = self.cust[g]; e = evaluate(C, r, sel, cs, self.day, cravings[g], self.pop, self.well)
            cs["health"] = max(0, min(100, cs["health"] + e["health"])); cs["delight"] = max(0, min(b["maxDelight"], cs["delight"] + e["delight"])); cs.setdefault("dd", set()).update(e["gcov"])
            self.pop += e["pop"]; self.well += e["well"]; earned += e["pay"] + e["tip"]; self.money += e["pay"] + e["tip"]
            self.meals[g] += 1; day_meals.append((g, r["id"], e, sel)); break
        # Experiment: walk-in / takeaway income proportional to popularity (foot traffic).
        walkin = max(0.0, self.pop - b.get("walkInPopularityThreshold", 0.0)) * b.get("walkInIncomePerPopularity", 0.0)
        self.money += walkin; earned += walkin
        skipped = [g for g in guests if g not in {p[0] for p in plan}]
        ingredient_cost = sum(C.minprice[f] for _, _, _, sel in day_meals for picks in sel for f in picks)
        self.stats["days"].append(dict(day=self.day, cap=cap, guests=len(guests), served=len(day_meals), skipped=len(skipped),
                                       spent=round(spent, 2), earned=round(earned, 2), money=round(self.money, 2),
                                       pop=round(self.pop), well=round(self.well, 1), ingredient_value=round(ingredient_cost, 2),
                                       meals=[(g, rid, e["health"], e["delight"], e["red"] + e["yellow"]) for g, rid, e, _ in day_meals]))
        if self.log:
            d = self.stats["days"][-1]
            print(f"  day {d['day']}: cap {cap} guests {guests} served {d['served']} spent {d['spent']} earned {d['earned']} "
                  f"money {d['money']} pop {d['pop']} well {d['well']}")
            for g, rid, e, sel in day_meals:
                print(f"     {C.customers[g]['name']:6} {C.recipes[rid]['name'][:28]:28} {[C.foods[f]['name'] for p in sel for f in p]} "
                      f"H{e['health']:+} D{e['delight']:+} pop{e['pop']:+.0f} cravings {cravings[g]} resolved {e['resolved']}")
        # finish day
        if self.day == b["totalDays"]: return False
        for x in self.inv:
            if x["days"] > 0: x["days"] -= 1
        self.inv = [x for x in self.inv if x["days"] != 0 and x["servings"] > 0]
        for cid, d in C.customers.items():
            if d["unlock"] <= self.day and d["type"] != "VisitingChef": self.cstate(cid)
        for cid, cs in self.cust.items():
            cs["hist"][self.day] = cs["health"] - cs["last_end"]; cs["last_end"] = cs["health"]
            src = cs["hist"].get(self.day + 1 - b["financeHealthLagDays"])
            if src is not None: cs["finance"] = max(0, min(100, cs["finance"] + src))
        self.day += 1
        return True

    def ending(self):
        # Mirrors EndingService: highest priority row whose popularity, wellbeing and money bounds match.
        if self.C.b.get("bankruptBelow") is not None and self.money < self.C.b["bankruptBelow"]: return "E_BANKRUPT"
        best = None
        for e in self.C.endings:
            def ok(v, lo, hi): return (lo in ("", None) or v >= float(lo)) and (hi in ("", None) or v < float(hi))
            if e["fallback"].lower() == "true": continue
            if ok(self.pop, e["min_popularity"], e["max_popularity"]) and ok(self.well, e["min_wellbeing"], e["max_wellbeing"]) \
                    and ok(self.money, e.get("min_money"), e.get("max_money")):
                if best is None or int(e["priority"]) > int(best["priority"]): best = e
        return (best or next(e for e in self.C.endings if e["fallback"].lower() == "true"))["ending_id"]

    def play(self):
        while self.play_day(): pass
        return self

def summarize(C, strat, n=300, acc=1.0, seed0=1, tutorial=True, mistake=0.0):
    runs = [Run(C, strat, seed0 + i, acc, tutorial=tutorial, mistake=mistake).play() for i in range(n)]
    endings = Counter(r.ending() for r in runs)
    fin_money = [r.money for r in runs]
    healths = [cs["health"] for r in runs for cs in r.cust.values()]
    meals = [m for r in runs for cid in r.cust for m in [r.meals[cid]]]
    served = [sum(d["served"] for d in r.stats["days"]) for r in runs]
    skipped = [sum(d["skipped"] for d in r.stats["days"]) for r in runs]
    drop = sum(1 for r in runs if r.stats["money_short"]) / n
    dropped = st.mean(len(r.stats["dropped_for_money"]) for r in runs)
    june = [r.cust["U001"]["health"] for r in runs if "U001" in r.cust]
    money_by_day = [round(st.mean(r.stats["days"][d]["money"] for r in runs), 1) for d in range(C.b["totalDays"])]
    margin = st.mean(sum(d["earned"] for d in r.stats["days"]) / max(0.01, sum(d["spent"] for d in r.stats["days"])) for r in runs)
    hdeltas = [m[2] for r in runs for d in r.stats["days"] for m in d["meals"]]
    nongreen = st.mean(sum(m[4] for d in r.stats["days"] for m in d["meals"]) / max(1, sum(len(d["meals"]) for d in r.stats["days"])) for r in runs)
    neg_end = sum(1 for r in runs if r.money < 0) / n
    neg_days = st.mean(len(r.stats["negative_days"]) for r in runs)
    return dict(strategy=strat, negative_at_end=f"{neg_end:.0%}", days_starting_negative=round(neg_days, 2), endings=dict(endings), final_pop=round(st.mean(r.pop for r in runs)), final_well=round(st.mean(r.well for r in runs), 1),
                money_by_day=money_by_day, final_money=round(st.mean(fin_money), 1), revenue_over_spend=round(margin, 2),
                runs_money_bound=f"{drop:.0%}", guests_unfed_for_money=round(dropped, 2), meals_served=round(st.mean(served), 1), customers_skipped=round(st.mean(skipped), 1),
                meals_per_customer=round(st.mean(meals), 2), health_per_dish=round(st.mean(hdeltas), 1),
                final_health_mean=round(st.mean(healths), 1), final_health_max=max(healths), june_final_health=round(st.mean(june), 1),
                share_high_band=f"{sum(1 for h in healths if h >= 70) / len(healths):.0%}", nongreen_servings_per_dish=round(nongreen, 2),
                farmer_days=round(st.mean(len(r.stats["aid"]) for r in runs), 2), sold_per_run=round(st.mean(sum(r.stats["sold"]) for r in runs), 2),
                mistakes=round(st.mean(len(r.stats["mistakes"]) for r in runs), 2))

if __name__ == "__main__":
    root = sys.argv[1]
    C = Content(os.path.join(root, "Assets/GameData/CSV"), os.path.join(root, "Assets/GameData/BalanceConfig.asset"))
    if len(sys.argv) > 2 and sys.argv[2] == "trace":
        for s in STRATEGIES:
            print(f"== {s} (seed 1)"); Run(C, s, 1, log=True).play()
    else:
        for s in STRATEGIES: print(json.dumps(summarize(C, s)))
