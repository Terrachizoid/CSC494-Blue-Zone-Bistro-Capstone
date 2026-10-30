"""Round-5 bot matrix: normal bots plus extreme cases on the current rules (visible-info decisions)."""
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bzb_sim import Content, summarize
root = sys.argv[1]; n = int(sys.argv[2]) if len(sys.argv) > 2 else 100
csv, asset = os.path.join(root, "Assets/GameData/CSV"), os.path.join(root, "Assets/GameData/BalanceConfig.asset")
base = Content(csv, asset)
old = Content(csv, asset, overrides=dict(pantrySellRate=0, foodAidEnabled=0))
broke = Content(csv, asset, overrides=dict(startingMoney=8))
cases = [("health", base, "health", 1, 0), ("balanced", base, "balanced", 1, 0), ("frugal", base, "frugal", 1, 0),
         ("popular (taste)", base, "popular", 1, 0), ("thrift", base, "thrift", 1, 0),
         ("luxury buyer", base, "luxury", 1, 0), ("junk chaser", base, "junk", 1, 0), ("never shops after day 1", base, "noshop", 1, 0),
         ("careless: 20% mistakes, 60% sorting", base, "balanced", .6, .2), ("careless, no sell/farmer", old, "balanced", .6, .2),
         ("broke start $8", broke, "balanced", 1, 0), ("broke start $8, no sell/farmer", Content(csv, asset, overrides=dict(startingMoney=8, pantrySellRate=0, foodAidEnabled=0)), "balanced", 1, 0)]
for name, C, strat, acc, mis in cases[int(sys.argv[3]) if len(sys.argv) > 3 else 0:]:
    r = summarize(C, strat, n=n, acc=acc, mistake=mis)
    print(json.dumps(dict(case=name, endings=r["endings"], money=r["final_money"], money_by_day=r["money_by_day"], pop=r["final_pop"],
                          well=r["final_well"], neg_end=r["negative_at_end"], skipped=r["customers_skipped"], nongreen=r["nongreen_servings_per_dish"],
                          farmer=r["farmer_days"], sold=r["sold_per_run"], mistakes=r["mistakes"])), flush=True)
