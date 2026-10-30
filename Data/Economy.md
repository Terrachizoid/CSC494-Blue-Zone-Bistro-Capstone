# Economy: rent, walk-ins, craving pressure and bankruptcy

Added 2026-10-22. All values live in `Assets/GameData/BalanceConfig.asset`; the C# defaults in
`BalanceConfig.cs` keep these mechanics **off** (0) so the isolated tests, which build
`new BalanceConfig()`, are unaffected.

| Setting | Value | Meaning |
| --- | --- | --- |
| `startingMoney` | 20 | Enough to cook any day-1 recipe freely |
| `rentStartDay` / `rentPerDay` | 3 / 11 | Charged each morning from day 3 (9 until AI round 6) |
| `walkInPopularityThreshold` / `walkInIncomePerPopularity` | 450 / 0.035 | Evening takeaway income = 0.035 × (popularity − 450), if positive (0.08 → 0.05 → 0.035 across AI rounds, see `PlaytestReport_AI.md`) |
| `failedRecipePopularityPenalty` | 20 | Popularity lost for serving a plate that does not match its recipe (40 until AI round 7) |
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

## Selling pantry food (2026-10-24)

`PantryService` lets a broke player raise cash instead of serving unfinished dishes. At the market, before
checkout, the Pantry panel has a SELL slot that works like a recipe slot: each drag adds one serving from that
batch, clicking SELL opens it so servings can be dragged back out, and the Sell button confirms. Each serving
is worth `pantrySellRate` × min(lowest shelf price per serving, price actually paid per serving). The sale is
validated as a whole and applied atomically (`PantryService.Sell(List<PantrySaleItem>)`).
The rate is 0.75 in `BalanceConfig.asset` (C# default 0 = off, like the other economy knobs), the same
share as the failed-dish payment. Using the lower of the two prices means a buy → sort (50% refund on that food) →
sell loop can never make money. Spoiled batches cannot be sold. Sales show in the day's results as
"Sold pantry food" and are not counted as earnings. `GameLoop.SellPantry` only works in the Market phase.

Rent is now announced the day before it starts (`T_RENT` on day `rentStartDay − 1`, plus the existing
landlord line in that day's results), and `T_SELL` explains selling once rent has started and money is below one day's rent (`tutorial.money_low`).

## Farmer's food aid (2026-10-24)

`FoodAidService` stops the empty-pantry dead end. Each time a guest reaches recipe choice, `GameLoop.EnterMealSelection`
asks it to check: if no unlocked recipe can be completed from the pantry and no gift was given today
(`GameSessionState.lastFoodAidDay`), it picks a random unlocked recipe whose required categories all have candidates
and adds exactly the required servings, each a random **green** food of that category with appeal ≤ `foodAidMaxAppeal`
(4). Gifted batches are ordinary inventory: cost = cheapest shelf price per serving (so selling and meal cost use the
normal rules), shelf life `foodAidShelfLifeDays` (1). The pick is seeded by day, so a reloaded checkpoint gives the
same crate. `FoodDatabase.InCategory` (built with the ID index) supplies the per-category lists. The meal screen and
results show "A local farmer dropped off …"; the first gift triggers the `T_FARMER` lesson. Toggle: `foodAidEnabled`
(C# default off, on in the asset). Intent: a stuck player can serve about one dish a day and tread water; low appeal
keeps popularity growth slow, so shopping is still needed to recover properly.

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
