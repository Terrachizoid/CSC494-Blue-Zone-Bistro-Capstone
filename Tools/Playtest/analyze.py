"""Summarise AI playtest sessions: outcome, economy by day, sorting accuracy, food choices, notes."""
import json, sys, glob, os, pickle
from collections import Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "BalanceSim"))
import play  # noqa: needed to unpickle
import __main__; __main__.Game = play.Game; __main__.Data = play.Data

for path in sorted(glob.glob(os.path.join(sys.argv[1], "*.pkl"))):
    g = pickle.load(open(path, "rb"))
    name = os.path.basename(path)[:-4]
    print(f"\n### {name}: phase={g.phase} day={g.day} ending={getattr(g, 'ending', None)} money={g.money:.2f} pop={g.pop:.0f} well={g.well:.1f} commands={sum(1 for x in g.log if 'cmd' in x)}")
    asked = right = 0; wrong = []
    lights = Counter(); built = failed = skipped = 0; walk = rent = refund = 0
    for d in g.days:
        s = d.get("sort")
        if s:
            asked += len(s["asked"]); right += s["correct"]
            wrong += [f"{g.fname(f)}→{a} (is {g.food(f)['tl']})" for f, a in s["wrong"].items()]
        for dish in d["dishes"]:
            lights.update(dish["lights"]); built += dish["built"]; failed += not dish["built"]
        skipped += len(d["skipped"]); walk += d["walkin"]; rent += d["rent"] or 0; refund += d.get("refund", 0)
        print(f"  day {d['day']}: money {d['money']:.2f} pop {d['pop']} well {d['well']} guests {len(d['guests'])} served {len(d['dishes'])} "
              f"skipped {len(d['skipped'])} spent {d['spent']:.2f} earned {d['earned']:.2f} walk-in {d['walkin']:.2f} rent {d['rent'] or 0} events {d['events']}")
        for dish in d["dishes"]:
            print(f"     {dish['guest']} {dish['recipe']} {'built' if dish['built'] else 'MISMATCH'} foods={[g.fname(f) for f in dish['foods']]} "
                  f"H{dish['health']:+d} D{dish['delight']:+d} pop{dish['pop']:+} crav {dish['resolved']}/{len(dish['cravings'])}")
    print(f"  sorting {right}/{asked}; wrong: {wrong}")
    print(f"  spoiled servings {sum(d.get('wasted', 0) for d in g.days)}; left in pantry at end {sum(x['servings'] for x in g.inv)}; sold ${sum(d.get('sold', 0) for d in g.days):.2f}; farmer days {sum(1 for d in g.days if d.get('aid'))}")
    print(f"  servings by light: {dict(lights)}; built {built} mismatched {failed} skipped {skipped}; walk-in total {walk:.2f}; rent total {rent}; refunds {refund:.2f}")
    print("  notes:")
    for n in [x for x in g.log if "note" in x]: print(f"    d{n['day']} {n['phase']}: {n['note']}")
