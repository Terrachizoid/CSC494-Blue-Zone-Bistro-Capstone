# Basic game operations

`Core/GameLoop` coordinates Market -> FoodSorting -> Service -> Results -> Market (or Completed).
FoodSorting is skipped for empty or entirely mastered-food carts.

## Cart and sorting

The UI holds uncommitted `CartEntry` quantities by listing ID. `QuoteCart` is read-only;
`Checkout` validates the entire cart (including full-price affordability) before charging
or creating inventory. Each package gets a separate batch ID plus its source listing ID.
The bootstrap enables this flow; legacy `Buy` remains available to isolated service tests,
but GameLoop rejects it when sorting is enabled.

Several checkouts per day (2026-11-13, Owen). Each checkout gets its own `ShoppingReceipt`; if it
has new foods the loop enters FoodSorting for that purchase only, and `ReturnToMarket` (the sorting
result's "Back to the market") returns to the store the player checked out from. A checkout of known
foods only stays at the market. A new checkout is refused while the previous one is unsorted (the
phase makes this unreachable from the UI). `GameLoop.EnterBistro` (the city map's Bistro click, after
a confirmation) is the only way out of the market and starts service, also with no purchase at all.
With `BalanceConfig.cityDuringService` on (easy-mode switch), `LeaveBistro` returns to the market mid-service
and `EnterBistro` resumes the same queue (`ResumeService`): a guest at meal selection waits, an unfinished
dialogue restarts without its effects (`DialoguePlaybackController.Cancel`), and a guest already served is
done. Checkpoints still save only at day end, so leaving never writes a mid-day save.

Checkout snapshots distinct food questions and the configured maximum discount in the
session's `ShoppingReceipt`. `correctlySortedFoodIds` excludes previously correct foods.
Wrong answers remain eligible on later purchases. UI submits only `SortingChoice` records;
FoodSortingService returns feedback and applies, exactly once, `maximumSortingDiscount` × what
this checkout paid for each correctly sorted new food (`SortingQuestion.cost`, all offers of that food;
each share rounded to cents). Known foods and wrong answers refund nothing (2026-10-28, Owen; was
30% of the whole cart × accuracy). Default discount is 0.50. Empty question sets grant no refund.
Only the refunded foods' batches record the lower unit cost; daily spending reflects the refund;
minimum catalogue prices used for meal scoring are unchanged. Sorting feedback remains visible
until Continue. No reattempt of that sorting round is possible; a food answered wrong comes back on the next purchase, even the same day.

Codex is available from market, cart, sorting and pantry cards. Pantry inspection is available
in all phases; serving still selects by inventory batch ID. Optional FoodData.sortingExplanation
can be authored in the Inspector or imported via the optional `sorting_explanation` CSV column.
Omitting the column preserves authored text. Blank explanations explicitly show a pending
placeholder; detailed research and final artwork are future content work.
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

## Playing the game

Press Play with the configured GameBootstrap scene object active. Bootstrap adds
`BistroUIScreen`, which builds the Canvas UI at runtime (see [UI framework](UIFramework.md)).
The old IMGUI `GreyboxUIController` was removed from startup on 2026-10-22 and its file emptied on 2026-10-28.

Market: pan the city map, enter stores and shelves, drag packages into the cart,
inspect the pantry, review the cart and check out (each purchase is sorted right after, as often as
needed), then click the Bistro on the map and confirm to start service.
Service shows one queued encounter at a time. Eligible story
encounters run first in descending priority, then customers chosen by attendance.
Customer arrival dialogue and first-visit introductions precede meal selection. All unlocked
recipes are offered. Choose a recipe, drag pantry food cards into its slots (one serving
per drop), and Submit to preview the dish.
The confirmation shows recipe success, missing/surplus categories, payment, and effects.
Back to reconsider retains selections without consuming anything. Confirm and serve
revalidates inventory and applies the result. Invalid requests keep selections.
Served dishes show their effects; Continue plays any after-service reaction and then
advances the queue. Skip is available
before submission and bypasses meal-reaction dialogue. Dialogue must finish before
underlying service actions are available. Back returns to recipe selection.

Morning popularity caps attendance; personal delight weights selection without replacement.
The morning plan is saved, so queries and resume do not reroll it. At least one eligible customer attends.
A customer cannot attend twice that day. BeginService builds the encounter queue only
once; empty queues immediately show Results. Story visitors do not roll attendance.
No early ShowResults bypass is allowed:
serve or skip remaining customers. Daily Results records successful purchases/dishes
and skipped customers. Finish day ages stock and opens the next market; the final day
shows completion. Successful recipes earn authored base pay (4–5.5 after the 2026-10-22 tuning) plus a finance/100 tip
(finance starts at 20, so the initial tip is 20%). Starting money is 15. Mismatched recipes earn 75% of
minimum ingredient value and no tip, and lose 30 delight before clamping to 0.
Delight ranges from 0 to 100. Correct dishes gain serving-weighted ingredient appeal,
recipe delight and resolved-craving bonuses, floored and capped at +20.
See [Popularity and cravings](PopularityAndCravings.md) for global effects and configuration.
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
histories and attendance decisions are persisted in day-start checkpoints.

GameContentQueries reads databases/state; RecipeCheck aggregates usable batches by
category, sums repeated required slots, excludes expired stock, and enforces recipe
Traffic Light restrictions. Optional slots need not be filled. GameLoop owns the
ServiceSessionState encounter queue and DaySummary, while UI owns temporary selections only.

## Dialogue and encounter authoring

Run **Tools > Blue Zone Bistro > Import All CSV Content** after changing the CSVs,
or **Import Dialogue CSV** for dialogue-only changes. GameBootstrapSetup automatically
assigns DialogueDatabase alongside the other databases. Bootstrap constructs the
existing DialogueService with the session's flags/history through EncounterScheduler.
`DialogueRuntimeExample` and `GameProgressState` were unused and were emptied on 2026-10-28.
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
The chef event runs from day 4 onward (the start of Chapter 2, when MegaBulk and the
Okinawa-inspired recipes unlock), after Market, until completed. Its completion sets
okinawa_chef_arrived; regional content itself is still unlocked by day, not by that flag.

Customer checkpoints select one eligible sequence each: CustomerArrival, BeforeService,
and AfterService. Highest-priority mandatory wins; otherwise weighted ambient is used.
Mandatory sequences that tie on the top priority are variants of one moment (2026-11-22): one is
picked by `random_weight`, skipping the one played most recently. The AfterService dialogue is
picked when the dish is served, so the guest's face can show its first emotion with the dish result.
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

113 sequences and 213 lines (as of 2026-10-22) cover June, Dev, Rosa, Sam, Marco, Priya, the visiting chef, rush/crash conversations and the four endings. Narrative
remains Pending review; no new clinical claims or claim-register entries were added.
Greetings select one of nine combinations per customer: current health low/mid/high
crossed with current delight low/mid/high (0–34, 35–69, 70–100). AfterService uses actual
dish health change, delight change, and explicit recipe-built success, not ingredient
effects alone. Failed recipes take priority over all positive reactions, including when
delight is already zero or health improves. Correct recipes with health loss select a
concerned reaction; other built recipes use the successful delight-gain bands (0–7 or
8–20), with premium/luxury reactions below the health-loss priority. Customer health bands describe current state, not a promised medical
outcome. Legacy basic dialogue IDs remain as ambient fallbacks.

Closing reactions (2026-11-22). Each guest's AfterService dialogue names the one main reason the dish
went the way it did, and dialogues with something to improve end with one `Blue Zone Guide` line giving a
tip (speaker id `Blue Zone Guide`; the guest stays on stage and keeps their face). Every condition is read
from the served dish's own result, so nothing is fetched from elsewhere. Guests are not critics of
everything: rather than one dialogue per combination of conditions, the **priority ladder** decides which
single reason wins, and each dialogue talks only about its own reason.

It works like a decision tree: the game walks down the ladder and the first rung whose conditions are all true is
the leaf that plays (the guest's lines, then the Guide's tip). Dialogues at the same priority are either siblings
whose conditions can never both be true (great vs okay) or variants marked with `variant_of`, which alternate.

| Priority | Sequence suffix | Condition columns | Guest reacts to | Guide tip |
|---|---|---|---|---|
| 200 | `_AFTER_POOR` | `recipe_build_condition` Failed | plate didn't match | fill every slot; check Review |
| 196 | `_RUSH_HEALTH_LOSS` | `red_served` Yes, `meal_health_condition` Loss | a quick lift, then "not good for me" | lift fades tomorrow; dish also cost health |
| 195 | `_RUSH` | `red_served` Yes | a quick lift | lift fades tomorrow; they may arrive tired (crash) |
| 190 | `_AFTER_HEALTH_LOSS` | `meal_health_condition` Loss, `red_served` No | health went down (yellow-heavy) | green foods in those slots |
| 115 | `_AFTER_FANCY_REQ_MISSED` | tiers Premium/Luxury, `request_condition` Missed | pricier dish, request missed | price doesn't replace a request |
| 110 | `_AFTER_FANCY` | tiers Premium/Luxury | pricier dish | none |
| 100 | `_AFTER_FATIGUED` | `recipe_fatigued` Yes | served too often lately | rotate recipes |
| 97 | `_AFTER_COMFORT_MISSED` | `missed_request` Comfort | Comfort request missed (Dev, Sam, Marco) | Comfort needs appeal {appealTarget}+ |
| 95 | `_AFTER_HIGH_REQ_*`, `_AFTER_OKAY_REQ_*` | delight band + `request_condition` All/Some/None | dish and requests together | Some/None: the missed request, coloured rims |
| 93 | `_AFTER_LOW_APPEAL` | `max_ingredient_appeal` 4.5 | a bit plain | appeal on cards; green foods rated 7+ |
| 92 | `_AFTER_DELIGHT_FULL` | `delight_full` Yes | already a fan | delight is full; requests still add popularity |
| 90 | `_AFTER_HIGH`, `_AFTER_OKAY` (+ `_V2` with `variant_of`) | delight band 8+ / 0–7 | the dish | none |

Rush and health loss are different events (rush = any red food; health = +1 per green serving and Daily Dozen bonus,
-1 per yellow, -3 per red), so the three rungs above never make each other obsolete. The rush used to be a separate
trigger (`RushAfterService`) that replaced the whole ladder, so a mismatched red plate got a cheerful line and no tip;
since 2026-11-22 it is a rung. The crash is still its own greeting at the guest's next arrival (`CrashArrival`).
No recipe is Premium/Luxury yet, so the fancy rungs cannot play until one exists.

Proof: `Tools/DialogueOutcomeChecks.cs` (run by `Test-CsvContent.ps1`) tries every guest x every combination of plate
matched, health change, red food, fancy, tired recipe, requests, appeal, delight full and dish delight (15,552
situations), fails on any situation with no winner or an unplanned tie, and writes the full table to
`Data/DialogueOutcomes.csv` (filter it in a spreadsheet to see what plays when).

Columns (all optional; blank = not checked): `request_condition` (All, Some, None need at least one
request; NoRequest; Missed = Some or None; a plate that didn't build its recipe meets none), `missed_request`
(tags, `|` separated; matches when any of them was asked for and missed), `recipe_fatigued`, `delight_full` and
`red_served` (Yes/No), `variant_of` (the original dialogue this one alternates with), `min_ingredient_appeal` / `max_ingredient_appeal` (the plate's average ingredient
appeal, inclusive, 0-10; each dialogue picks its own cutoff). General low appeal uses its own lower cutoff (4.5), while
a Comfort request that fails because appeal was under `BalanceConfig.comfortMinimumAppeal` (7) is its own, higher
reaction (`missed_request` Comfort, 97): the guest's expectation, not plain taste. `{appealTarget}` is that Comfort target.
Mandatory dialogues that tie on the top priority are variants: one is picked by `random_weight`, never the
one played most recently. Tokens for these lines: `{metRequest}`, `{missedRequest}` (tag names joined with
"and", as on the guest card), `{requestsMet}`, `{requestCount}`, `{ingredientAppeal}`, `{appealTarget}`,
`{recipeRecentServes}`, plus `{servedRecipe}` and `{customerName}`.

Importer note: the dialogue import is skipped while its fingerprint (CSV text plus a schema tag in
`DialogueCsvImporter.SourceFingerprint`) is unchanged. Bump the tag whenever a column is added or parsed
differently; otherwise an import made by older code (before Unity recompiled) stays "current" with the new
conditions missing. This happened on 2026-11-22: `_AFTER_FATIGUED` lost `recipe_fatigued` and fired on every dish.

Adding a guest: copy one guest's `_AFTER_*` rows (both CSVs), change `customer_id`, `min_day` and the
guest's own lines, keep the priorities and the Guide lines. Adding a new reason: give it its own
condition column and a priority slot in this table, write one dialogue per guest, and add it here.
195 sequences, 454 lines (2026-11-22).

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
Listing unlock flags use `state.flags`. Store memberships were cut (user decision, 2026-10-22)
and their code and CSV columns removed on 2026-10-28: MegaBulk simply opens by `unlock_day` 4.

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
Any red serving sets rushCrashTriggered, which drives the visual-only rush/crash state and
its dialogue (see [Checkpoints](Checkpoints.md)); no extra health penalty is applied.
cravingsResolved lists active cravings matched by a successfully built recipe (day 4+;
see [Popularity and cravings](PopularityAndCravings.md)).

Fresh shelf life defaults to 2 game days, frozen to 5, and canned/dry/shelf-stable
to -1 (no expiry). These are prototype game rules, not food safety guidance.
FinishDay decrements positive shelf life before advancing the day, removes expired
batches, and clears current selections. Finishing the final day completes the loop
without opening another day. The default run is six days.

The GDD's mixed-food category-slot recipes are followed; the broader plan's green-only
restriction remains available per recipe through allowNonGreenChoices. No approved
content assets are changed. Numerical scoring is provisional because the plans give
inputs rather than an exact equation.

Save/load, cravings, events and endings are implemented in their own guides. Remaining
work (final art, customer visuals, audio, balance, claims) is tracked in [KnownIssues](KnownIssues.md).
Editor setup can fill scene references; gameplay UI is added only at runtime.

Run `pwsh -File Tools/Test-GameOperations.ps1` for isolated operation checks using
Unity stubs. These checks do not replace a Unity Play Mode or Web build test.

See [DeveloperGuide.md](DeveloperGuide.md) for the architecture map, authoring workflow,
structural validator and importer regression checks.
