# Card UI framework

Status (2026-10-22): this Canvas UI is the live player-facing interface for every phase,
including main menu, news/events, tutorials and endings. The sections below are kept in
the order the milestones were built.

## Current state (2026-10-28) — read this first; later sections are history

- **Art comes from the CSVs**, not from JSON catalogues or sprite sheets: Foods.csv `icon_path`/`back_path`/`card_style`,
  Recipes.csv `icon_path`/`back_path`, Stores.csv `logo_path`/`shop_background_path`/`shelf_colours`, Categories.csv
  `icon_path`. Screen art (city, service room, counter, plaques, rail, sprig, cart icon) is on `BistroUITheme.asset`,
  auto-filled from `Assets/Art/UI/<field>.png`. `Resources/BistroArt/world.json` holds click regions only.
  Sections below that mention `catalog.json`, `recipes.json`, `decor.json` or sprite sheets describe the old pipeline.
- **One dialogue box at a time:** panels the player opened, then the Blue Zone Guide, then guest/dish/day text
  (`DialoguePanel` queue, `FlushDialogue`).
- **Market:** city map → store overview (world.json regions over the store background; plain shelf buttons if a store
  has no art) → illustrated shelf with the cart panel. The cart review is always the illustrated `IllustratedCartReview`;
  sorting bins are always `IllustratedSortBin`. The old text grid shelf, market footer and generic cart panel were removed.
- **Top bar:** logo = menu; back button in stores; place; Day, Popularity, Wallet (each opens a one-time lesson on first
  click), Cart (market), Recipes, Guests, Pantry, Help. Same font, ink, 24 px icon and spacing for every item.
- **Recipe cards when cooking:** a recipe the pantry cannot fill is faded to half opacity and says "Not enough in your pantry" (still selectable; a mismatched dish pays less). Otherwise the bottom line shows recipe fatigue ("Served N× in 2 days · popularity ×0.5") or "Choose recipe".
- **Recipe card art:** the dish is drawn once (top picture); the rest of the front and the whole back use the background-only `back_path` image. The back has no white reading veil (2026-10-28). Unbuildable cards get a cream wash on top instead of lower opacity, so layers never show through.
- **Sorting results** show the refund per food ("Correct · $1.25 back" / "Needs another look · no refund").

## How screens are built (read before the UI/art pass)

- `BistroUIScreen` is a partial class: `.cs` (shell, market, sorting), `.City`, `.Service`,
  `.Menu`, `.Events`, `.Ending`, `.Tutorial`, `.Motion`, `.Scroll`, `.CardMemory`.
- Every command sets `dirty`; `LateUpdate` destroys the current screen object and rebuilds
  it from scratch (`Refresh`). Scroll positions and card faces are remembered across rebuilds.
- Layout uses hard-coded normalized anchors inside each draw method (reference resolution
  1440×900) and literal English strings. There are no prefabs.
- The legacy IMGUI `GreyboxUIController` is gone (file emptied 2026-10-28; delete it and its `.meta` in Unity).
- Optional art hooks already exist: `BistroUITheme.cityBackground` / `serviceBackground`,
  `StoreData.shopBackground`, `FoodData.icon`, dialogue portraits and line backgrounds.

Consequences and recommended direction are in `KnownIssues.md` (UI section).

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
- Every food card front shows an "Appeal N/10" strip (bottom of the image, card-palette colour, from `FoodData.ingredientAppeal`).
  It is a placeholder for delight icons, useful in play and debugging; it hides when the card flips and is
  turned off with `BistroUITheme.showCardAppeal`. Appeal is not a Traffic Light answer, so it is safe to show.
  The playtest harness shows it on shelf, sorting and service pantry lines too.

## Main files

- BistroUITheme: palette, optional city/frame art, font, appeal badge toggle, debug card details. Create with
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
- Each drop assigns one serving to a recipe slot; optional slots are included. There is no "Extra ingredients" slot (removed 2026-11-20: anything placed there only made the dish a mismatch).
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

## Recipes panel, pantry selling and dish review (2026-10-24)

- A **Recipes** button in the top bar (every phase) opens "TODAY'S MENU": each unlocked recipe with base price,
  availability, the requests it satisfies, and its required and optional categories. The text shares
  `RecipeSummary` with the service recipe buttons, which also show the base price.
- **Pantry** at the market shows each batch's per-serving sale value and a SELL slot (the shared
  `BistroDropSlot`, same as recipe slots). Each drag adds one serving; click SELL to open it and drag servings
  onto RETURN ONE SERVING; "Sell for $X" confirms. Pending servings live only in the UI (`sellSelections`) until
  confirmed. Batch cards are draggable only at the market; elsewhere the panel says food can be sold there.
- **Review dish** no longer forecasts the outcome. In normal play it shows whether the plate matches the
  recipe, any slot warnings, what is on the plate and which of the guest's requests the recipe answers.
  Health, delight, popularity, payment, tip and cost appear only after serving. With
  `BistroUITheme.debugCardDetails` in an editor/development build it still shows the full forecast.
  The first-meal lesson still checks the preview internally to require a built recipe.

## Guest info, plate appeal, recipe history and run recap (2026-10-25)

- Service header: request line plus "Health N · Delight N · Daily Dozen eaten k/11: …" for the current guest
  (numbers for now; bars or words can come with the UI pass).
- Meal building title: "Plate appeal so far 6.3 · Comfort needs 7", from the placed servings' card appeal.
- Recipe buttons and the Recipes panel: "Served N× in 2 days" and, when fatigued, "guests are used to it, popularity
  gains ×0.5". The Recipes panel explains Comfort. Dish results say "(guests are used to this recipe)".
- Ending screen: after the story, the panel shows a recap (dishes and matched recipes, green/yellow/red servings,
  recipes tried, Daily Dozen groups served and missing, money/popularity/wellbeing, each guest's health start → end).

## Illustrated produce shelf (2026-10-25)

BistroUIScreen.Shelf.cs provides the first art-backed shelf when BistroShelfArt matches the store/shelf IDs in Resources/BistroArt/catalog.json. JSON maps food IDs to top-origin atlas cells; missing food artwork keeps the existing plain card. Runtime sprites are cached for the session. The atlas is read through Resources, so no scene wiring or CSV reimport is needed. New listings reuse mapped food art automatically; new foods need a presentation mapping/art entry. This catalogue currently supports one shelf theme; expand to multiple theme definitions for the next store batch.

BistroFoodCard.ApplyShelfArtwork provides clipped full-card food art, a live title plaque and live servings/price footer. Appeal and claims remain on the card back; debug-only classification remains guarded. The matching cream toolbar, illustrated cart thumbnails, cart review and drag ghost now use the same visual language. All listing, event-price, quote and checkout validation still use the existing services. Other shelves keep their existing presentation.

BistroShelfLayout repeats shelf rails and calculates card columns from the viewport. BistroShelfStyle provides rounded panels and licensed Lora/Liberation fonts; BistroShelfIcon draws small presentation icons. BistroShelfCartTarget forwards drag intent and retains the card's single-drop guard. Disabled responsive grids must not be reapplied during scroll restoration.

BistroShelfArtImport enforces Texture2D import for the atlas. The prior missing-food failure was a cubemap import, not a missing food mapping. It repairs settings on editor load; the menu Tools > Blue Zone Bistro > Reimport shelf artwork also runs the check. Preserve the atlas GUID.

Validation: runtime assembly compiles with zero warnings/errors. An isolated Unity Editor project rendered the actual Canvas at 1672x940 and checked atlas lookup, card flipping, single-drop handling and cart refresh through injected pointer events. This is offscreen Editor verification, not manual Play Mode or browser testing. Manual scrolling, modal interaction, checkout, resize and event-price presentation still need playtesting. Tools/UI/ShelfCapture.cs is the isolated capture harness; do not install it into the live game. Food art remains a review batch, not approved mass production.

### Layered card refinement (2026-10-26)

The produce presentation now uses optional per-food `lightLogo`, `darkPlaque`, `plaqueTilt` and `foregroundLeaves` properties in the art catalogue. Live names/prices remain UI text. Foreground decoration sits above the plaque but below readable text, hides on flip, and never receives input. Thin offset card/rail shadows, illustrated irregular plaques, a transparent leaf/cart sprite atlas and oak rail artwork replace the most visible generic shapes. Rack slots remain repeated UI decoration. No gameplay content changed.

`decor.json` contains normalized top-origin source rectangles for `ShelfDecor.png`. `catalog.json` optionally supplies normalized rowEdges to avoid cross-row bleeding in generated atlases whose boundary is not exactly halfway. Missing decoration falls back safely. Food/background remain a combined tile in this batch; the foreground and plaque are independent layers. A fully independent food cutout/background pair is not yet authored. Shared top-bar adoption across other phases remains future UI work.

### Multi-shelf presentation and shared toolbar (2026-10-26)

The same illustrated shelf is now selected through `catalog.json` themes for S001/PRODUCE, S002/BOTTOM_SHELF, S002/BACK_FREEZER, S003/BULK_SACKS and S003/BULK_PRODUCE. Each theme supplies store/shelf IDs, rack colour, optional logo and rail artwork. Additional atlas definitions supply food mappings. Existing listing CSVs still exclusively determine availability, stock and prices. Adding a listing for an already illustrated food on a themed shelf requires no UI code; new art needs a catalogue entry. Other shelves retain plain fallback presentation until their artwork is authored.

The presentation file is JSON, not gameplay CSV. Food plus background remain deliberately composed together in each tile; independent foreground decoration is now referenced by an artwork key rather than a food-specific boolean. Plaques, text and shadows are UI layers. This is data-driven presentation, not an assertion that every drawing/layout parameter is content-authored or that food/background can already be tinted independently.

GlobalToolbar replaces the legacy top controls across active market/city/store, sorting, service/meal-building and day-results views. It shows contextual location, day, wallet, Recipes, Guests, Pantry, Menu and News. Generic inspection/cart/news overlays stop below it. Title screen and ending presentation remain dedicated full-screen views. Other menu body styling is not redesigned in this pass.

Verification: runtime assembly compiles without warnings/errors; isolated Unity Canvas render/injected flip and duplicate-drop checks pass. All current authored listings on the five themed shelves have mapped artwork. Four added shelves and city toolbar were captured by Tools/UI/ShelfCapture.cs; manual Play Mode/browser interaction remains outstanding. Catalogue coordinates use a small inset to exclude generated divider lines.

### Food-specific card backs and Wellness batch (2026-10-26)

Card backs now use a companion background atlas, retaining the food palette and contextual silhouettes behind a translucent reading veil. Information is unchanged. Presentation JSON accepts `backTexture` and optional `backRowEdges`; missing back art falls back to the front illustration. Front food/background are still composed together, while plaque, overlap, text and shadow remain UI layers.

S001/WELLNESS now uses the illustrated shelf. Eight added food illustrations bring the catalogue to 38 foods across six themed shelves. This does not change food classification, listings or gameplay. New items still require authored artwork mappings; adding a price row alone cannot invent artwork.

Current verification is being repeated with Tools/UI/ShelfCapture.cs, including card-back capture and Wellness coverage. Earlier screenshots do not establish these latest changes. Manual Play Mode/browser interaction remains outstanding.

Verification completion: runtime assembly build passed with zero warnings/errors after restoring its temporary build metadata. The isolated Unity run reached SHELF PASS: flip, duplicate-drop protection, cart refresh and listing-art coverage for all six styled shelves. Reviewed new CardBacks.png and WellnessShelf.png in Data/Previews. The disposable project logged package import/path errors during initial setup but subsequently compiled and completed the capture; this is not a clean full-project validation. Manual Play Mode/browser checks remain outstanding.

### Remaining shelves illustrated (2026-10-26)

Added presentation themes for S001/LUXURY, S002/EYE_LEVEL, S002/BARGAIN_BIN and S003/PALLETS. All ten current shelf IDs now use the shared illustrated renderer. Three front atlases and three background-only companion atlases add 25 foods, bringing coverage to all 63 current food IDs. Art selection is still by stable food ID; listings, prices, unlocks and authoritative purchases remain unchanged. New catalogue entries use rowEdges/backRowEdges to exclude neighbouring tiles. No gameplay CSV changes were needed.

Themes retain muted store colours and existing wood rails; this pass expands coverage, not a separate bespoke shelf geometry/material system. Art remains reviewable. The normal missing-art fallback is retained for future foods.

Verification: runtime assembly build passed with zero warnings/errors. Isolated Unity capture completed with SHELF PASS, including sprite lookup for all current listings, flip, duplicate-drop protection and cart refresh. Visually reviewed all four new front screens, lower Luxury rows and Eye Level backs. Additional reverse captures are saved in Data/Previews. No current listing lacks a front mapping (63 unique food IDs); all ten shelf themes are present. Manual Play Mode/browser interaction remains unverified.

### Clickable city/store placeholders (2026-10-26)

City and three store overview images are runtime resources under BistroArt. world.json maps normalized top-left polygon coordinates and marker positions to existing store/shelf IDs. BistroWorldRegion performs polygon hit testing; BistroWorldButton suppresses drag-clicks. Image fitting preserves aspect ratio and region alignment. City is a clipped pannable image, store overviews fit the viewport; no zoom transition or roof animation is implemented.

UI asks existing queries for unlocked stores, shelf listings and event closures; clicks recheck access. Unavailable regions are disabled. Missing artwork falls back to the previous views; future stores/shelves without regions retain fallback controls. The city currently links only the three shops; bistro/tower interactions are not included in this pass. Existing cart and checkout flow is retained.

CheapCart and MegaBulk card/header logos are raster emblems loaded by decoration keys from decor.json; decoration entries may specify a separate texture. Themes select the logo by store. No gameplay CSV or semantic changes were required.

Verification: isolated Unity compilation/capture passed WORLD PASS (three shops, ten shelf routes, drag-click suppression, day-one MegaBulk lock) and SHELF PASS. City and three store captures reviewed. Tests inject pointer callbacks; manual Play Mode/browser raycasting, resizing and panning remain outstanding. Reviewed navigation captures are in Data/Previews. Initial city framing was reduced and Recenter moved to avoid clipped/overlapping shop labels.

### Full-height map and tower news (2026-10-26)
Removed the legacy footer from illustrated city/store overviews. Artwork now fills below the toolbar. Cart review is accessible beside the wallet; tutorial Help is in the toolbar when an active tutorial exists. Shelf cart controls remain unchanged. The NEWS polygon in world.json opens the existing archive. Timed event previews now appear as cream speech bubbles above the tower, follow its position during panning and stay within the viewport; clicking opens existing event detail/codex UI. Existing six-second sequencing and tutorial/modal visibility rules are retained.
Validation: isolated Unity compilation/capture passed NEWS PASS (archive and detail routes), WORLD PASS and SHELF PASS. City and tower-bubble captures visually reviewed. Manual browser/Play Mode pointer checks remain outstanding. No gameplay/event effect changes.

### Supporting menu visual pass (2026-10-27)
Main menu uses the existing city artwork behind a cream welcome panel; removed the technical save-storage explanation while retaining save errors and new-game confirmation. Return confirmation now says the player will restart today. Shared overlays (pantry, recipes, guests, news, tutorial and return confirmation) use cream/sage panels, rounded controls and the shelf fonts through a scoped presentation styling pass. Food-card visuals are excluded from that pass. Pantry batches reuse food artwork with servings/expiry and no store logo; batch identity and sell actions remain unchanged.
These are screen-space Canvas overlays, compatible with later 3D scene backgrounds. This is a shared visual pass, not a bespoke layout redesign of every panel; recipe/guest lists remain text-based and the service/ending compositions remain separate.
Validation: isolated Unity compilation/capture and existing NEWS/WORLD/SHELF injected checks passed. Main menu, recipe list and seeded pantry captures reviewed. Manual Play Mode/browser selling, resume/new-game confirmations and smaller-screen layouts remain unverified.

### Illustrated recipe book (2026-10-27)
Recipes opens a scrolling library of large front-facing cards. Each card flips in place to Required/Optional category tiles with quantities. All names, base prices, craving tags, unlocks and slots come from queried imported RecipeData; duplicate category slots are grouped within their required/optional section. This is a reference library, not a replacement meal-building workflow. No food choices alter its representative dish artwork.
Presentation-only Resources/BistroArt/recipes.json maps recipe IDs and FoodCategory names to atlas cells, with optional normalized row top/bottom bounds. Recipes.png covers the 15 current recipes; Categories.png covers all 12 assigned ingredient categories. RecipeData.icon overrides mapped artwork if provided. Missing recipe art uses a labeled placeholder; missing category icons retain their category text/quantity. New recipe data appears without UI code changes; new bespoke art requires a mapping. New category semantics still require extending the existing enum/importer, not merely JSON.
Backs reuse the dish art under a cream reading veil, rather than separate background-only art. Fonts, names, prices, cravings, requirements and buttons are runtime UI, not baked into artwork. Card backs use scrollable icon groups for larger recipes. Screen-space UI remains independent of eventual 3D scene rendering.
Validation: isolated Unity compilation and RECIPE PASS verified current unlocked card count, every current recipe/category artwork mapping and flipping. Front/back captures visually reviewed after correcting atlas boundaries and long labels. Existing WORLD/NEWS/SHELF checks passed. Manual pointer scrolling, narrow screens and full meal-building integration were not tested; no gameplay formulas or CSV changes.

Recipe overlay refinement (2026-10-27): front reading veil peak opacity is now .50 (previous .72), reverse .56 (previous .94), adjustable via BistroUITheme.recipeFrontVeilOpacity/recipeBackVeilOpacity. Shared cached UI alpha mask feathers both veils toward their edges. Food art and data unchanged; solid ingredient tiles/title plaque retained. Isolated Unity RECIPE checks passed; front/back renders reviewed. Manual Play Mode remains unverified.

### Service scene layering (2026-10-27)
- Service uses the selected midtone bistro from Resources, central customer portrait or temporary silhouette, matching cropped foreground counter, then dialogue/cooking overlays. The scene remains independent of gameplay services and may later be replaced by a 3D view.
- Dialogue uses a feathered cream veil at 60% centre opacity. Cooking dims the scene and presents illustrated recipe selection, existing ingredient slots above pantry cards, and existing preview/serve validation.
- Recipe-card backs now use background-only slices declared in recipes.json (`<recipe-id>-background`); no dish is repeated. Currently these are clean gradient strips from the atlas, not newly painted motif layers.
- `decor.json` defines room and counter source rectangles. Counter crop is specific to this selected image; changing the room requires updating its matching counter reference.
- Customer art/animation remains placeholder work. Pointer/drag behaviour in live Play Mode and browser still requires manual validation.

Verification for the service-layer pass: isolated Unity compilation and callback/render checks passed for service dialogue advancement and illustrated recipe selection, alongside existing shelf/world/recipe checks. Content importer checks passed. GameOperations and CsvContent checks are blocked by the existing float-to-int assignment at Tools/ComfortFatigueChecks.cs:48; no gameplay test files were changed for this UI pass. Captures are in Data/Previews/Service*.png and RecipeBacks.png. The service fixture bypasses sorting only inside the disposable preview project; this is not end-to-end checkout validation.


### Compact dialogue and cooking update — 2026-10-28
User rejected the misty cream gradient panels and full-page guide. `BistroUIScreen.DialoguePanel.cs` now provides a shared bottom dark fade, speaker/name column, light text, pagination, final action and optional Back. Dialogue, tutorial instructions, guest information, dish review/result, day summary and return-to-menu confirmation use it. Browsing interfaces remain card/grid views.

Cooking buttons and ingredient slots use the illustrated palette; seated ingredient miniatures use their food artwork. The persistent “Comfort needs…” line was removed. The existing CSV request lesson explains Comfort using `{comfort}`, expanded from `BalanceConfig` through `IntroMealPlan`; no scoring rule changed. Tutorial review reads encountered lesson text without replaying actions. City Recenter was removed and replaced with Tutorial; toolbar Help is always available.

Recipe backs keep background-only art. Customer silhouette is still a placeholder. Future 3D replacement must retain the independent overlay structure. Earlier notes approving the cream dialogue veil are superseded by this decision.

Verification/handoff (2026-10-28): the shared bottom panel also handles event/Codex explanation and ending prose, retaining source links and final actions. The atlas catalogue is now nonrecursive to avoid Unity serialization-depth warnings. Tutorial CSV was reimported; its asset diff was checked before copying back. Offscreen Unity/callback checks and ContentImport passed; broader gameplay checks are blocked at ComfortFatigueChecks.cs:48 (existing float-to-int mismatch). No manual Play Mode/WebGL or narrow-screen validation is claimed. Data/Previews contains Dialogue, Guide, Review, Result, Slots and DaySummary captures for review.

## Art from data, not JSON catalogues (2026-10-28)

All per-item art now comes from data assets filled by the CSV importers; the UI never looks art up by ID.

- Food cards: `BistroFoodCard.ApplyShelfArtwork(FoodData food, price, servings, Sprite logo)` uses `food.icon` (front), `food.cardBack` (back; falls back to the front), and the `lightLogo`/`darkPlaque`/`cardSprig` flags from `card_style`. Plaque tilt alternates by card position instead of being authored per food.
- Recipe cards: `BistroRecipeCard.Create(parent, recipe, theme, foods)` uses `recipe.icon`, `recipe.cardBack` and `foods.CategoryIcon(category)` for the requirement tiles. Meal slots use the same category icon and each seated food's `icon`.
- Shelves: `StoreShelfDefinition.colour` (Stores.csv `shelf_colours`) and `StoreData.logo` (`logo_path`). Every shelf a store defines uses the illustrated layout.
- City, store and bistro pictures: `theme.cityBackground`, `StoreData.shopBackground`, `theme.serviceBackground`/`serviceCounter`. Plaques, shelf rail, sprig and cart icon are theme slots too.
- `BistroWorldArt` now only reads click regions from `Resources/BistroArt/world.json`.
- Removed: `BistroShelfArt`, `BistroRecipeArt`, `BistroShelfStyle.DecorationSprite`, `BistroShelfArtImport` (files left as one-line stubs to delete), and the use of `catalog.json`, `recipes.json`, `decor.json` and the sprite sheets.
- The sheets were cut into individual PNGs with the exact rectangles the old code used, so the game should look the same. Two intentional differences: recipe backs are now clean vertical gradients (the old strip for the smoothie showed a pink straw streak), and images are trimmed by up to 3 px to a multiple of 4 for compression.
- Not verified in Unity yet (no compiler in the AI workspace): compile, Import All CSV Content, then check a shelf, the cart, pantry, cooking slots, the recipe book, the city and a store view.

## One dialogue box at a time (2026-10-28)

Bug: a guide lesson and a guest line (or dish result) could be drawn at once. Both boxes shared one page counter, so Next on the guide was reset by the box below and never advanced; the game also blocks the guest's Continue while a guide lesson is active, so play got stuck.

Fix: `DialoguePanel` now queues a request (an inactive placeholder keeps its layer position) and `FlushDialogue`, called once at the end of `Refresh`, builds only the last request, which is the top layer. Priority therefore follows draw order: a panel the player opened (news, guests, review dish, return to menu) > Blue Zone Guide > guest lines, dish results and the day summary. The hidden ones are simply requested again on the next refresh, so they appear in turn once the shown box is closed. Extra buttons on a dialogue frame go through the `decorate` callback (used by news and Codex). Not yet checked in Play Mode.

## Sorting screen and guide Help (2026-10-28)

- Sorting now uses the illustrated kitchen style (room, dim, cream workspace, art cards via `ApplyShelfArtwork`). The three bins are illustrated drop slots with seated mini cards, a light tint and Traffic Light shapes drawn by `BistroShelfIcon` (`LightGo` circle, `LightSlow` triangle, `LightStop` octagon), so colour is never the only cue. Opening a bin (`sort:<light>`) shows its cards like the cart review, with a take-back target. Results are one row per food: picture, verdict, the correct light's shape and the explanation. `KitchenWorkspace()` is shared helper code for this layout.
- Errors from submitting (e.g. foods left unsorted) show in place of the "placed" counter, because the room covers the old bottom notice line.
- Toolbar Help / city Tutorial now repeats only the lesson in progress. With no active lesson it says all guides are complete for now (it no longer stacks every past lesson). `TutorialController.ReviewInstructions` was removed on 2026-10-28.
- Not yet checked in Play Mode.

## Top bar and dialogue text (2026-10-28)

- Top bar, left to right: the BLUE ZONE logo (with a small menu icon) opens the return-to-menu panel; a back button in stores; the current place as icon + name (city, store logo, bistro, sorting, day complete) with the shelf name underneath; Day; Popularity (number, with "Popularity +N" while publicity is active); Wallet; Cart with package count (market only); Recipes, Guests, Pantry, Help. The Menu and News buttons were removed (news is on the city tower). Icons are vector symbols in `BistroShelfIcon` (City, Bistro, Sort, Day, Popularity, Help); `ToolbarStat` draws icon + value + caption.
- Dialogue speaker and text now sit in the lower, fully dark part of the fade (below 64% of the frame); pages split at 5 lines / 330 characters.
- Not yet checked in Play Mode.

### CSV/per-item-art verification — 2026-10-28
Preserved the later CSV-to-Sprite migration, toolbar, sorting and single-dialogue routing. Updated the isolated harness to clone the authored theme and support multi-page lessons. A fresh isolated snapshot imported the current CSVs and passed DIALOGUE, SERVICE, RECIPE, NEWS, WORLD and SHELF checks. Reviewed captures: `Data/Previews/2026-10-28-CSV/`. No old JSON art lookup was restored; only world click-region layout uses JSON. No generated test assets were copied into the live data folders in this follow-up.

ContentImport passed (including card_style and eleven tutorial definitions). GameOperations and CsvContent remain blocked by the existing float-to-int assignment in `Tools/ComfortFatigueChecks.cs:48`. Fresh Unity import initially logged package/compiler messages before completing successfully; the harness also logs runtime Destroy warnings in Edit Mode. These results are offscreen and injected-callback verification, not manual Play Mode/WebGL validation. Current Help intentionally repeats only the active lesson, per the later implementation; earlier history-review notes are superseded.

## Recipe, welcome menu and map polish — 2026-10-28

- Recipe fronts still draw the dish once; unavailable cards retain the cream wash, not group alpha. Back images use recipe.cardBack from Recipes.csv, now with ingredient motifs. No dish or opaque reading veil is drawn on the back.
- Recipe flips compress horizontally, swap faces halfway, and expand using unscaled time. Timing/reduced motion use the same theme settings as food cards. Repeated flips during animation are ignored; disabling restores scale. Long frames are capped for animation progression so loading cannot skip the entire flip. Edit-mode previews flip instantly. Selection cards still invoke their recipe-selection action.
- Start menu uses a left sage panel, botanical accent from theme.cardSprig, readable cream type, and the same New/Resume/replace-save actions. BistroMenuBlur owns and disposes a material using Resources/BistroMenuBlur.shader; it blurs only the menu city image. Missing/unsupported shader falls back safely to the original image. No 3D camera or world shader dependency.
- theme.cityMapZoom defaults to 1.4. City image and normalized hotspots enlarge together; store views stay at 1.0. BistroWorldLayout now responds to zoom/aspect/cover changes as well as viewport resize. Both axes retain clamped dragging.
- Other small menus retain their existing layouts; this pass focuses on the requested start menu and recipe backs.

### Verification: recipe/menu/map polish (2026-10-28)

Unity 6000.4.1f1 compiled and ran the updated ShelfCapture in disposable Temp/UIReview28. All shelf, recipe, service, dialogue, news and world assertions passed; additional assertions verify map overflow on both axes and the start-menu blur material. Reviewed rendered menu and recipe backs; captures are in Data/Previews/2026-10-28-MenuCards. Separate RecipeFlipCheck entered Play Mode and passed animation narrowing, paused-time progression, repeated-click suppression, disable cleanup and instant reduced-motion flip.

Test-ContentImport.ps1 passed. Test-GameOperations.ps1 and Test-CsvContent.ps1 remain blocked by the existing Tools/ComfortFatigueChecks.cs:48 float-to-int compile error; this UI pass did not change that gameplay test. No WebGL build or manual end-to-end playthrough was run. Other small-menu art/layout polish remains separate future work.

### Toolbar, markers and sorting surfaces (2026-10-29)
The global toolbar and map labels now use subtle vertical shading, fine accent rules and the existing authored botanical sprig. Toolbar opacity is 90%; active map labels are 89%, closed labels 85%. Map marker icons use StoreData.logo when available, with a speech-bubble fallback for News; hotspot geometry and navigation remain unchanged. Sorting workspaces and bins receive the same restrained surface treatment without changing their placement or classification rules.

BistroShelfIcon keeps the existing semantic shapes but rasterizes their union at 4x sampling into cached 128px alpha textures with mipmaps/trilinear filtering. Circle outlines use 64 segments. This avoids hard mesh-edge aliasing without AI-generated replacements or per-food art lookups. Assigned sprite icons still take precedence. Decorations never receive raycasts. BistroSurface changes only panel vertex tint; all food/recipe/store art continues to come from CSV sprite fields.

Store-logo sprites plus the shared sprig/cart-icon textures now enable mipmaps with trilinear filtering and uncompressed import for small-size readability. Their PNGs, CSV paths and GUIDs are unchanged. Preserve these settings on reimport.

### Surface/icon verification (2026-10-29)
Completed toolbar/marker/bin surface polish and HUD icon antialiasing. Graphics-enabled Unity capture in Tools/UI/ReviewProject passed shelf, recipe, dialogue, service, world and news checks, plus the sorting icon-cache assertion. Reviewed city and sorting captures; final previews saved under Data/Previews/2026-10-29-Surfaces. The disposable project's initial package import emitted transient ShaderGraph/package-cache errors; the final fresh capture completed without those errors. Existing full-suite ComfortFatigueChecks.cs:48 float/int test compile blocker remains unchanged. No new manual end-to-end Play Mode or WebGL validation for this surface-only pass; earlier dedicated recipe flip Play Mode check passed.

### Shared contextual action buttons (2026-10-29)
BistroActionButton.Create accepts an explicit BistroActionRole (Primary, Secondary, Caution), caller-owned bounds, label and callback. Theme actionPrimary/actionSecondary/actionCaution/actionInk fields control the palette: muted sage, warm cream, soft clay and dark ink by default. This replaces inherited deep-green blocks for cooking actions, dialogue Next/Previous/Back, optional event-dialogue actions, sorting submission and continuation. Next/continue/review are primary; back/recipes/previous are secondary; skip customer is caution. There is no label-string inference.

All roles share rounded geometry, subtle gradient, thin rim, upper highlight and light shadow. Hover/pressed/disabled use Selectable colors; selection adds a focus rim; press reduces the shadow. Reduced motion disables color-fade timing. Existing main-menu controls, toolbar items and world indicators retain their separate designs. No gameplay, CSV content or art ownership changed.

Validation: graphics-enabled Unity ShelfCapture passed its existing dialogue paging/final-action, service, recipe, shelf and world routing checks. Reviewed service-review.png with all three roles and dialogue navigation; copied to Data/Previews/2026-10-29-Actions. Keyboard focus/disabled visuals were implemented but not manually exercised in a full playthrough; WebGL unverified.

### UI motion pass 1 — 2026-10-30 (Claude)
- **Screen cross-fade:** `Refresh` no longer destroys the old screen at once. When the place or an overlay changes (the same contexts `AnimateNavigation` already tracks), the old screen stays underneath, not clickable, while the new one fades in over `BistroUITheme.screenFadeSeconds` (0.16 s), then is removed. Opening and closing panels therefore animate both ways. Rebuilds without a context change (adding to the cart, moving a card) still swap instantly. A new click during a fade finishes it at once. `reducedMotion` turns it off (the capture harness already sets it).
- **One button motion family:** `BistroShelfStyle.Button` and `BistroActionButton` now get `BistroMotion` (hover lift, press squash, release settle), like `BistroWidgets.Button` already did.
- **Fix (same day, Owen's playtest): too much was animating.** The motion context used to include the chosen recipe, customer, dish acknowledgement and dialogue sequence, so each of those replayed the reveal on the whole room background (zoom + flash) and cross-faded the entire screen, top bar included. Now only a change of *place* (phase, store, shelf, menu) cross-fades the screen. Inside a place, the rebuild compares top-level pieces by name: new ones (a panel, the dialogue box) fade and pop in, removed ones fade out as a short-lived copy, and everything else (top bar, room, customer) swaps in the same frame with no visible change. Backgrounds never scale.
- Next slices: per-element keys below the top level (cards, slots, the cooking workspace) so those can animate individually, then true update-in-place for the busiest screens.

### Cart panel is the cart review — 2026-10-30 (Claude, Owen's request)
- On a shelf, the right-hand cart panel replaces the separate review: its rows scroll (vertical swipe) and can be dragged back onto the shelf to remove one package (`BistroDragChip`, kind `remove-cart`; the shelf viewport has a `BistroShelfCartTarget`). "Review cart" was removed. **Checkout** opens one confirmation ("Buy N packages for $X? You will have $Y left.") in the shared dialogue box, then buys. An over-budget cart shows a note instead of a confirmation that would fail.
- Drop targets accept any `IBistroDragSource` (food cards and cart rows), declared in `BistroShelfCartTarget.cs`.
- The top-bar Cart button still opens `IllustratedCartReview`, which is useful from the city or a store overview.

### Slots hold draggable rows — 2026-10-30 (Claude, Owen's request)
- Cooking slots, sorting bins and the pantry SELL slot no longer show seated mini cards or open a panel on click. `BistroDropSlot.ShowRows` lists their contents as small scrollable rows (thumbnail + name ×count). Drag a row onto another slot to move it there (sorting: kind `sort`; cooking: kind `meal-move`, key `slotIndex|foodId`, moves one serving), or release it anywhere that is not a drop area to take it out (`BistroDragChip.DroppedOutside`: un-sort, put one serving back, keep one serving). Releasing it on its own slot does nothing. Cart rows on the shelf follow the same rule.
- Removed: `IllustratedSortBin`, `MealModal`, the `pantry-sell` panel, `MealSlotTitle`. The generic `Modal()` panel now only serves Pantry. Lesson text in T_SHOPPING and T_COOKING updated to the drag-out wording.
- (same day, Owen) Drag vs scroll split by input: any click-drag on a row picks it up (no more "mostly vertical = scroll" guess, which swallowed quick drags); slot lists scroll with the mouse wheel only (`BistroWheelScroll`, a ScrollRect that ignores click-drags). The city map and card shelves keep normal drag-to-scroll.

Event UI exception (2026-10-30): event details/Codex use a layered newspaper and full-scene dimmer, not dialogue. CSV colours and art determine the page; toolbar/slot code is unchanged. A cached subtle RawImage paper finish ignores raycasts. NewsReading explicitly sets dark ink and initial scroll position.

### Text motion — 2026-10-30 (Claude)
- `BistroTextMotion` (legacy `Text`): **typewriter** (letters appear one by one; hidden letters are drawn transparent via a colour tag so wrapping never jumps) and **pop** (quick scale-settle). Start times are kept by the screen, so a rebuild mid-animation continues instead of restarting.
- Dialogue box: each page types at 55 characters/s with short pauses after punctuation. Clicking the box or the main button while typing shows the whole page; the next click advances.
- Top bar: a label animates only when its text changes. The place name types in (90 characters/s); Day, Popularity, Wallet and Cart do a small pop. Unchanged labels never animate on rebuilds.
- `reducedMotion` shows everything instantly. If the UI moves to TextMeshPro later, per-letter effects (hop, wave, colour) can replace this with the same start-time approach.

### Text and layout pass — 2026-10-30 (Claude, Owen's request)
- Dialogue pages break only between sentences and are balanced (`BistroDialoguePages`: fewest pages that fit ~4 lines of ~95 characters, then the most even split; word breaks only for an overlong sentence).
- Cooking header: left-aligned two bands, guest (name in the title font, request, muted stats line) then dish (name, "Base price · Plate appeal"), with a thin rule between; buttons aligned to the dish band.
- Shelf and pantry cards show a small "Appeal N" tag top-right on the front (logo stays top-left); the back still lists it.
- Shorter player-facing messages: no "events begin on day 2", no internal notes on publicity, no per-serving notice, "Hoping for" / "No special request today" for requests, farmer aid says "Use it today".

### City map: wheel zoom and elastic edges — 2026-10-30 (Claude, Owen's request)
- Click-drag pans; the edges are elastic (`ScrollRect.MovementType.Elastic`, elasticity 0.12): dragging past an edge gets harder the further you pull and the map springs back on release. The mouse wheel zooms toward the cursor between 1× and 2.6× with easing (`BistroMapZoom`), and no longer scrolls. Zoom level and position are kept across rebuilds and reset on a new game. Touch pinch-zoom is not added yet (needed for the phone build).

### Reusable illustrated panels — 2026-11-20
- Six transparent PNGs under Assets/Art/UI: panelPaper (general paper), guideNotePanel (sage left strip/corner sprig), planPanel (teal header), suggestionFrame (transparent centre), soldOutStamp (blank clay outline), newTag (blank coral sticker). All text remains live. Theme Sprite fields, not a runtime JSON/ID lookup, own these screen assets.
- BistroShelfStyle.PaperPanel creates a resizable paper surface; ApplySurface dresses an existing Image while preserving raycast/layout settings. It uses nine-slicing and a pixels-per-unit multiplier of 4 to keep high-resolution borders modest on screen. Missing sprites preserve existing plain fallbacks. Use these helpers for future neutral panels; do not indiscriminately replace buttons, colour-coded tiles, dark dialogue, or newspapers.
- Applied to guide chatter, the expanded/collapsed plan, suggestion frame, recipe-book shell, kitchen/sorting workspace, cooking workspace, cart/review, and generic legacy menu backgrounds (including pantry). New badges use the sticker; unavailable shelf cards get a stamp with live text on the front only and retain disabled/dimmed behaviour. No tutorial rules, timings, planning rules, event effects or inventory semantics changed.
- BistroArtImport defaults to full-rect, uncompressed UI textures with authored borders. Later Inspector changes survive normal reimport. Tools > Blue Zone Bistro > Configure illustrated panel borders explicitly restores these six defaults. GameBootstrapSetup auto-fills empty theme slots from matching filenames; explicit references are also saved in the theme.
- Verified in disposable Tools/UI/ReviewProject: Unity compilation and PanelArtCapture passed; reviewed plan/guide/suggestion, recipe-book/New, and stamp previews in Data/Previews/2026-11-20-Panels. This is an Edit Mode render, not manual Play Mode/WebGL interaction. The initial restricted launch could not access Unity licensing/cache; the normal-permission retry completed. Test-ContentImport passed. Test-GameOperations and Test-CsvContent remain blocked by the pre-existing float-to-int error at Tools/ComfortFatigueChecks.cs:48.

## Shelf sorting (2026-11-20)
A "Sort" pill above each aisle opens options: Shelf order, Price per serving, Appeal, Food type. Pressing the active option again reverses it; sold-out cards always go last. The shelf rebuilds as usual and `BistroSortSlide` slides each card from its previous grid spot to the new one (0.5 s ease-out, skipped with reduced motion). It is visual only: if the grid rebuilds mid-slide, cards simply land early. State lives in `BistroUIScreen.Sort.cs` (per session, not saved).

Map name tags (2026-11-20): the store/News/Bistro name tag on the city map is clickable and does the same as clicking the area.

### Web panel footprint — 2026-11-20
Supersedes the uncompressed texture default above: six new panel sprites use normal compression, 2048 default max size and explicit WebGL override at 1024, normal quality. PNG originals and authored borders are retained. The first 2D-only disposable render passed after compression; Web-target verification is recorded in SubmissionCleanup_20261120.md.

### Main-menu sound controls (2026-11-22)
User kept the existing Claude-generated music; both audition tracks remain external and unapproved. Main menu now has a separate matching sage sound panel with Background music and Sound effects sliders, percentages, and 0% mute. Uses existing BistroAudio PlayerPrefs keys; changes apply immediately without rebuilding UI or altering the CSV. Preference writes are debounced 0.35 seconds and flushed on pause/quit/destruction. Active effects and outgoing crossfade music now respect master volume changes immediately. No gameplay/save-progress ownership changes.
