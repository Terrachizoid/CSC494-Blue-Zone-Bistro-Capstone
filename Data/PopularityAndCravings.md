# Popularity, cravings and community wellbeing

All tuning lives in `BalanceConfig`. These are prototype balance values, not established playtest results.

## State and ownership

- `GameSessionState` saves popularity, community wellbeing, permanent customer unlocks, and the morning customer plan.
- `CustomerState` saves that day's active cravings alongside the existing personal stats.
- `CustomerVisitService` prepares the morning plan. `MealService` computes ingredient appeal, cravings and dish outcomes. `CommunityProgress` validates tuning, migrates old state and computes global changes.
- `GameLoop` requests the new morning plan before the checkpoint event. Bootstrap also prepares it before saving a new game or migrating a legacy save. UI queries only read it.

## Attendance

Popularity starts at 300 and ranges from 0 to 1000. Morning popularity sets the daily capacity: 0–249 gives one customer; 250–499 two; 500–749 three; 750–1000 four. Distinct eligible customers are selected with weights equal to their personal delight. If none can be selected by weight, one eligible customer is selected uniformly. Chefs are separate encounters and do not occupy these seats.

U005 unlocks at 500 and U006 at 750, in addition to their authored unlock days. Unlocks persist even if popularity drops. The day's queue and cravings never reroll on queries or resume. Popularity changes during service affect the next morning's capacity.

From day 4, select one or two distinct cravings from each visitor's CSV pool. A combination must have an unlocked recipe covering all selected tags, with required categories obtainable from accessible stores or usable pantry food (respecting green-only recipes). This is a content-feasibility check, not a guarantee that the player can currently afford every dish. Unsupported pools yield no requests rather than blocking service.

## Dish outcomes

Ingredient appeal is a serving-weighted mean of food CSV `ingredient_appeal` values (0–10). Fifty servings of one food have the same appeal as one serving of that food.

For a correctly built recipe, dish delight is the floored sum of weighted appeal, recipe delight, and 4 points per resolved craving, capped at +20. Each contribution has its own configurable multiplier. A failed recipe retains -30 delight, resolves no cravings, and pays 75% of minimum ingredient value with no tip. Ingredient price no longer adds delight.

Before day 4, a valid recipe adds dish delight × 5 popularity. From day 4, multiply that gain by resolved cravings / active cravings; no resolved or active cravings means no gain. Positive gain is capped at 100 per dish. An invalid recipe loses 40 popularity. Skipping currently adds no new penalty.

Community wellbeing starts at 50, ranges from 0 to 100, and changes by dish health effect × 0.25, independently of cravings and recipe success. Both global calculations use dish effects before clamping individual customer stats, so a customer already at 100 can still contribute. `GameContentQueries.GetCommunityVisualScore` supplies a normalized value for future visuals; no city saturation filter is added yet.

## Content and saves

After editing CSVs, use **Tools → Blue Zone Bistro → Import All CSV Content**. Invalid ingredient appeal rejects the food import before modifying any food assets.

Old checkpoints initialize the new scores from config once, then save a morning plan. A legitimate saved popularity of zero remains zero. Existing day-start-only save semantics are unchanged.

Tests: `Tools/Test-CsvContent.ps1`, `Tools/Test-ContentImport.ps1`, and the real Unity `CheckpointSmokeTest` cover calculations, queue caching, migration, serialization and content import.
