# Basic game operations

`Core/GameLoop` coordinates Market -> Service -> Results -> Market (or Completed).
`Services` holds purchase, meal, and day operations behind interfaces. `Runtime` holds
mutable session, customer, and inventory records. `Data` holds authored definitions.
The existing name `GameSessionState` is retained rather than adding a competing `GameState`.

Add `GameBootstrap` to one scene object. The editor automatically fills missing
database references from the CSV import locations and creates/assigns
`Assets/GameData/BalanceConfig.asset` if needed. Existing assignments and balance
tuning are preserved. Missing databases require **Tools > Blue Zone Bistro >
Import All CSV Content**; they are not replaced with empty databases. Save the scene
after automatic setup. Build processing also resolves references in unopened scenes
and rejects missing dependencies. Bootstrap creates one session and passes that same reference to
all services. UI code should call `bootstrap.Loop`, whose phase guards enforce order.
No per-frame Update loop is needed for these button-driven commands.

## Playing the greybox

Press Play with the configured GameBootstrap scene object active, then open the Game
view. Bootstrap automatically adds GreyboxUIController at runtime. It uses Unity's
built-in immediate-mode text/buttons, so no Canvas, fonts, or button bindings need
manual setup. This is temporary desktop greybox presentation, not the final card UI.

Market: choose a store, buy packages, inspect the pantry, go back to stores, and
Continue to service. Service shows one queued encounter at a time. Eligible story
encounters run first in descending priority, then customers chosen by attendance.
Customer arrival dialogue and first-visit introductions precede meal selection. All unlocked
recipes are offered, labelled Can make or Substitution needed. Choose a recipe,
adjust batch serving counts with +/- buttons, and Submit to preview the dish.
The confirmation shows recipe success, missing/surplus categories, payment, and effects.
Back to reconsider retains selections without consuming anything. Confirm and serve
revalidates inventory and applies the result. Invalid requests keep selections.
Served dishes show their effects; Continue plays any after-service reaction and then
advances the queue. Skip is available
before submission and bypasses meal-reaction dialogue. Dialogue must finish before
underlying service actions are available. Back returns to recipe selection.

Each unlocked customer rolls attendance once per day with probability delight/maxDelight
(initially 50/100). The roll is stored on CustomerState, so queries do not reroll it.
A customer cannot attend twice that day. BeginService builds the encounter queue only
once; empty queues immediately show Results. Story visitors do not roll attendance.
No early ShowResults bypass is allowed:
serve or skip remaining customers. Daily Results records successful purchases/dishes
and skipped customers. Finish day ages stock and opens the next market; the final day
shows completion. Successful recipes earn authored base pay plus a finance/100 tip
(finance starts at 20, so the initial tip is 20%). Mismatched recipes earn 75% of
minimum ingredient value and no tip, and lose 30 delight before clamping to 0.
Delight ranges from 0 to 100. Correct dishes gain recipe delight plus the floor of
minimum ingredient value times delightPerIngredientDollar (default 1), capped at +20.
Positive health change is also capped at +20; health remains 0–100 and negative
health effects are not limited by the positive-gain cap. Mismatched dishes still
apply food-based health effects and do not resolve cravings.

FoodDatabase stores a serialized minimum price-per-serving table: the smallest valid
gamePrice/servingsPerPackage across ALL stores, even locked ones. Food and price CSV
imports rebuild it, and Bootstrap refreshes it from assigned listings when play starts.
MealService reads this directly; GameContentQueries exposes it for presentation.
Actual historical purchase cost remains informational and never increases delight or
mismatch payment. Missing price data produces an explicit import error rather than
silently assuming a value. Free valid listings can establish a zero minimum value.

Finance starts at BalanceConfig.startingFinance (20), separate from the legacy
CustomerData.startingProsperity field. At the end of each day, DayService snapshots
health and its daily change for all unlocked customers, including absent/skipped ones.
Day 3 finance adds day 1's health change; day 4 adds day 2's change. For example,
health 50 -> 54 on day 1 raises finance 20 -> 24 on day 3. Unchanged health adds
zero, and health losses reduce finance after the same delay. The initial baseline is
the customer's authored starting health. Finance is clamped to 0–100. The final
day does not advance finance because there is no following market. These runtime
histories and attendance decisions are serializable, but save/load is still future work.

GameContentQueries reads databases/state; RecipeCheck aggregates usable batches by
category, sums repeated required slots, excludes expired stock, and enforces recipe
Traffic Light restrictions. Optional slots need not be filled. GameLoop owns the
ServiceSessionState encounter queue and DaySummary, while UI owns temporary selections only.

## Dialogue and encounter authoring

Run **Tools > Blue Zone Bistro > Import All CSV Content** after changing the CSVs,
or **Import Dialogue CSV** for dialogue-only changes. GameBootstrapSetup automatically
assigns DialogueDatabase alongside the other databases. Bootstrap constructs the
existing DialogueService with the session's flags/history through EncounterScheduler.
Do not attach DialogueRuntimeExample for gameplay; it remains a standalone example.
DialogueDatabase also records an import fingerprint. Editor setup detects changed CSVs
or legacy assets with no fingerprint and reimports dialogue automatically before play
and builds. A rejected automatic import blocks entering play/building with stale dialogue;
fix the Console errors and use Import Dialogue CSV to retry. This synchronization is
editor-only; player builds still use the imported assets and do not read CSVs at runtime.

Sequence and line CSVs remain separate authoring tables. Import groups line rows by
sequence_id into ordered DialogueSequenceData.lines. Optional columns:
`recipe_build_condition` is Any/Built/Failed, `meal_health_condition` is
Any/Gain/Loss/Unchanged, and `voice_over_path` is an optional
Unity AudioClip asset path. Blank/missing voice columns are silent and preserve an
existing manually assigned clip. No audio asset is required. Portraits remain optional.

Use the ServiceEncounter trigger for standalone story sequences such as the chef.
Their day/flag/history conditions determine eligibility at queue construction; priority
orders them ahead of regular customers. Eligibility is checked again before playback.
Newly unlocked story events not eligible at construction are considered next day.
The chef event runs from day 3 onward, after Market, until completed, rather than only in a fragile
one-day window. Its completion sets okinawa_chef_arrived; current regional content
still retains its existing day-based availability.

Customer checkpoints select one eligible sequence each: CustomerArrival, BeforeService,
and AfterService. Highest-priority mandatory wins; otherwise weighted ambient is used.
BeforeService selection happens after arrival completion, so it sees new flags. Each
checkpoint runs only once per encounter; repeatable ambient lines cannot create a loop.
Other legacy triggers (DayStart/DayEnd) remain available in the selector but are not
scheduled by this service-only integration.

GameLoop owns legal progression: StoryDialogue -> next encounter, or ArrivalDialogue
-> BeforeDialogue -> MealSelection -> DishResult -> AfterDialogue -> next encounter.
DialoguePlaybackController owns only sequence/line position. Next on the final line
applies completion flags and history once. UI only renders the speaker, optional portrait,
text and Next/Continue; optional voice playback stops when the line changes. No sequence
or no usable lines skips that dialogue checkpoint. Flags are session-wide; showing a
line or previewing a dish does not complete dialogue or set flags.

97 sequences and 189 lines cover June, Dev, Rosa, Sam, Marco, Priya and the visiting chef. Narrative
remains Pending review; no new clinical claims or claim-register entries were added.
Greetings select one of nine combinations per customer: current health low/mid/high
crossed with current delight low/mid/high (0–34, 35–69, 70–100). AfterService uses actual
dish health change, delight change, and explicit recipe-built success, not ingredient
effects alone. Failed recipes take priority over all positive reactions, including when
delight is already zero or health improves. Correct recipes with health loss select a
concerned reaction; other built recipes use the successful delight-gain bands (0–7 or
8–20), with premium/luxury reactions below the health-loss priority. Customer health bands describe current state, not a promised medical
outcome. Legacy basic dialogue IDs remain as ambient fallbacks.

```csharp
PurchaseResult purchase = bootstrap.Loop.Buy("listing_id");
bootstrap.Loop.BeginService();
// UI handles these one click at a time; a headless example can advance opening lines.
while (bootstrap.Loop.Dialogue != null && bootstrap.Loop.Dialogue.IsActive)
    bootstrap.Loop.AdvanceDialogue();
ServeResult served = bootstrap.Loop.Serve(new ServeInput
{
    customerId = bootstrap.Loop.Service.CurrentCustomerId,
    recipeId = "recipe_id",
    ingredients = new List<IngredientSelection>
    {
        new IngredientSelection
        {
            inventoryEntryId = purchase.inventoryEntryId,
            servingsUsed = 2
        }
    }
});
if (served.success) bootstrap.Loop.ContinueAfterDish();
// Repeat for remaining customers, or use SkipCustomer().
if (bootstrap.Loop.Phase == GamePhase.Results)
    bootstrap.Loop.FinishDay();
```

Use actual database IDs and check `success`/`error` before proceeding. Correct builds
must match category bounds; mismatches remain servable with penalties. Service can contain multiple
customers; each is served at most once per day. Ending service with no meals is allowed.

Purchasing buys one package, reading price and servings from StoreListingData and
storage type from its FoodData reference. Each purchase gets a unique batch ID.
Membership stores require their store ID in `state.storeMembershipIds`; listing
unlock flags use `state.flags`. Membership purchase UI is not implemented.

Serving accepts batch IDs and quantities, not mutable InventoryEntry references.
Repeated selections of the same batch are combined before stock validation.
Required slots must be filled for recipeBuilt=true; optional slots may be partially
filled. Extras beyond category capacity or forbidden Traffic Light choices set
recipeBuilt=false but are still servable after the UI confirmation. Empty, expired,
unknown, or overdrawn inventory requests remain invalid. Food selection determines
Traffic Light counts and Daily Dozen coverage. Recipe baseSalePrice supplies earnings
and delightValue supplies delight for correctly built recipes. Operation success is
separate from recipeBuilt. Invalid requests return an error without changing state.

Provisional health formula: green servings times green weight, plus unique Daily
Dozen categories from green foods times coverage weight, minus yellow and red
serving penalties. All weights are on BalanceConfig. Health is clamped to 0–100;
healthDelta reports the actual applied change. Coverage output includes all selected
foods, but non-green foods do not earn coverage bonuses. Delight does not add health.
Any red serving sets rushCrashTriggered; timed animation and crash scheduling are
not implemented. cravingsResolved is reserved and currently empty.

Fresh shelf life defaults to 2 game days, frozen to 5, and canned/dry/shelf-stable
to -1 (no expiry). These are prototype game rules, not food safety guidance.
FinishDay decrements positive shelf life before advancing the day, removes expired
batches, and clears current selections. Finishing the final day completes the loop
without opening another day. The default run is six days.

The GDD's mixed-food category-slot recipes are followed; the broader plan's green-only
restriction remains available per recipe through allowNonGreenChoices. No approved
content assets are changed. Numerical scoring is provisional because the plans give
inputs rather than an exact equation.

Polished UI, save/load, customer animation, cravings and broader chapter events remain
future work. Editor setup can fill scene references; gameplay UI is added only at runtime.

Run `pwsh -File Tools/Test-GameOperations.ps1` for isolated operation checks using
Unity stubs. These checks do not replace a Unity Play Mode or Web build test.

See [DeveloperGuide.md](DeveloperGuide.md) for the architecture map, authoring workflow,
structural validator, importer regression checks, and remaining Week 3 limitations.
