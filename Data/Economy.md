# Economy: rent, walk-ins, craving pressure and bankruptcy

Added 2026-10-22. All values live in `Assets/GameData/BalanceConfig.asset`; the C# defaults in
`BalanceConfig.cs` keep these mechanics **off** (0) so the isolated tests, which build
`new BalanceConfig()`, are unaffected.

| Setting | Value | Meaning |
| --- | --- | --- |
| `startingMoney` | 20 | Enough to cook any day-1 recipe freely |
| `rentStartDay` / `rentPerDay` | 3 / 9 | Charged each morning from day 3 |
| `walkInPopularityThreshold` / `walkInIncomePerPopularity` | 450 / 0.08 | Evening takeaway income = 0.08 × (popularity − 450), if positive |
| `popularityPerMissedCraving` | 15 | Popularity lost per active craving a built dish leaves unresolved (day 4+) |
| `popularityPerDelight` | 3 | Popularity per point of dish delight |
| Recipe pay (`Recipes.csv`) | 3.2–4.4 | 80% of the previous values |
| Bankrupt ending | money < −10 after day 6 | `E_BANKRUPT`, priority 200, `max_money` −10 in `Endings.csv` |

## How it works in code

- **Rent:** `DayService.FinishDay` charges rent after advancing the day, so it is part of the
  day-start checkpoint and is never charged twice on resume. It sets `tutorial.rent_started`,
  which opens the `T_RENT` lesson at the next market. `DayResult.rentCharged` →
  `DaySummary.rentPaid`.
- **Walk-ins:** `DayService.CloseService` is called once from `GameLoop.ShowResults` (phase guard);
  it adds money and is recorded in `DaySummary.walkInIncome` (also included in `moneyEarned`).
- **Craving pressure:** `CommunityProgress.Evaluate` subtracts the penalty for unresolved active
  cravings on successfully built dishes. It depends only on cravings, never on health.
- **Negative money:** allowed. Checkout requires `total ≤ max(0, money)`, so an empty cart still
  works and the player can keep serving from the pantry. Checkpoints accept negative money.
- **Bankruptcy:** `EndingDefinition` has optional money bounds (`min_money` inclusive,
  `max_money` exclusive; columns optional in `Endings.csv`). Endings are only chosen after day 6.
- **UI:** the Results screen shows walk-in income, rent paid this morning, the landlord's notice
  the day before rent starts, and current money.

## Design intent and messaging

Popular, delightful bistros profit; bistros that ignore requests and serve unexciting food fall
behind. Healthy cooking is never the cause: cheap staples stay the lowest-cost way to cook.
The rent lesson and the bankrupt ending text say this explicitly. Keep it that way when editing.

## Balance evidence (simulator, 100 runs per bot, final files)

| Bot | Ending | Final money |
| --- | --- | --- |
| frugal (smart informed) | Blue 98% | 70 |
| popular (junk) | Popular 100% | 94 |
| balanced (informed, overspends on luxuries) | Niche 81% | 38 |
| health | Niche 99% | 38 |
| thrift | Niche 100% | 33 |

No competent bot went bankrupt; bankruptcy comes from player mistakes, so measure it with AI or
human playtests. Rent of 11 or more is a cliff (informed players lose Blue). Re-run
`python Tools/BalanceSim/bzb_sim.py .` after any change; see `Tools/BalanceSim/README.md`.
