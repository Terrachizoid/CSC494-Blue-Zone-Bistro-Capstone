# Popularity, cravings and community wellbeing

All tuning lives in `BalanceConfig`. These are prototype balance values, not established playtest results.

## State and ownership

- `GameSessionState` saves popularity, community wellbeing, permanent customer unlocks, and the morning customer plan.
- `CustomerState` saves that day's active cravings alongside the existing personal stats.
- `CustomerVisitService` prepares the morning plan. `MealService` computes ingredient appeal, cravings and dish outcomes. `CommunityProgress` validates tuning, migrates old state and computes global changes.
- `GameLoop` requests the new morning plan before the checkpoint event. Bootstrap also prepares it before saving a new game or migrating a legacy save. UI queries only read it.

## Attendance

Popularity starts at 300 and ranges from 0 to 1000. Morning popularity sets the daily capacity: 0–249 gives one customer; 250–499 two; 500–749 three; 750–1000 four. Distinct eligible customers are selected with weights equal to their personal delight. If none can be selected by weight, one eligible customer is selected uniformly. Chefs are separate encounters and do not occupy these seats.

A customer's popularity requirement is the optional `unlock_popularity` column in `Customers.csv` (0 = none; currently Marco U005 = 500, Priya U006 = 750), in addition to their unlock day. Unlocks persist even if popularity drops. The day's queue and cravings never reroll on queries or resume. Popularity changes during service affect the next morning's capacity.

From day 4, select one or two distinct cravings from each visitor's CSV pool. A combination must have an unlocked recipe covering all selected tags, with required categories obtainable from accessible stores or usable pantry food (respecting green-only recipes). This is a content-feasibility check, not a guarantee that the player can currently afford every dish. Unsupported pools yield no requests rather than blocking service.

## Dish outcomes

Ingredient appeal is a serving-weighted mean of food CSV `ingredient_appeal` values (0–10). Fifty servings of one food have the same appeal as one serving of that food.

For a correctly built recipe, dish delight is the floored sum of weighted appeal, recipe delight, and 4 points per resolved craving, capped at +20. Each contribution has its own configurable multiplier. A failed recipe retains -30 delight, resolves no cravings, and pays 75% of minimum ingredient value with no tip. Ingredient price no longer adds delight.

Before day 4, a valid recipe adds dish delight × `popularityPerDelight` popularity (3 in `BalanceConfig.asset` since the 2026-10-22 tuning; the C# default and the isolated tests still use 5). From day 4, multiply that gain by resolved cravings / active cravings; no resolved or active cravings means no gain. Positive gain is capped at 100 per dish. An invalid recipe loses 40 popularity. From day 4 a built dish also loses `popularityPerMissedCraving` (15 in the asset, 0 in C# defaults) per active craving it leaves unresolved (2026-10-22, see `Economy.md`). Skipping currently adds no new penalty.

Community wellbeing starts at 50, ranges from 0 to 100, and changes by dish health effect × 0.25, independently of cravings and recipe success. Both global calculations use dish effects before clamping individual customer stats, so a customer already at 100 can still contribute. `GameContentQueries.GetCommunityVisualScore` supplies a normalized value for future visuals; no city saturation filter is added yet.

## Content and saves

After editing CSVs, use **Tools → Blue Zone Bistro → Import All CSV Content**. Invalid ingredient appeal rejects the food import before modifying any food assets.

Old checkpoints initialize the new scores from config once, then save a morning plan. A legitimate saved popularity of zero remains zero. Existing day-start-only save semantics are unchanged.

Tests: `Tools/Test-CsvContent.ps1`, `Tools/Test-ContentImport.ps1`, and the real Unity `CheckpointSmokeTest` cover calculations, queue caching, migration, serialization and content import.

## Comfort request and recipe fatigue (2026-10-25)

- **Comfort** (`CravingTag.Comfort`, appended last) is answered by any built plate whose mean ingredient appeal
  reaches `BalanceConfig.comfortMinimumAppeal` (7), not by recipe tags. It makes high-appeal foods (junk, luxury,
  sweet potato, berries, dates) worth considering. Customers.csv gives it to Dev, Sam and Marco. Craving selection
  treats Comfort as answerable by any recipe (`CustomerVisitService.ChooseCravings`).
- **Recipe fatigue** (`Services/RecipeFatigue.cs`): if a recipe has already been served `recipeFatigueThreshold` (4)
  times to anyone within `recipeFatigueWindowDays` (2, counting today), its positive popularity is multiplied by
  `recipeFatigueMultiplier` (0.5). Negative popularity is unchanged. `ServeResult.recipeFatigued/recipeRecentServes`
  report it; recipe buttons show "Served N× in 2 days". Threshold 0 turns it off (C# default).
- `GameSessionState.recipeServes` records every served dish (day, recipe, guest, built, green/yellow/red servings);
  `CustomerState.dailyDozenEaten` records the Daily Dozen groups each guest has eaten from green foods. Both feed the
  guest info line and the end-of-game recap.
- Bots: Comfort, fatigue and the new recipes leave junk and taste-chasing at Struggling (junk wellbeing 47); informed
  play still reaches Blue (46/60 runs, $34). See `Tools/BalanceSim/README.md`.

## Junk as the crowd route, Daily Dozen novelty (2026-10-25, second pass)

Owen's intent: a junk-food bistro should reach "A full house, an unfinished promise" (Popular) and make the most money,
while wellbeing collapses. Bots showed it could not: every recipe forced greens (diluting plate appeal), junk-fed guests'
finance fell to 0 (no tips), and appeal moved popularity only a little.

- `popularityPerAppealPoint` (15) × max(0, plate appeal − `popularityAppealPivot` (7)) is added to a built dish's
  popularity before the requests-answered fraction (`CommunityProgress.Evaluate`). Only indulgent plates draw a crowd;
  plain staples get nothing, tasty greens (7) get none to a little.
- New recipe R015 Snack platter (Starchy vegetable + Beverage, optional Other, day 1): chips + soda + candy, or sweet
  potato + water.
- `dailyDozenNewGroupsOnly` (on) with `healthPerGreenDailyDozenCategory` 2 (was 1): a dish's Daily Dozen health bonus
  counts only groups that guest has not eaten before in the run (`CustomerState.dailyDozenEaten`). Repeating the same
  plants for the same guest no longer stacks health; variety does.

Bots (40 runs): junk → Popular 36/40, $47 (most money), wellbeing 14; balanced → Blue, $37; health-only → Niche, $22;
taste chaser → mixed, $33; luxury → mostly Niche, $25; careless → low, $8. Stronger crowd values (20, or pivot 6.5)
made junk $66–68, too dominant.
