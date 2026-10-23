# Developer guide

Architecture, ownership, invariants and content authoring. Updated 2026-10-22 to match the code. For system details see the per-system guides indexed in `../AGENTS.md`; for open issues see `KnownIssues.md`.

The game is a six-day, data-driven loop. The solo GDD supplies the scope and category-slot approach; the instructor's capstone plan supplies the nutrition framework, character themes and design context. Later user decisions (flexible meals, delight, delayed finance, no memberships) take precedence over the older plan formulas.

## Open and run

1. Open the project in Unity 6000.4.1f1. Let scripts compile.
2. Run **Tools > Blue Zone Bistro > Import All CSV Content** after editing any CSV. It imports foods, stores, claims, prices, recipes, customers, dialogue, endings, events and tutorials in dependency order, wires loaded scenes, then validates the library.
3. **Tools > Blue Zone Bistro > Validate Imported Content** re-checks the imported assets without importing.
4. Open `Assets/Scenes/SampleScene.unity` with one active `GameBootstrap`. Missing default references are filled automatically by `GameBootstrapSetup`; explicit Inspector assignments are preserved. Save the scene after setup.
5. Press Play. Main menu → New Game / Resume → Market (city, stores, cart) → checkout → food sorting (skipped if nothing new) → service encounters → meal preview/confirm → reaction → daily results → next day's Market, until day 6 shows the ending.

Dialogue CSV changes are detected before Play and build; a failed dialogue import blocks Play with a Console error rather than using stale conversations. Other CSVs need the explicit import command. Editing a CSV alone does not update the imported `.asset` files.

## Folder map

| Folder | Contents |
| --- | --- |
| `Assets/Scripts/Core` | `GameBootstrap` (composition root, menu, checkpoints), `GameLoop` (phase/encounter flow), `GamePhase` |
| `Assets/Scripts/Services` | Purchase, sorting, meal, day, visit, event, ending, tutorial services; `GameContentQueries`; shared contracts |
| `Assets/Scripts/Runtime` | Mutable session data (`GameSessionState`, `CustomerState`, `InventoryEntry`, `EventRecord`), dialogue runtime, `Persistence/CheckpointStore` |
| `Assets/Scripts/Data` | ScriptableObject definitions and databases, enums, `BalanceConfig` |
| `Assets/Scripts/UI` | Canvas UI: `BistroUIScreen` (partial classes per area), cards, slots, widgets, motion, theme |
| `Assets/Editor` | CSV importers, validator, bootstrap setup, Web build, editor test windows |
| `Assets/GameData/CSV` | Authored content tables (source of truth) |
| `Assets/GameData/*` | Imported runtime assets, `BalanceConfig.asset` |
| `Tools/` | Isolated PowerShell/C# regression and smoke harnesses; `BalanceSim/` Python balance simulator |
| `Data/` | These guides |

## Responsibilities and ownership

| Area | Responsibility | Keep out |
| --- | --- | --- |
| `Core/GameBootstrap` | Construct and connect the shared objects; New Game / Resume / checkpoint capture | Gameplay formulas, per-frame loops |
| `Core/GameLoop` | Accept legal commands, consult tutorials, coordinate phases/encounters | Drawing widgets, importing files, meal scoring |
| `Services/PurchaseService`, `MealService`, `DayService` | Validate and commit operations | UI controls and screen strings |
| `Services/FoodSortingService` | Validate choices, compute accuracy, apply one refund, remember correct food IDs | UI-provided scores or payments |
| `Services/CustomerVisitService` | Cache one attendance plan and cravings per day | Dialogue priority and playback |
| `Services/CommunityProgress` | Validate tuning, popularity/wellbeing deltas, capacity | Customer stats |
| `Services/EventService`, `MarketEffects` | Morning event selection; effective prices, closures, publicity | Changing baseline content |
| `Services/EndingService` | Choose and persist the ending | Presentation |
| `Services/TutorialController`, `TutorialBehaviours`, `IntroMealPlan` | Lesson scheduling and permitted actions | Buttons, coordinates |
| `Services/EncounterScheduler` | Put eligible stories before attending customers; build dialogue context; choose rush/crash triggers | Mutating customer outcomes |
| `Runtime/Dialogue/DialogueService` | Select dialogue and apply completion effects | A second copy of flags/history |
| `Runtime/Dialogue/DialoguePlaybackController` | Track ordered lines, complete once | Buying, serving, advancing days |
| `Runtime/Persistence/CheckpointStore` | Versioned JSON + checksum + recovery copy in PlayerPrefs | Mid-day state |
| `Services/GameContentQueries` | Read catalogue/state for UI | Commands |
| `Services/RecipeRequirements` | Interpret slot minimum/maximum once | Inventory mutation |
| `Services/RecipeCheck` | Pantry feasibility hint | Reserving food |
| `Runtime/GameSessionState` | The run's mutable progress | Authored definitions |
| `Runtime/ServiceSessionState` | Encounter cursor, step, dish result, daily summary | Persistent customer state |
| `Data` | Authored definitions and ID lookup | Runtime pantry, money, flags |
| `UI/BistroUIScreen` (+ partials) | Listen to input, query data, render current state | Scoring or transition rules |
| `Editor` | Import, validate, wire references, build | Code needed in the Web player |

The UI calls GameLoop commands. GameLoop calls services. Services change the shared session and return results. The UI reads state/results and queries the catalogue to redraw. There is no blocking gameplay loop; Unity processes each click and returns to rendering. `BistroUIScreen` marks itself dirty after a command and rebuilds the current screen in `LateUpdate`.

`PhaseChanged` observers see a fully prepared service encounter. An empty queue goes straight to Results without a stale Service notification. Changes between customers or dialogue lines stay inside the Service phase; the UI refreshes after each command.

## Invariants to preserve

- Pass the same `GameSessionState` to every service. Dialogue uses its `flags` and `dialogueHistory`; a context is a snapshot, not a second progression store.
- Treat returned data assets and result objects as read-only in presentation code.
- A failed **request** has `success=false` and changes nothing. A served but mismatched **recipe** has `success=true`, `recipeBuilt=false`, consumes food, pays 75% of minimum ingredient value, gives no tip and applies the failure delight/popularity penalty.
- Preview and commit use the same meal evaluator. Commit validates again; never consume a previously computed preview.
- Inventory selections use batch IDs. Two purchases of the same food may differ in expiry or cost.
- Required slots set the minimum; required plus optional set the maximum. Extras and swapped slots fail the build. A recipe name is not an exact-ingredient rule.
- Minimum ingredient value comes from all valid listings, including locked stores, and ignores event prices.
- Attendance and cravings are cached once per day in the session. Story encounters (`ServiceEncounter` dialogue) bypass attendance.
- Finance changes by the recorded **health delta**, two days later by default. Never assign finance from absolute health.
- Dialogue flags/history change on the final Continue, never when a line appears. Failed-recipe reactions take priority over positive reactions.
- `awaitingDishAcknowledgement` is derived from `ServiceStep.DishResult`; do not add a second writable flag.
- Checkpoints are day-start only. Cart, selections, active-dialogue cursors and receipts are not saved. Do not add mid-day saving without designing encounter-cursor persistence first (see `Checkpoints.md`).
- Delight never adds health.

## Extend content without new service classes

Use stable, unique IDs. CSV column names and enum values are part of the authoring contract; renaming them needs a migration. Reimport updates assets with the same IDs and preserves blank optional media references. Removing a row removes database membership but can leave the old asset on disk; do not infer active content from folder contents.

**Food:** add a Foods row (category, Daily Dozen tags, Traffic Light, label, ingredient list, storage, appeal 0–10) and at least one Prices row (store, shelf ID that exists in the store's `shelf_definitions`, unlock day, servings, game price). Classification must come from the approved table.

**Store / shelf:** add a Stores row with `shelf_definitions` (`ID:Display name|…`). Leave `requires_membership=false` (memberships are cut). Shelf IDs are what 2D hotspots or 3D colliders should reference.

**Customer:** add a Customers row with unlock day, starting health, theme, preferred recipes and craving pool. Starting delight and finance come from BalanceConfig; `starting_prosperity` is legacy metadata. An optional `unlock_popularity` column sets the popularity needed before the customer first appears (0 = none; U005 = 500, U006 = 750). Add an introduction, the nine health × delight greetings (bands 0–34, 35–69, 70–100), after-meal reactions, and one `RushAfterService` and one `CrashArrival` sequence. Preferred recipes are descriptive; cravings drive rewards from day 4.

**Recipe:** declare required/optional category slots with positive servings, unlock day, tier, pay, delight, craving tags and `allow_non_green_choices`. Check that its required categories are buyable in accessible stores on its unlock day (the CSV checks verify this). There are 11 recipes; R003 and R006 are the Okinawa-inspired set (day 4).

**Dialogue:** add a sequence row and line rows keyed by sequence ID. `CustomerArrival` = greeting, `BeforeService` = introduction, `AfterService` = dish reaction, `ServiceEncounter` = story visit (e.g. the chef), `RushAfterService` / `CrashArrival` = rush/crash, `Ending` = ending playback. `DayStart`/`DayEnd` exist in the selector but are not scheduled. `voice_over_path`, portrait and claims may be blank. Failed recipes must use `recipe_build_condition=Failed`.

**Claim:** add a Claims row with text, source title/citation/URL and approval status. Only supervisor-approved claims should appear as health statements; current rows are placeholders and game-rule explanations.

**Event / ending / tutorial:** see `Events.md`, `Endings.md`, `Tutorials.md`.

## Verification and limits

```powershell
pwsh -NoProfile -File Tools/Test-GameOperations.ps1
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

- Operations checks: request atomicity, preview/commit, missing/surplus ingredients, caps, minimum prices, expiry, attendance, delayed finance, phase order, chef priority, dialogue completion, events, meal slots, shopping, progress, tutorials.
- CSV checks build runtime content from the authored tables, prove each recipe can be bought/built on its unlock day, test greeting boundaries and reactions, and simulate six days with a fixed attendance seed. They include the operations checks.
- Import checks run the real importers against an in-memory AssetDatabase and reject malformed definitions without writing assets.
- The Unity validator checks IDs, listing coverage, storage, quantities, references, recipe bounds and dialogue structure after Import All and before builds. It is structural validation, not content approval. It does not yet enforce the plan's nutrition rules (see `KnownIssues.md`).
- Editor-only smoke tests (`CheckpointSmokeTest`, `EndingChecks`, `EventImportChecks`, `Tools/UnityUIInteractionSmoke.cs`, `Tools/EventUISmoke.cs`) are run in an isolated project copy.

- `Tools/BalanceSim` replays hundreds of six-day runs with four bot play styles from the real CSVs and `BalanceConfig.asset` (see its README). Use it after any balance or price change.

Automated checks do not replace Play Mode or a Web build. Manual smoke test: New Game, buy food, sort, preview and cancel a dish, serve a correct dish, serve a mismatch, skip a customer, finish days, confirm the chef appears first on day 4 together with MegaBulk and the Okinawa recipes, resume from a checkpoint, finish day 6 and view the ending.

## Cleanup decisions

Keep the existing architecture. A dependency-injection framework, generic event bus, chapter subsystem or interface for every data class would raise the solo project's maintenance cost. Each owned C# file has a responsibility comment; keep comments focused on ownership, invariants and why a rule exists.
