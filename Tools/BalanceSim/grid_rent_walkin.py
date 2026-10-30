"""Round-6 grid: rent × walk-in rate, scored on final money per play style."""
import sys, os, json, itertools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bzb_sim import Content, summarize
root = sys.argv[1]; n = int(sys.argv[2])
csv, asset = os.path.join(root, "Assets/GameData/CSV"), os.path.join(root, "Assets/GameData/BalanceConfig.asset")
for rent, walk in itertools.product([9, 11, 12, 14], [0.05, 0.035, 0.025, 0.015]):
    C = Content(csv, asset, overrides=dict(rentPerDay=rent, walkInIncomePerPopularity=walk))
    row = dict(rent=rent, walk=walk)
    for name, strat, acc, mis in [("balanced", "balanced", 1, 0), ("frugal", "frugal", 1, 0), ("health", "health", 1, 0),
                                  ("popular", "popular", 1, 0), ("careless", "balanced", .6, .2)]:
        r = summarize(C, strat, n=n, acc=acc, mistake=mis)
        row[name] = dict(money=r["final_money"], end=r["endings"], bankrupt=r["endings"].get("E_BANKRUPT", 0) / n, neg=r["negative_at_end"],
                         skipped=r["customers_skipped"], farmer=r["farmer_days"])
    print(json.dumps(row), flush=True)
