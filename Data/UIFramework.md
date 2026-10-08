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

