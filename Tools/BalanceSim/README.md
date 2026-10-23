# Balance simulator

A headless Python 3 port of the game's scoring rules that plays many six-day runs from the real
`Assets/GameData/CSV/*.csv` tables and `Assets/GameData/BalanceConfig.asset`. Use it after any
change to prices, recipes, customers or balance values, before hand playtesting.

```
python Tools/BalanceSim/bzb_sim.py .          # 300 runs per bot, one JSON summary line each
python Tools/BalanceSim/bzb_sim.py . trace    # one detailed run per bot: guests, dishes, foods, effects
```

No packages are needed. One full run takes a few milliseconds.

## Bots (play styles)

| Bot | Plays like |
| --- | --- |
| `health` | Green only, maximizes health, mostly ignores taste and cravings |
| `balanced` | Informed player: green, tasty, chases cravings |
| `popular` | Naive player: picks by appeal and cravings, any Traffic Light |
| `thrift` | Green only, cheapest builds |
| `frugal` | Informed player who also watches money (stands in for a smart human) |

All bots know today's guests and cravings (the Market shows them), buy whole packages from the
cheapest open listing, reuse pantry stock, downgrade a dish to a cheaper build when the cart is
unaffordable, and turn a guest away only as a last resort. Day 1's first dish is the tutorial
recipe (R001). Sorting accuracy defaults to 100% (`summarize(..., acc=0.6)` for weaker players).

## What it models / does not model

Ported: meal health/delight/pay/tip, craving resolution and popularity fraction, popularity and
wellbeing caps, attendance capacity and delight-weighted selection, popularity unlocks, cravings
feasibility, sorting refund, shelf life and expiry, delayed finance, endings.

Not modelled: morning events (random noise; off), rush/crash dialogue (no stat effect), menu/UI
friction, players who misread recipes. Results describe what a competent player of each style
*can* reach, not how a first-time human feels.

## Keeping it honest

This is a port, so it can drift from the C#. If you change `MealService`, `CommunityProgress`,
`CustomerVisitService`, `DayService` or `FoodSortingService` formulas, mirror the change in
`evaluate()` / `Run` here. To try tuning without editing files, construct
`Content(csv_root, balance_path, overrides={...}, csv_patch=fn)`.

## Baseline results (2026-10-22, 300 runs per bot)

Before tuning (startingMoney 30, popularityPerDelight 5, recipe pay 6–8): every green bot reached
E_BLUE in 300/300 runs, popularity hit its 1000 cap by day 5, money grew 30 → ~140 and was never
short.

After tuning (startingMoney 15, popularityPerDelight 3, recipe pay 4–5.5):

| Bot | Ending | Pop | Wellbeing | Money day 1 → 6 |
| --- | --- | --- | --- | --- |
| health | E_NICHE 97% | 645 | 81 | 16 → 66 |
| balanced | E_BLUE 99% | 795 | 82 | 16 → 65 |
| popular | E_POPULAR 100% | 834 | 58 | 15 → 72 |
| thrift | E_NICHE 100% | 529 | 77 | 17 → 76 |

## Experimental knobs

Rent, walk-ins, the missed-craving penalty and money-bounded endings are now real game mechanics
(2026-10-22) and are read from `BalanceConfig.asset` / `Endings.csv` automatically. The pay
multiplier and delight threshold remain simulator-only experiments.


Pass these in `overrides` to try designs before writing game code:
`rentStartDay`, `rentPerDay` (charged at day start, money may go negative), `bankruptBelow`
(ending `E_BANKRUPT` if final money is below it), `payBase` + `paySlope` (dish pay ×
`payBase + paySlope × popularity / maximumPopularity`), `popularityDelightThreshold` (built dishes
below this delight lose popularity), `popularityPerMissedCraving` (popularity lost per unresolved
craving), and `attendanceThresholds` (seat ladder). `summarize(..., tutorial=False)` drops the
forced first recipe.

Rent findings (2026-10-22, starting money 20, 100–150 runs per bot): rent alone is a flat tax.
Thrift/niche players end with the most money because pay does not depend on popularity and
popularity adds only 1–2 meals over six days. Pay scaled by popularity makes the junk player
richest, but thrift still roughly matches the informed player. No competent bot goes bankrupt,
because staples are always affordable, so bankruptcy only comes from mistakes, which bots do not make.

Second round (2026-10-22, start 20, rent from day 3, bankrupt below -10, 200 runs):

| Config | health | balanced | popular | thrift |
| --- | --- | --- | --- | --- |
| A: rent 8, current ladder | Niche, money 41 | Blue, 40 | Popular, 46 | Niche, 51 |
| B: rent 10, ladder 0/300/450/600/750, missed craving -15 | Niche, 36 | Blue, 36 | Popular, 57 | Niche, 44 |

- Delight thresholds of 4–10 hurt every play style (even informed players fell to Niche). Not recommended.
- The missed-craving penalty only affects players who ignore requests. Recommended.
- Every play style serves 12–18 meals, so money gaps stay at 10–20. No bot went bankrupt.
- The informed bot pays for tasty ingredients, so it ends near the thrift bot on money.

## Scored search (`search.py`), 2026-10-22, after the store restock

`python Tools/BalanceSim/search.py Assets/GameData search <configs> <runs>` samples random combinations of
config, price and pay knobs and scores each against the design intent written at the top of the file
(informed → Blue and profitable, junk → Popular and profitable, health/thrift → Niche and poorer,
cheap healthy food must still make customers healthy, start money ≥ 20, competent players rarely
bankrupt). Lower score = closer to intent. `search2` adds walk-in income.

Findings:
1. 240 combinations of start money, rent, popularity per delight, missed-craving penalty, seat
   ladder, recipe pay, and junk/staple/premium price scales: **none** made niche players poorer than
   winners. With 6 regulars, popularity barely changes how many meals you sell.
2. Walk-in income proportional to all popularity helps the junk player but also pays niche bistros.
3. **Walk-in income only above a popularity threshold works.** Best config (60 runs):
   start 20, rent 9/day from day 3, walk-ins 0.08 × (popularity − 450) per day, recipe pay ×0.8,
   missed craving −15 popularity, popularityPerDelight 3, current seat ladder.

| Bot | Ending | Final money |
| --- | --- | --- |
| popular (junk) | Popular 100% | 95 |
| frugal (smart informed) | Blue 93% | 68 |
| balanced (informed, overspends on luxuries) | Niche 77% | 39 |
| health | Niche 100% | 37 |
| thrift | Niche 100% | 32 |

Rent 11 or more is a cliff: even the frugal informed player gets squeezed into cheap food, loses
popularity and drops to Niche (Blue 13–20%), while health players start going bankrupt (up to 15%).
Niche players are relatively poorer, not broke, at rent 9–10.
