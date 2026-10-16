# Card UI implementation checkpoint

## Milestone 1 — implemented

GameBootstrap automatically connects the Canvas interface in every phase.
No scene rebuilding or hand-wiring is required to try this milestone.

- City map placeholder → store → shelf browsing, using imported stores/listing shelf IDs.
- Cross-store cart, listing cards, click-to-flip backs, drag one package into the cart.
- Reusable slots display small non-draggable card representations. Click to inspect,
  then drag a card into RETURN / REMOVE; cart removal removes one package.
- Overbudget cart outline, atomic full-price checkout, unchanged service-owned refunds.
- Unique food sorting, no correctness rim warnings, slot contents can be returned before submission.
- Individual-batch pantry cards accessible from market/sorting. Existing pantry stays available in service/results.
- Normal card backs exclude Traffic Light classification; educational sorting feedback remains after submission.

## Main files

- BistroUITheme: palette, optional city/frame art, font, debug card details. Create with
  Assets > Create > Blue Zone Bistro > UI Theme and assign GameBootstrap's UI Theme.
  Missing theme uses muted defaults; no artwork is required.
- BistroWidgets: Canvas layout/text/button/grid construction.
- BistroFoodCard: front/back, pointer threshold via EventSystem, drag ghost.
- BistroDropSlot: shared drop/open interaction, small seated cards, supplied warning state.
- BistroUIScreen: market/sorting screen navigation and temporary selections. Calls existing
  GameLoop commands; does not compute accuracy/refunds or mutate pantry quantities.

The current Canvas hierarchy is generated at runtime and inspectable during Play.
Five columns fit when shelf width permits; narrower panels wrap automatically.
Mouse wheel scrolls shelves; card dragging is reserved for placement.
StoreData.shopBackground and FoodData.icon are already used when supplied.

## Milestone 2 — meal slots, dialogue and results implemented

- Meal pantry cards group usable batches by food ID, sum servings and show earliest expiry.
- Each drop assigns one serving to a recipe slot; optional and extra slots are included.
- Required slots need exact category/count. Optional slots are empty or exactly filled.
  Placement remains permissive, with warning rims and explanations in slot contents.
- Remove one serving by dragging from the slot contents panel to the return area.
- MealSlotSelection carries food/slot intent. MealAllocation resolves earliest finite expiry
  first, then non-expiring stock, with stable batch-ID ties. Preview never mutates stock;
  MealService re-resolves and validates on commit. Legacy batch input remains supported.
- Explicit placements participate in recipe success: swapped slots and extras fail even
  when aggregate categories would otherwise satisfy the recipe.
- Confirmation reports recipe warnings and effects before consumption.
- Canvas dialogue uses portraits/optional voice clips without leaving the service background.
- Canvas dish results and day results replace the fallback screen. Pantry is accessible throughout.
- Optional BistroUITheme.serviceBackground supplies bistro artwork.

BistroUIScreen.Service.cs separates meal/dialogue/result layout from market navigation.
Tools/MealSlotChecks.cs covers allocation, unavailable/expired stock, preview immutability,
wrong slots, optional slots, extras and commit revalidation.

## Remaining visual refinement

Settling animations, authored artwork,
and optional conversion of generated layouts to prefabs.
Interactive Play Mode verification remains necessary; assembly compilation is not runtime QA.

## Manual acceptance walkthrough

Press Play in SampleScene. Open a store and shelf. Flip a card, then drag it into the
cart. Add enough packages to exceed budget: the cart rim should warn. Open the cart,
drag a package to RETURN / REMOVE, close and verify its count and total. Checkout.
Place foods in sorting slots, inspect one slot and return a food, place it elsewhere,
then submit. Continue through existing service/results and verify the next market opens.
Also test an empty checkout, known-food-only checkout, narrow window, and pantry inspection.

Compilation and operation/content tests do not replace this visual/interaction walkthrough.
This milestone has not been published or committed automatically.


## Follow-up polish

User verified the original Canvas interactions in Play Mode. Subsequent improvements:
- Scroll positions survive card placement/removal and opening/closing slot panels.
- Long sorting feedback, meal previews and results use readable scrolling text.
- Daily results include individual dish outcomes.
- Rejected checkout stays open with the error visible for immediate adjustment.
- Non-draggable cards are rejected by drop targets as well as by selection validation.
- Drag ghosts and image placeholders follow the configured theme palette.

These follow-up changes passed compilation and existing gameplay/content checks;
scroll restoration and the updated reading panels still need a visual Play Mode check.


## Procedural interaction polish

The city view now contains a clipped, larger map with inertial two-axis dragging.
Store buttons are children of the map and move with it; Pantry, Menu and cart
controls remain outside it. Recenter returns to the map's initial view. Map position
survives UI redraws and store visits, separately from vertical shelf scroll positions.
The placeholder blocks can be replaced through the existing theme city background.

`BistroUIScreen.City` owns map composition, `BistroMotion` supplies brief unscaled-time
button and entrance motion, and `BistroUIScreen.Motion` animates only navigation
changes. Card flips take 0.26 seconds and switch content at the narrow midpoint.
Dragging interrupts a flip cleanly, dims the original card, and suppresses accidental
post-drag clicks. Drop targets ease toward their hover size without changing warning
semantics. Animation never delays a gameplay transaction or uses game time scale.

Play Mode checks: drag both empty map space and a store button (dragging should pan,
clicking should enter); pan to each edge; enter/leave a store; open Pantry and return;
use Recenter; rapidly click a card, then drag it during a flip; drop into and out of
slots; open/close the cart and tutorial instructions. Check at both wide and narrow
Game view sizes. Compilation and gameplay checks do not substitute for this visual
interaction walkthrough.

### Interaction hardening

- Pantry and exhausted cards forward drag gestures to their surrounding scroll view.
  Draggable food cards keep their existing drag-to-slot meaning; mouse wheel scrolling
  remains available over either kind.
- Face-up/face-down choices survive panel opening and data redraws within the same
  browsing context. This is bounded UI memory, cleared when binding a new session.
- Shelves shrink cards only when a single normal-sized card cannot fit; widening the
  view restores their preferred size. Recipe and ingredient-slot grids specify their
  own preferred dimensions through `BistroResponsiveGrid.SetPreferred`.
- Navigation animations target content panels, never the fixed HUD. `BistroUITheme`
  exposes flip duration, panel duration and reduced motion for presentation tuning.
- Losing application focus cancels a food drag. Drop slots require an active drag,
  preventing a cancelled gesture from committing a selection when focus returns.
- Empty shelves, pantry, cart and ingredient panels explain what to do next. Empty
  sorting hands explicitly indicate that cards are already in the answer slots.

`Tools/UnityUIInteractionSmoke.cs` is an isolated Play Mode harness (copy to an isolated
project's Assets/Editor with the runtime dependencies, then follow its header command).
It exercises real Unity layout, card flips, drop/cancel handling, drag cleanup,
pantry scrolling and animations while timeScale is zero. It does not replace visual
review of the full game or browser/touch-device testing.

### Animation and input lifecycle

`BistroMotion` owns a single reveal per object. Repeated reveals reuse the component
and continue from the displayed pose. Disabling a panel or losing focus restores its
authored scale and opacity. Hover checks use a cached Selectable, and settled motion
stops writing transforms. Page and overlay animation contexts are separate: closing
Pantry, the cart or an instruction panel no longer reanimates the underlying page.

Food-card drags belong to the pointer that began them. Only that pointer can move,
end or claim the drop, and each gesture can produce at most one drop intent. Services
still validate the intent as before. Focus loss also releases a forwarded pantry-scroll
gesture. Slots ease back to rest if the dragged card disappears or is cancelled.

The isolated Play Mode harness additionally checks duplicate drop callbacks, pointer
ownership, restarted/interrupted reveals, and preservation of authored transforms.
