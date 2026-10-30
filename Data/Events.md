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

Five examples: CheapCart sale, fictional oat-demand price rise, Verdant maintenance closure, CheapCart restocking closure (added 2026-10-24), and bistro publicity (+200 attendance popularity since 2026-10-24, was +500) paired with a Verdant promotion. Their codex entries explain game rules, not medical claims. Optional source buttons accept HTTP(S) links only. Artwork reuses the geometric ending placeholders; replace paths/asset references later.

## Runtime ownership

- EventService prepares each morning once, before reservations and the checkpoint. It samples eligible events without replacement, excludes already-active events, and snapshots effect values in EventRecord. Reimporting content does not change a saved event's effects.
- EventRecord stores start day and exclusive expiry date. Starting day 2 for three days means active on 2/3/4, expired on 5. The archive retains expired events; evaluators ignore them. No asset mutation or subtract-to-undo operation occurs.
- MarketEffects computes prices, closure reasons and publicity. Prices use `max(0, baseline + sum(additions)) * product(multipliers)`, rounded once to cents away from zero at the midpoint. Invalid/overflowed prices fail checkout.
- PurchaseService and UI use the same effective prices. Inventory batches record actual paid cost. Minimum-price failed-dish valuation remains based on original authored listing prices. Sorting refunds remain based on the actual checkout receipt.
- Temporary publicity affects attendance capacity only. Earned popularity, permanent customer unlock requirements and ending selection remain unchanged. UI shows earned popularity plus the temporary bonus separately.
- Closure eligibility prevents all accessible stores from closing and rejects simultaneous store-targeted pricing/closure conflicts. Purchases are rejected by the service even if a cart already contains those listings. Pantry food remains usable. Craving feasibility excludes closed stores.

Day-start checkpoints include the plan and effect snapshots. Repeated queries/resume do not reroll. Old saves with an already-prepared reservation list get events from the following day, avoiding retroactive closures after requests were chosen. New Game resets the archive.

## Presentation

On the city map, new-day announcements slide in from the right, remain for six seconds, and advance automatically. They do not change phase, freeze gameplay or require acknowledgment. Notifications hide while browsing a store/modal, then continue on return. They can replay after resuming the day; this changes no gameplay state.

News remains available in the header. Its archive includes active and expired events. Click a notice or archive entry for larger artwork, details, effect list, duration, optional audio, and codex buttons. An assigned source URL opens only when the player clicks Open source. Closed stores remain visible but cannot be entered. Price cards show original → effective prices.

## Validation

`Tools/EventChecks.cs` runs with `Tools/Test-GameOperations.ps1` and the CSV regression suite. `Assets/Editor/EventImportChecks.cs` is an isolated-project import/save test that intentionally submits invalid data. `Tools/EventUISmoke.cs` is copied into an isolated project's Editor directory for actual Play Mode UI checks and screenshots; neither test writes the player's checkpoint.

Balance values are prototypes. No nutrition changes, secret-store unlock rules, food bans or real medical news are implemented.

Tower news update: the city NEWS hotspot opens the archive; notifications use a tower-anchored speech bubble with existing click-to-detail and six-second timing. Missing images remain optional placeholders. Event scheduling/effects are unchanged. Isolated injected UI checks passed; manual Play Mode/browser checking remains open.
