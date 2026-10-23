"""Scored random search over config + price/pay knobs. Lower score = closer to the design intent.
Design intent (2026-10-22, Owen):
  balanced (informed, tasty, cravings) -> E_BLUE, profitable
  popular  (junk/appeal)              -> E_POPULAR, profitable
  health   (green, ignores taste)      -> E_NICHE, struggling for money
  thrift   (cheapest green)            -> niche/struggling, low money, bankrupt possible
Constraints: start money >= 20; day 1 should not feel tight; thrift must still make customers healthy
(plan §3.5: health must be reachable with cheap staples); competent players rarely go bankrupt.
"""
import sys, json, random, itertools, statistics as st
from multiprocessing import Pool
sys.path.insert(0, '.')
from bzb_sim import Content, Run, STRATEGIES

ROOT = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else "Assets/GameData"
LADDERS = {"L4": [0., 250., 500., 750.], "L5a": [0., 300., 450., 600., 750.], "L5b": [0., 300., 400., 550., 700.]}
SPACE2 = dict(startingMoney=[20, 25], rentStartDay=[3], rentPerDay=[8, 10, 12, 14, 16, 20],
             popularityPerDelight=[3, 4], popularityPerMissedCraving=[0, 15], ladder=["L4", "L5a"],
             payScale=[0.6, 0.8, 1.0], junkScale=[1.0], stapleScale=[1.0], premiumScale=[1.0],
             walkInIncomePerPopularity=[0.01, 0.015, 0.02, 0.025, 0.03])
SPACE = dict(startingMoney=[20, 25, 30], rentStartDay=[3, 4], rentPerDay=[0, 6, 8, 10, 12, 14, 16],
             popularityPerDelight=[3, 4, 5], popularityPerMissedCraving=[0, 10, 15, 20], ladder=list(LADDERS),
             payScale=[0.8, 1.0, 1.2, 1.4, 1.6], junkScale=[0.7, 1.0, 1.3], stapleScale=[0.8, 1.0, 1.25], premiumScale=[0.8, 1.0, 1.25])

def build(p):
    def patch(recipes, prices, foods, **k):
        light = {f["id"]: f["traffic_light"] for f in foods}
        for r in recipes: r["base_sale_price"] = str(round(float(r["base_sale_price"]) * p["payScale"], 2))
        for l in prices:
            s = 1.0
            if light[l["food_id"]] == "Red": s = p["junkScale"]
            elif l["store_id"] == "S001": s = p["premiumScale"]
            elif light[l["food_id"]] == "Green": s = p["stapleScale"]
            l["game_price"] = str(round(float(l["game_price"]) * s, 2))
    over = {k: v for k, v in p.items() if k in ("startingMoney", "rentStartDay", "rentPerDay", "popularityPerDelight", "popularityPerMissedCraving", "walkInIncomePerPopularity", "walkInPopularityThreshold")}
    over.update(attendanceThresholds=LADDERS[p["ladder"]], bankruptBelow=-10)
    return Content(ROOT + "/CSV", ROOT + "/BalanceConfig.asset", over, patch)

def metrics(C, n, seed0=1, acc=1.0):
    out = {}
    for s in STRATEGIES:
        runs = [Run(C, s, seed0 + i, acc, tutorial=False).play() for i in range(n)]
        ends = [r.ending() for r in runs]
        out[s] = dict(
            p=lambda *e, ends=ends: sum(x in e for x in ends) / len(ends),
            ends={e: ends.count(e) for e in set(ends)},
            money=st.mean(r.money for r in runs), bankrupt=sum(r.money < -10 for r in runs) / n,
            day1short=sum(1 in r.stats["money_short"] for r in runs) / n,
            well=st.mean(r.well for r in runs), pop=st.mean(r.pop for r in runs),
            health=st.mean(cs["health"] for r in runs for cs in r.cust.values()),
            meals=st.mean(sum(d["served"] for d in r.stats["days"]) for r in runs))
    return out

def score(m):
    b, p, h, t = m["balanced"], m["popular"], m["health"], m["thrift"]
    pen = {}
    pen["balanced_blue"] = 3 * max(0, 0.8 - b["p"]("E_BLUE"))
    pen["popular_popular"] = 3 * max(0, 0.7 - p["p"]("E_POPULAR"))
    pen["health_niche"] = 2 * max(0, 0.7 - h["p"]("E_NICHE", "E_STRUGGLING", "E_BANKRUPT"))
    pen["thrift_niche"] = 2 * max(0, 0.8 - t["p"]("E_NICHE", "E_STRUGGLING", "E_BANKRUPT"))
    pen["winners_profit"] = (max(0, 30 - b["money"]) + max(0, 30 - p["money"])) / 10
    pen["niche_struggle"] = (max(0, h["money"] - 12) + max(0, t["money"] - 8)) / 10
    pen["niche_not_crushed"] = (max(0, -10 - h["money"]) + max(0, -15 - t["money"])) / 10
    pen["gap"] = max(0, 25 - (min(b["money"], p["money"]) - max(h["money"], t["money"]))) / 10
    pen["winners_safe"] = 5 * (max(0, b["bankrupt"] - 0.05) + max(0, p["bankrupt"] - 0.05))
    pen["thrift_bankrupt_cap"] = 2 * max(0, t["bankrupt"] - 0.6) + 2 * max(0, h["bankrupt"] - 0.35)
    pen["day1_freedom"] = 2 * max(0, b["day1short"] - 0.2)
    pen["cheap_health_reachable"] = max(0, 70 - t["well"]) / 5 + max(0, 65 - t["health"]) / 5
    pen["no_runaway_money"] = max(0, max(b["money"], p["money"]) - 120) / 20
    return sum(pen.values()), pen

def evaluate(args):
    p, n, seed0 = args
    m = metrics(build(p), n, seed0)
    s, pen = score(m)
    slim = {k: {kk: (round(vv, 2) if isinstance(vv, float) else vv) for kk, vv in v.items() if kk != "p"} for k, v in m.items()}
    return dict(params=p, score=round(s, 3), penalties={k: round(v, 2) for k, v in pen.items() if v > 0}, metrics=slim)

if __name__ == "__main__":
    mode = sys.argv[2] if len(sys.argv) > 2 else "search"
    rng = random.Random(7)
    if mode in ("search", "search2"):
        n_cfg, n_runs = int(sys.argv[3]), int(sys.argv[4])
        space = SPACE2 if mode == "search2" else SPACE
        cfgs = [dict(t) for t in {tuple(sorted({k: rng.choice(v) for k, v in space.items()}.items())) for _ in range(n_cfg * 3)}][:n_cfg]
        with Pool(2) as pool:
            res = pool.map(evaluate, [(c, n_runs, 1) for c in cfgs], chunksize=4)
        res.sort(key=lambda r: r["score"])
        json.dump(res, open(f"{mode}_results.json", "w"), indent=1)
        for r in res[:12]: print(r["score"], r["params"], r["penalties"])
    elif mode == "refine":
        res = json.load(open(sys.argv[5] if len(sys.argv) > 5 else "search_results.json"))[:int(sys.argv[3])]
        n_runs = int(sys.argv[4])
        with Pool(2) as pool:
            out = pool.map(evaluate, [(r["params"], n_runs, 1001) for r in res])
        out.sort(key=lambda r: r["score"])
        json.dump(out, open("refine_results.json", "w"), indent=1)
        for r in out: print(r["score"], r["params"], r["penalties"])
