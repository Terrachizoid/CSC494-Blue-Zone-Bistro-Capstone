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

Since 2026-10-24 bots decide only from what a player can see before serving, because Review dish no
longer forecasts outcomes: food appeal, Traffic Light belief (green good, red bad), which requests a
recipe answers, base price and ingredient cost, plus whether the plate matches the recipe. The real
outcome is still computed by the `MealService` port after serving.

| Bot | Plays like |
| --- | --- |
| `health` | Green only, prefers green count over taste and requests |
| `balanced` | Informed player: green, tasty, chases requests |
| `frugal` | Informed player who watches cost closely |
| `popular` | Taste chaser: appeal and requests, any Traffic Light |
| `thrift` | Green only, cheapest builds |
| `luxury` | Extreme: highest appeal, ignores cost (green/yellow) |
| `junk` | Extreme: high appeal, prefers yellow/red |
| `noshop` | Extreme: buys for day 1 only, then lives on pantry, selling and the farmer |

All bots know today's guests and cravings, buy whole packages from the cheapest open listing, reuse
pantry stock, downgrade a dish to a cheaper build when the cart is unaffordable, sell pantry servings
the day's plan does not use when short of money (soonest-to-spoil first), get the farmer's food aid once
a day when a guest is left unserved and nothing can be cooked, and turn a guest away only as a last
resort. `Run(..., mistake=0.2)` serves 20% of dishes with a missing serving (human error);
`summarize(..., acc=0.6)` sets sorting accuracy.

`extremes.py <project_root> [runs] [first_case]` runs the normal bots plus extreme cases (luxury, junk,
never shops, careless player, broke start), each also without selling/farmer for comparison.

## What it models / does not model

Ported: meal health/delight/pay/tip, craving resolution and popularity fraction, popularity and
wellbeing caps, attendance capacity and delight-weighted selection, popularity unlocks, cravings
feasibility, sorting refund, shelf life and expiry, delayed finance, endings.

Not modelled: morning events (random noise; off), rush/crash dialogue (no stat effect), menu/UI
friction. Misread recipes are approximated by `mistake`. Results describe what a competent player of each style
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

## Round-5 matrix (2026-10-24, 100 runs per case, current content, visible-info bots)

| Case | Endings | Final money | Popularity | Wellbeing | Farmer days | Sold $ |
| --- | --- | --- | --- | --- | --- | --- |
| health | Niche 95, Blue 5 | 34 | 613 | 78 | 0 | 0 |
| balanced | Blue 99 | 58 | 817 | 77 | 0 | 0 |
| frugal | Niche 97, Blue 3 | 40 | 629 | 78 | 0 | 0 |
| popular (taste) | Struggling 100 | 22 | 587 | 60 | 0.4 | 6.8 |
| thrift | Struggling 100 | 32 | 488 | 65 | 0 | 0 |
| luxury buyer | Struggling 100 | 26 | 590 | 65 | 0.2 | 9.3 |
| junk chaser | Struggling 100 | 27 | 605 | 47 | 0 | 10.7 |
| never shops after day 1 | Struggling 100 | 19 | 413 | 61 | 4.0 | 0 |
| careless (20% mistakes, 60% sorting) | Struggling 69, Niche 28, Blue 3; 11% end negative, 0 bankrupt | 15 | 483 | 68 | 1.1 | 1.5 |
| careless, no sell/farmer | Struggling 44, Niche 29, **Bankrupt 24**; 42% end negative | 7.5 | 462 | 66 | – | – |
| broke start $8 | Struggling 73, Niche 16, Blue 11 | 25 | 609 | 68 | 0.8 | 6.3 |
| broke start $8, no sell/farmer | Struggling 74, Niche 19, Blue 7 | 24 | 576 | 69 | – | – |

Reading: only the informed `balanced` style reaches Blue; green-but-request-ignoring styles land Niche;
taste, luxury and junk chasers all struggle (junk also wrecks wellbeing). Selling + farmer removed
bankruptcy for careless players (24% → 0%) without making "never shop" viable (Struggling, $19).
Bot quirk: the broke-start bot sometimes sells food a later day needed, so it skips more guests than
without selling; a human would not sell planned stock.

## Rent × walk-in grid (2026-10-24, `grid_rent_walkin.py`)

Used to pick rent $11 / walk-in 0.035 before AI round 6. Balanced-bot final money by (rent, walk-in): 9/0.05 $58,
9/0.025 $40, 11/0.035 $36, 11/0.025 $30, 12/0.035 $33, 14/0.035 $22. Careless bots start going bankrupt at rent 12
(8%) and rent 14 (52%). Plate-mistake popularity penalty 40 → 20 changed bots little but removed careless bankruptcy.

## Comfort + recipe fatigue + new recipes (2026-10-25, 60 runs)

| Case | Endings | Money | Wellbeing | Non-green servings/dish |
| --- | --- | --- | --- | --- |
| balanced | Blue 46, Niche 14 | 34.5 | 75 | 0.0 |
| health | Niche 60 | 21.3 | 76 | 0.0 |
| frugal | Niche 59, Blue 1 | 25.8 | 77 | 0.0 |
| popular (taste) | Struggling 60 | 6.6 | 58 | 0.42 |
| junk | Struggling 60 | 5.5 | 47 | 1.65 |
| luxury | Struggling 58, Niche 2 | 9.3 | 62 | 0.0 |
| careless | Niche 40, Struggling 18, Blue 1, Bankrupt 1 | 6.4 | 66 | 0.0 |

Bots meet Comfort with green high-appeal foods (sweet potato, berries, dates) when they care about health; junk and
taste chasers use chips/cereal/soda and fall behind on wellbeing. Fatigue is visible to bots (they avoid a fatigued
recipe in planning).

## Crowd popularity + snack platter + Daily Dozen novelty (2026-10-25, 40 runs)

| Setting (pivot, per point) | Junk | Balanced | Health | Taste | Luxury |
| --- | --- | --- | --- | --- | --- |
| no snack platter, 7 / 15 | Struggling mostly, $22 | Blue, $37 | Niche, $22 | mixed, $33 | Niche/Struggling, $25 |
| snack platter, 7 / 15 (chosen) | **Popular 36/40, $47**, wellbeing 14 | Blue, $37 | Niche, $22 | mixed, $33 | mostly Niche, $25 |
| snack platter, 7 / 20 | Popular, $66 | Blue, $38 | Niche, $22 | Popular, $47 | mixed, $39 |
| snack platter, 6.5 / 15 | Popular, $68 | Blue, $42 | Niche, $22 | Popular, $45 | mixed, $37 |

The junk bot now buys cheaply (cost weight 1.0); the old version overspent on bacon/cheese/heirloom tomatoes.
