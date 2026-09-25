# Week 3 developer guide

This is a six-day, button-driven greybox. The solo GDD supplies the scope and category-slot approach; the larger capstone plan supplies character themes and design context. The player's later decisions about flexible meals, delight, and delayed finance take precedence over the older plan formulas.

## Open and run

1. Open the project in Unity 6000.4.1f1. Let scripts compile.
2. Run **Tools > Blue Zone Bistro > Import All CSV Content** after editing foods, stores, prices, recipes, or customers. This also imports dialogue in dependency order and validates the imported library.
3. Run **Tools > Blue Zone Bistro > Validate Imported Content** to check the standard imported assets again without importing them.
4. Open the gameplay scene with one active `GameBootstrap`. Missing default references are filled automatically; explicit Inspector assignments are preserved. Save the scene after setup.
5. Press Play. Market → service encounters → meal preview → confirmation → reaction → daily results → next day.

Dialogue CSV changes are detected before Play and build. A failed dialogue import blocks Play with a Console error rather than silently using stale conversations. New character/recipe references require Import All first. Other CSVs still need the explicit import command. Imported `.asset` files are Unity's runtime representation; editing a CSV alone does not update every database.

## Responsibilities and ownership

| Area | Responsibility | Keep out |
| --- | --- | --- |
| `Core/GameBootstrap` | Construct and connect the shared objects once | Gameplay formulas, long per-frame loops |
| `Core/GameLoop` | Accept legal commands and coordinate phases/encounters | Drawing widgets, importing files, calculating meal scores |
| `Services/PurchaseService`, `MealService`, `DayService` | Validate and commit operations | UI controls and presentation strings for screens |
| `Services/CustomerVisitService` | Cache one attendance roll per day | Dialogue priority and line playback |
| `Services/EncounterScheduler` | Put eligible stories before attending customers; build dialogue context | Mutating customer outcomes |
| `Runtime/Dialogue/DialogueService` | Select dialogue and apply completion effects | Another copy of gameplay flags/history |
| `Runtime/Dialogue/DialoguePlaybackController` | Track ordered lines, complete once | Buying, serving, or advancing days |
| `Services/GameContentQueries` | Read catalogue data for UI | Performing purchases or other commands |
| `Services/RecipeRequirements` | Interpret category minimum/maximum once | Inventory mutation |
| `Services/RecipeCheck` | Pantry feasibility hint using those bounds | Reserving food or guaranteeing a later serve succeeds |
| `Runtime/GameSessionState` | Own the run's mutable progress | Authored food/recipe definitions |
| `Runtime/ServiceSessionState` | Encounter cursor, step, dish result, daily summary | Separate persistent customer state |
| `Data` | Shared authored definitions and ID lookup | Runtime pantry, money, or player flags |
| `UI/GreyboxUIController` | Listen to buttons, query data, render current state | Reimplementing scoring or transition rules |
| `Editor` | Import, validate, and wire references | Code needed in a Web player |

The UI calls GameLoop commands. GameLoop calls services. Services change the shared session and return results. The UI reads that state/results and queries the catalogue to redraw. There is no blocking gameplay `while` loop: Unity processes each click and returns to rendering. The short loop in `StartEncounter` only skips invalid queued stories.

`PhaseChanged` observers see a fully prepared service encounter. An empty queue transitions directly to Results without a stale Service notification. Changes between customers or dialogue lines stay inside the Service phase; a future event-driven UI will need a separate encounter/view notification, or explicitly refresh after commands as the greybox does.

## Invariants to preserve

- Pass the same `GameSessionState` to every service. Dialogue uses its `flags` and `dialogueHistory`; a context is a snapshot for selection/text, not a second progression store.
- Treat returned data assets and result objects as read-only in presentation code. Unity serialization uses public fields; it does not enforce immutability.
- A failed **request** has `success=false` and changes nothing. A served but mismatched **recipe** has `success=true`, `recipeBuilt=false`, consumes food, pays 75% of minimum ingredient value, and applies the failure delight penalty.
- Preview and commit use the same meal evaluator. Commit validates again; never consume a previously computed preview directly.
- Inventory selections use batch IDs. Two purchases of the same food may have different expiry or historical cost.
- Required category slots establish a minimum. Required plus optional slots establish the maximum. Extras fail the build. A recipe label is not an exact-ingredient rule.
- Minimum ingredient value comes from all valid store listings, including locked stores. Historical paid cost is informational. Refresh minimum prices after changing listings.
- Attendance is cached once per customer per day. `VisitingChef` entries bypass meal attendance and require a `ServiceEncounter` dialogue to appear.
- Finance changes by the recorded **health delta**, two days later by default. Do not assign finance from absolute health. Customer initialization must establish the health baseline.
- Dialogue flags/history change on the final Continue, never just because a line appeared. Failed-recipe reactions take priority over positive health/delight reactions.
- `awaitingDishAcknowledgement` is derived from `ServiceStep.DishResult`; do not introduce a second independently writable flag.
- Save/load is not implemented. Persisting only `GameSessionState` would not restore a mid-service encounter cursor or in-progress dialogue. Decide checkpoint boundaries before implementing persistence.

## Extend content without new service classes

Use stable, unique IDs. CSV column names and enum values are part of the authoring contract; renaming them requires migration. Reimport updates assets with the same IDs and preserves blank optional media references. Removing a row removes database membership but can leave its old asset on disk; don't infer active content from folder contents alone.

**Customer:** add a Customers row with unlock day, starting health, theme, and valid recipe preferences. Starting delight and finance come from BalanceConfig. `starting_prosperity` is legacy metadata. Add an introduction, nine health/delight greeting combinations, and after-meal reactions to the dialogue tables. Reuse the current band boundaries 0–34, 35–69, 70–100. Preferred recipes/cravings are currently descriptive metadata; they do not add rewards.

Marco (`U005`, day 2) and Priya (`U006`, day 3) expand the larger plan's stretch cast using the existing customer systems. Marco is a fisherman trying familiar plant-food meals; Priya is a new vegan looking beyond packaged-food marketing. Their dialogue is draft narrative rather than a full multi-chapter arc or medical claim.

**Recipe:** declare category slots with positive serving counts, unlock day, pay, and delight. Check that affordable ingredients exist in accessible stores on its unlock day. `R007` adds a bean/vegetable plate; `R008` adds a fruit/seed dessert bowl. The eight templates remain generic: foods actually selected determine classification and effects. Mixed colours remain playable under the user's chosen rules.

**Dialogue:** add a sequence row and one or more line rows keyed by sequence ID. `D_NAME_INTRO` is an identifier for a one-time introduction, not a random variation marker. On the first visit, a two-line greeting followed by a two-line introduction produces four lines. Later visits normally omit the completed introduction. Player-facing `display_name` is separate from the ID.

Use `CustomerArrival` for current-state greetings, `BeforeService` for introductions, `AfterService` for actual dish reactions, and `ServiceEncounter` for priority story visits. Other selector triggers exist but are not yet driven by this greybox loop. Story eligibility is sampled when service begins and checked again before playback; completing one story does not discover new stories for that same queue.

Line order is local to a sequence. `voice_over_path`, portrait, and claims may be blank. Keep existing line order stable if retaining Inspector-assigned voice/portrait references. After-service `servedMealDelight` is the actual applied delta; failed recipes must use `recipe_build_condition=Failed`, including when delight cannot fall further. Claims require their own approval work; this pass does not author new claims.

## Verification and limits

Run each in a fresh PowerShell process from the project:

```powershell
pwsh -NoProfile -File Tools/Test-GameOperations.ps1
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

- Operations checks use isolated fixtures and Unity stubs: request atomicity, preview/commit, missing/surplus ingredients, caps, minimum prices, expiry, attendance, delayed finance, phase order, chef priority, and dialogue completion.
- CSV checks build runtime content from the authored tables, prove all eight recipes can be bought/built on their unlock day, test 216 greeting boundary combinations and meal reactions, and simulate six days with a fixed attendance seed. They include operations checks automatically.
- Import checks run the actual dialogue importer with an in-memory AssetDatabase, exercising all 97 sequences plus unresolved customer references, undefined enums, and non-finite numeric parsing. They do not write Unity assets.
- Unity's validator checks the standard imported library, including IDs, food listing coverage, storage, quantities, references, recipe bounds, and dialogue lines/ranges. It runs after Import All and before a build. Draft approval and missing optional art/audio are permitted; this is structural validation, not release approval or a complete content linter.

The scripts are also compiled against the installed Unity assemblies during this cleanup. Automated checks do not replace Play Mode or a Web build. For a manual smoke test: buy food, preview and cancel a dish, serve a correct dish, serve a mismatch, skip a customer, finish days, verify the chef appears first on day 3, and finish day 6.

Remaining planned work: save/load checkpoints, ingredient inspector/sorting, discovery/membership UI, craving resolution, visual rush/crash, and final UI/art. Economy tuning remains provisional: the seeded informed-shopping run ends with a large surplus. Do not mistake one passing strategy simulation for complete difficulty validation. Scene objects and optional packed-sprite presentation still require visual testing when moving from this greybox to card UI.

## Cleanup decisions

Keep the existing architecture. A dependency-injection framework, generic event bus, new chapter subsystem, or interface for every data class would increase the solo project's learning/maintenance cost now. The small shared `RecipeRequirements` helper removes a real source of inconsistent rules; the editor validator and importer preflight make errors more visible. Each owned C# file has a responsibility comment with extension guidance. Keep future comments focused on ownership, invariants, and why a rule exists rather than restating each line of code.
