# Morning events

Events begin on day 2. Each morning has a 25% chance of zero new events, 50% of one, and 25% of two. These probabilities and the starting day are in BalanceConfig. Existing multi-day events remain active, so there can be more than two active events. Ineligible/conflicting candidates can reduce the number selected.

## Authoring

`Blue_Zone_Bistro_Events.csv` holds identity, headline, summary, detail text, day eligibility, duration, selection weight, optional image and notification/detail audio paths, and `|`-separated codex ClaimData IDs. `max_day=-1` means no latest starting day. Blank presentation paths preserve manually assigned assets; no audio assignment means silence.

`Blue_Zone_Bistro_EventEffects.csv` links any number of effects to each event ID:

| effect_type | target_type | target_id | operation | value |
|---|---|---|---|---|
| Price | Store or Food | Store/food ID | Add or Multiply | Signed amount or nonnegative multiplier |
| Availability | Store | Store ID | Close | 0 |
| AttendancePopularity | Bistro | Blank | Add | Signed publicity amount |
| Price | Category | Food category (e.g. Fruit) | Add or Multiply | as above, for every listing of that food type (2026-11-19) |
| Availability | Food | Food ID | Close | 0 = that food is sold out everywhere |
| Availability | Shelf | storeId/shelfId (e.g. S002/BACK_FREEZER) | Close | 0 = that aisle is sold out |
| Craving | Craving | Craving tag (e.g. Sweet) | Multiply | Weight when guests' requests are chosen (2 = twice as likely; guests still only ask for tags they like) |
| SortingRefund | Bistro | Blank | Multiply | Sorting refund rate for purchases made that day (snapshotted into the receipt, capped at 100%) |
| ShelfLife | Food or Category | ID or category | Add | Whole days added to food bought that day (never below 1; long-life foods unaffected) |

Food targets match every listing referring to that food. Existing inventory, recipe appeal and health classification do not change. A new combination needs only rows; a new kind of effect needs a typed evaluator and validation. Unknown enum values, invalid references and non-finite values reject the entire event import before replacing the database. Import claims before events using **Import All CSV Content**.

Five examples: CheapCart sale, fictional oat-demand price rise, Verdant maintenance closure, CheapCart restocking closure (added 2026-10-24), and bistro publicity (+200 attendance popularity since 2026-10-24, was +500) paired with a Verdant promotion. Their codex entries explain game rules, not medical claims. Optional source buttons accept HTTP(S) links only. Each event now owns an individual transparent editorial illustration under Assets/Art/Events, linked by image_path.

## Runtime ownership

- EventService prepares each morning once, before reservations and the checkpoint. It samples eligible events without replacement, excludes already-active events, and snapshots effect values in EventRecord. Reimporting content does not change a saved event's effects.
- EventRecord stores start day and exclusive expiry date. Starting day 2 for three days means active on 2/3/4, expired on 5. The archive retains expired events; evaluators ignore them. No asset mutation or subtract-to-undo operation occurs.
- MarketEffects computes prices, closure reasons and publicity. Prices use `max(0, baseline + sum(additions)) * product(multipliers)`, rounded once to cents away from zero at the midpoint. Invalid/overflowed prices fail checkout.
- PurchaseService and UI use the same effective prices. Inventory batches record actual paid cost. Minimum-price failed-dish valuation remains based on original authored listing prices. Sorting refunds remain based on the actual checkout receipt.
- Temporary publicity affects attendance capacity only. Earned popularity, permanent customer unlock requirements and ending selection remain unchanged. UI shows earned popularity plus the temporary bonus separately.
- Closure eligibility prevents all accessible stores from closing and rejects simultaneous store-targeted pricing/closure conflicts. Purchases are rejected by the service even if a cart already contains those listings. Pantry food remains usable. Craving feasibility excludes closed stores.

Day-start checkpoints include the plan and effect snapshots. Repeated queries/resume do not reroll. Old saves with an already-prepared reservation list get events from the following day, avoiding retroactive closures after requests were chosen. New Game resets the archive.

## Presentation

On the city map, new-day announcements appear above the signal tower, remain for six seconds, and advance automatically. They do not change phase, freeze gameplay or require acknowledgment. Notifications hide while browsing a store/modal, then continue on return. They can replay after resuming the day; this changes no gameplay state.

News remains available in the header. Its archive includes active and expired events. Click a notice or archive entry for larger artwork, details, effect list, duration, optional audio, and codex buttons. An assigned source URL opens only when the player clicks Open source. Closed stores remain visible but cannot be entered. Price cards show original → effective prices.

## Validation

`Tools/EventChecks.cs` runs with `Tools/Test-GameOperations.ps1` and the CSV regression suite. `Assets/Editor/EventImportChecks.cs` is an isolated-project import/save test that intentionally submits invalid data. `Tools/EventUISmoke.cs` is copied into an isolated project's Editor directory for actual Play Mode UI checks and screenshots; neither test writes the player's checkpoint.

Balance values are prototypes. No nutrition changes, secret-store unlock rules, food bans or real medical news are implemented.

Tower news update: the city NEWS hotspot opens the archive; notifications use a tower-anchored speech bubble with existing click-to-detail and six-second timing. Missing images remain optional placeholders. Event scheduling/effects are unchanged. Isolated injected UI checks passed; manual Play Mode/browser checking remains open.

## Newspaper presentation columns (2026-10-30)

`Events.csv` also has `thumbnail_path` (small art for the toast and the news list; falls back to `image_path`), `paper_color` and `accent_color` (hex, e.g. `#EAE0CB`; defaults `#E9E2D0` / `#A6B7A0`). `image_path` is the large picture in the opened article. All are CSV-owned; no JSON or ID lookup. Pending: the referenced `Assets/Art/Events/EV_*_icon.png` files do not exist yet, so import warns and keeps the previous placeholder sprites (old `Assets/Art/Endings/*_N.png`). Delete those five old placeholders only after the news art lands and has been imported.

### Newspaper presentation (2026-10-30)
Events use a compact headline + icon tower card, and a layered newspaper over a 65%-black scene dimmer when opened. No event or Codex dialogue panel is used. Layers: paper colour, flat accent field, transparent illustration, live headline/summary/body, subtle cached print-grain overlay, navigation. Body text uses dark ink and starts at the top; longer articles scroll. Archive entries share the thumbnail treatment. Close returns to the scene, All news to the archive, Codex to authored claims; source links still require HTTP(S) and an explicit click.

New optional CSV columns: `thumbnail_path`, `paper_color`, `accent_color` (HTML hex). `image_path` remains the expanded illustration. Both image columns currently point to the same simple icon per event; a separate compact illustration can be assigned later. Blank paths preserve Inspector references; missing thumbnail falls back to image. Missing/invalid colour strings use safe UI defaults. Text, effects and art are not baked together and there are no event-ID art lookups in UI code. Scheduling, snapshots, prices and outcomes are unchanged.

All five remaining geometric ending pictures were removed after the event CSV and imported asset moved to the new artwork and reference scans found no remaining consumers.

Validation: Test-ContentImport passed. Focused disposable Unity EventPaperCapture compiled and passed five image/thumbnail mappings, article rendering without dialogue, Codex navigation, tower click and close. Live presentation references match the actual isolated Unity import. Screenshots are in Data/Previews/2026-10-30-Events. Broad ShelfCapture stopped at the shopping drag assertion after concurrent slot changes; no slot changes were made here. GameOperations/CsvContent remain blocked by the existing ComfortFatigueChecks.cs:48 float-to-int compile error. Manual Play Mode, small-screen and Web build checks remain open.

## New events and easy mode (2026-11-19)
Eight events: Harvest week (fruit/vegetables −30%), Bean & lentil week (beans −25%), Lantern festival (+150 visitors, Sweet requests ×2), Cold snap (Hearty and Comfort ×2), Community nutrition workshop (sorting refunds ×1.5 for a day), CheapCart freezer delay (back freezer sold out), Sweet potato shortage (sweet potatoes sold out, purple sweet potatoes +30%), Heat wave (Sweet ×2, fruit/vegetables bought that day keep a day less).
Start days (2026-11-20): Cold snap, Lantern festival and Heat wave have `min_day` 4, the day guest requests begin (`cravingsStartDay`), so their request boosts always have an effect and the headline is never misleading.
Heat wave (2026-11-20): its request boost is Sweet ×2 (was Fizzy, which no guest ever asks for: no customer likes Fizzy and no recipe is tagged Fizzy).
`Events.csv` has an optional `easy_mode` column (blank/true = allowed). With `BalanceConfig.easyMode` on, events marked false are never chosen: the two store closures, the oat price rise, the sweet potato shortage and the heat wave.
Sold-out listings stay on the shelf, greyed with "Sold out", and can't be added; the navigation helper and craving feasibility skip them. Store closures still close the whole store (`ClosureReason`); food/aisle closures use `UnavailableReason`.
After editing, run **Import All CSV Content** (or Import Events CSV) so `EventDatabase.asset` picks up the new rows.

### Eight event illustrations (2026-11-19)
Added individual transparent Assets/Art/Events/EV_<ID>_icon.png files for HARVEST, BEAN_WEEK, FESTIVAL, COLD_SNAP, WORKSHOP, FREEZER_DELAY, SWEET_POTATO and HEAT_WAVE. Both image_path and thumbnail_path point to the corresponding PNG. Existing five icons, event text, effects, colours and eligibility were preserved. Exact prompts and generated-file provenance are in Data/EventArt_20261119.json (documentation only, not a runtime catalogue).

All eight were visually inspected and verified as square 1254x1254 RGBA images with transparent corner pixels. Test-ContentImport passed; Test-CsvContent stops at the existing ComfortFatigueChecks.cs:48 float-to-int error. Live Unity import remains pending: editor automation could not reliably access the Unity window. Run Tools > Blue Zone Bistro > Import All CSV Content to import the sprites and bind the CSV paths; runtime rendering has not yet been verified. No manually patched EventDatabase or gameplay changes were made for this art batch.

### Event art import follow-up — 2026-11-20
The eight new event PNGs already had Unity metadata but the live EventDatabase still had blank image/thumbnail references. A disposable Unity import verified all 13 event illustrations and thumbnails. Filled only those eight missing image/thumbnail pairs with their existing PNG GUIDs; preserved all event text, effects and concurrent data edits. CSV paths remain the source of truth. No live Play Mode news interaction was performed in this pass.

### Event Web compression verified — 2026-11-22
All 13 event sprites have 1024 Web overrides with automatic compression, quality 75. Original PNGs, GUIDs and CSV paths remain unchanged. Release report shows 1 MiB per event instead of 6 MiB. Disposable WebGL-target captures rendered all 13, passed newspaper/Codex/tower/close checks, and expanded Harvest/Festival were visually reviewed. No event rules or dialogue edits. See SubmissionCleanup_20261120.md for measured build sizes and browser-testing limits.
