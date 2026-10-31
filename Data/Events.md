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
