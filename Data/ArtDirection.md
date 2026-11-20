# Visual direction

## Current production pipeline — 2026-10-28

This section supersedes historical atlas/JSON integration notes below. Art now uses **one PNG per item**, with CSV path columns imported into Sprite references on food, recipe and store assets. Category icons come from Categories.csv. Screen-wide room/counter/rail/plaques use BistroUITheme asset slots. Do not rebuild the old runtime JSON art catalogues or coordinate-based sprite sheets. `world.json` remains only for click-region layout, not image selection. See AGENTS.md “Where art lives” for exact paths and ownership.

The selected bistro and city are already integrated into the live Canvas UI. Historical “not yet assigned” statements below describe earlier milestones. The current service uses a compact dark bottom dialogue fade, original-placeholder customer silhouette behind the counter, and illustrated cooking cards/slots. Keep Claude's later toolbar, sorting, one-dialogue-at-a-time routing, and active-lesson-only Help changes.

## Approved city reference

`Assets/Art/Concepts/City_Approved_Direction.png` is the user-approved city concept (2026-10-21). It is saved for reference, not yet assigned to the live scene.

Elevated oblique city view; simplified low-poly building silhouettes; restrained roof edges and facade detail; muted cool grey and warm stone; moderate soft shading; very sparse greenery; no red glow. Background should remain subordinate to separate interactive store markers. Earlier concepts were either too detailed or too featureless.

## Store composition

Elevated roofless/cutaway interior, bounded panning, clear selectable shelf/aisle zones. Slightly richer colour than the city; material identity varies by store (wood, polished pale stone, practical warehouse fixtures). Avoid dense product-label detail and walls obscuring interaction targets. Keep store and shelf IDs independent of 2D hotspots or eventual 3D colliders. Serving-view composition is recorded below.

## Approved bistro reference

Assets/Art/Concepts/Bistro_Approved_Direction.png is the user-selected service composition: the earlier muted sage bistro with grey-blue exterior, not the subsequent white/misty or narrower variants. Fixed view behind foreground counter toward glass entrance; separate customer upper-body and counter layers planned. Image is a flattened concept, not yet assigned to live UI.



## Store prototypes — approved initial directions

Saved under Assets/Art/Concepts/Store_S001_Verdant_Prototype.png, Store_S002_CheapCart_Prototype.png, and Store_S003_MegaBulk_Prototype.png. Same elevated cutaway framing, palette changes by store. Based on original plan store table and current Stores.csv shelf_definitions:
- S001: PRODUCE, WELLNESS, LUXURY.
- S002: EYE_LEVEL, BARGAIN_BIN, BOTTOM_SHELF, BACK_FREEZER.
- S003: PALLETS, BULK_SACKS, BULK_PRODUCE (shelf added 2026-10-22 for giant produce packs that spoil; the current MegaBulk prototype image shows only pallets and sacks).

The user approved all three as initial visual directions on 2026-10-22; later revisions remain possible. This is user art-direction approval, not supervisor or release approval. These are flattened art prototypes only, not wired to UI or exact product placement. Signs are baked into concept images; final interactive labels should be separate UI driven by shelf IDs. Camera matching is approximate across generated images. Product dressing is illustrative.



## City direction update — 2026-10-25 (takes precedence over earlier city reference)

User returned to the first, more detailed city style, supplied as clipboard-c54d41a5-496b-47f3-92be-855c6b75908b.png. Preserved as Assets/Art/Concepts/City_Detailed_Reference_20261025.png. Keep earlier City_Approved_Direction.png for history, not as the latest density preference. Zoomed-in panning will reduce apparent crowding compared with a full-map overview.

Requested direction, not yet implemented:
- Muted low-saturation/low-contrast city with some parks, greenery, water and a bridge.
- Natural boundary cues before camera limits: waterfront/beach and/or progressively smaller, sparser buildings toward rural outskirts.
- Five future click targets: three stores (Verdant, CheapCart, MegaBulk), player's bistro, and tallest landmark signal tower. Slightly stronger color/detail plus distinct silhouettes for recognition. Tower should feel sophisticated and technological without clashing with the city.
- Tower opens news; explore tower-anchored notification previews. Existing screen-edge news remains a possible fallback when tower is offscreen.
- Bistro is a proposed entry to service; retain existing checkout/sorting/tutorial/phase guards, never bypass them for visual navigation.
- Later pan feedback: progressively resisted overscroll around 30 screen/UI pixels, capped extension, gentle return after release. Keep this resolution-aware and provide scenery padding beyond legal bounds. No camera or input changes in this concept pass.
- Stable landmark IDs and map coordinates should remain independent of sprites versus future 3D colliders. HUD remains screen-fixed.

Generated review candidates (NOT yet selected/approved):
- City_Waterfront_Prototype_A.png: peninsula/waterfront, bridge, parks and central signal tower.
- City_Greenbelt_Prototype_B.png: lakefront plus rural density transition, tallest tower and five landmarks.
Both are under Assets/Art/Concepts. They are flattened composition studies; text/product/building dressing is illustrative. Both are greener/more detailed than the source, and B includes an additional bridge exiting the frame: final boundaries should not promise unexplorable destinations. Final labels and interactions must be separate UI.

Generation brief: use the user-supplied detailed miniature city as visual reference; elevated orthographic oblique camera, pale grey/stone buildings, diffuse white lighting, restrained sage greenery, five separated mildly saturated landmarks. A explores water around the district; B explores water plus greenbelt. No live game code or imported content was changed.

## Selected waterfront and food study — 2026-10-25, latest direction

User selected the detailed muted waterfront image supplied as clipboard-008bfff9-aec2-4005-99fd-7e7c16a49d24.png, now preserved as City_Waterfront_Selected_20261025.png in Assets/Art/Concepts. This supersedes earlier city candidates; do not use the subsequently simplified sandbox city as the selected reference.

Pinned visual intentions (not implemented):
- For the 2D placeholder, clickable areas follow projected building silhouettes. Author image-specific polygon hotspots using normalized map-image coordinates; share the image's pan/scale transform. Coordinates belong to presentation configuration, while targets retain stable IDs for later 3D colliders.
- Soft edge blur and separate cloud/mist layers; keep clickable targets and UI readable and do not let decorative overlays intercept input.
- Start subdued/misty/stressed, gradually shift toward clearer richer green/blue atmosphere with community wellbeing.
- User is exploring an orange/red commercial atmosphere for popular but low-wellbeing play. Suggested distinction, not finalized: wellbeing drives health palette/clarity, popularity drives activity/commercial accents; a two-axis look rather than using popularity as health.
- Explore cel/toon shading with discrete light/shadow bands in the eventual 3D city. A flat PNG can only approximate this through grading/posterization or authored repainting, not reproduce geometry-based lighting.
- Food icons: recognizable actual food shapes, no faces or exaggerated character anatomy; about 4–5 main colors per item, crisp colored outlines and hard-edged shadows, minimal texture. Keep color choice separate from Traffic Light answer coding.

Food_CelStyle_Study_01.png is a generated six-item review sheet (tomato, broccoli, bread, tofu, chips, soda), NOT approved individual production sprites. Prompt used the user's supplied cartoon plant image solely for limited-palette cel shading and readable contours, with no characters copied. Requested roughly 4–5 colors per icon, base plus crisp shadow, no gradients, consistent three-quarter angle. Output is a style exploration: bread/chips remain more detailed and palette/gradients exceed the strict target; simplify in a later pass if selected. No gameplay/UI code changed.

## Food approval and card composition proposal — 2026-10-25

User approved Food_CelStyle_Study_01.png as the food representation direction for later mass generation (despite earlier strict palette caveat).

FoodCard_StoreShelf_Study_01.png is an unapproved concept sheet: three store/shelf frames, two populated fronts and a broccoli back. Food icon is a separate layer in production. All text, price, quantities, logos and shelf badges should be composited by UI, not baked into one image per listing. Mock prices/servings are illustrative, not CSV edits. No nutrition-distribution table is implemented or implied.

Current identity confirmed in code: market/cart listingId; sorting foodId aggregated across today's purchased batches; meal foodId aggregated across usable inventory; detailed pantry inventoryEntryId with optional source listingId. Current BistroFoodCard still uses one theme.cardFrame, so automatic store/shelf theme resolution is a proposal, not already implemented.

Proposed presentation lookup: store theme + shelf overlay, optionally exact store/shelf override, then store/default fallback. Same-store/shelf listings reuse assets. New listings on existing shelves require only CSV/import; a new bespoke shelf look requires art registration too. Neutral pantry/batch theme; grouped meal/sorting cards must not falsely imply one source store when multiple listings contributed. Proposed grouped source display: neutral frame and optional multiple-source badges. Detailed pantry can show source when available; gifted batches may have no source listing.

The front carries food name/icon, store/shelf identity where applicable, existing marketing label, and context-specific quantity/price. Back carries ingredients, Daily Dozen, storage and optional Codex. No normal-play Traffic Light answers. Keep claim content sourced from approved authored data. This pass changed no UI code.

## Purchased cards and shelf browsing — latest 2026-10-25 refinement

User decision: purchased-food views (sorting, grouped meal selection, detailed inventory) share a Blue Zone Bistro pantry visual identity. Keep their different IDs and quantity aggregation rules; a shared skin does not merge inventory batches. Market/cart listing cards retain store/shelf styling. Do not repeat section names such as Produce, Back freezer or Bulk sacks on every card; show the current location once in the shelf header, convey source through visual treatment. Earlier full store/shelf card mockup remains provisional, not final art approval.

Shelf_Card_Study_01.png is a review concept sheet with Verdant produce, CheapCart freezer and MegaBulk sacks. Orthographic straight-on shelving; cards are browsing items resting on repeated shelf rails, not gameplay drop slots. Intended implementation: fixed location header/icon, clipped vertically scrolling content, responsive columns and repeatable row backgrounds/rails. Number of rows follows listing count, no fixed painted inventory. Decorative shelf art should not intercept card input. Scrolling and card dragging need distinct gestures (wheel/scrollbar/empty-area drag versus card drag) to avoid accidental purchases. Missing shelf artwork uses a generic store style.

Concept generated from user shelf photo for straight-on composition and FoodCard_StoreShelf_Study_01.png for card style, with simplified borders, food names above icons, servings/price below, no repeated location labels on cards. Mock foods, prices and quantities are illustrative, not imported content; some food illustrations are more detailed than the selected style. All UI text, product icons and rails should be separate runtime elements, not a single baked screen image. No UI/gameplay code changed.

## Shelf approval and distinct store architecture — 2026-10-25

User selected the full Verdant shelf-menu mockup (generated source exec-e62fe4c5-9342-41a5-9a88-5083a54e79e7.png; user clipboard-4bd7f139-6d10-4108-8a1a-e05edc74a7a6.png). Its muted colours, layered cards and richer food detail take precedence over the subsequently oversimplified, brighter CheapCart freezer mockup, which was rejected. Carry Verdant's harmony/detail across store themes. Fixed screen-level navigation and cart; scrolling shelf content. This is visual approval only, not live implementation.

New review candidates, not yet user-approved, saved in Assets/Art/Concepts:
- Store_S001_Verdant_Architecture_v2.png: arched conservatory pavilion, curved produce arrangement, Wellness alcove, Luxury bay.
- Store_S002_CheapCart_Architecture_v2.png: practical rectangular discounter, four visible targets: Eye-level shelves, Bargain bin, Bottom shelf, Back freezer. Muted ochre accents instead of harsh red/yellow.
- Store_S003_MegaBulk_Architecture_v2.png: timber/steel warehouse racks, darker concrete, broad loading lane, Pallet displays, Bulk sacks, Bulk produce.

Generated with built-in image generation, one image per store. Prompt/reference brief: supplied older Verdant interior (clipboard-4fe17c69-fa70-4aa4-98c2-666c1d22f99c.png) for soft light, harmonious muted palette and moderate detail; City_Waterfront_Selected_20261025.png for exterior identity. Common elevated orthographic roofless cutaway camera; distinct floor plans rather than recoloured identical rooms; slightly richer shopping zones and restrained section signs. Store-specific prompts requested arched glazing and pale stone/oak for Verdant, compact parallel gondolas and low staples for CheapCart, deep timber bays/charcoal concrete/bulk crates for MegaBulk.

These are flattened architecture studies, not exact map-matched geometry. Camera, footprint and facade alignment still need reconciliation for a seamless zoom/roof-removal transition; 2D can approximate with a transition, actual continuous reveal needs matching layers or 3D. Generated signs/logos (including incidental leaf motifs) and product dressing are illustrative, not final branding or health claims. Labels/hotspots must remain separate, keyed to existing shelf IDs. Clickability and occlusion need interactive validation later. Old approved initial references retained. No gameplay, CSV or UI code changed; no gameplay tests needed for this concept-only pass.

## Verdant produce first implementation batch — 2026-10-25

The user selected the midpoint bistro brightness (Bistro_Midtone_Selected_20261025.png) as the current service direction, retaining the earlier camera/composition. This supersedes earlier brightness studies without deleting them.

Character direction pinned only: original simple anime/chibi-inspired 2D upper-body portraits with bold readable silhouettes, restrained flat colours, swappable expressions and later 2D animation over the low-poly world. User supplied Photo 1.jpg and Photo 2.jpg as third-party mood references, not licensed production characters. Develop original designs from current character descriptions; do not assume every customer is elderly or muscular. No character art or behaviour implemented in this batch.

First produce art batch: Assets/Resources/BistroArt/VerdantProduce.png, with presentation-only tile mapping in catalog.json. Ten actual S001/PRODUCE food IDs, including day-4 purple sweet potato. Generated with built-in imagegen using approved Verdant shelf mockup exec-e62fe4c5-9342-41a5-9a88-5083a54e79e7.png as reference. Prompt: exact 5-column/2-row edge-to-edge atlas, separate muted food/background compositions, no lettering, calm lower area for live text. Order: frozen berries, whole-grain bread, spinach, tomato, banana; acai powder, goji berries, sweet potato, purple sweet potato, kale. Output has more texture than requested in bread/berries and remains a review batch. Atlas contains food plus backdrop, not separate transparent icons yet; do not present it as final reusable isolated food sprites.

Runtime shelf is a first integration rather than pixel-identical mockup: existing global header and cart-review modal retained, cream cart panel, sage shelf and repeatable oak rails added. Food/name/price remain separate from illustration; no content CSV or imported gameplay asset modified. No city/store background integration or clickable marker work in this pass.

### Shelf correction after user comparison

The first live batch was rejected as too close to the old block UI, with missing illustrations. The revised implementation follows the approved Verdant full-screen shelf composition: cream brand/navigation bar, four large portrait cards per row, overlapping serif name plaques, servings/price footer, continuous timber rails, sage backing and cream illustrated cart. The cart review uses the same presentation. Actual CSV products and prices are retained, rather than the illustrative mockup inventory.

Revised atlas source: exec-d1d73899-e34f-4072-a662-4af9a097ca0e.png (built-in image generation). Brief: simplify previous atlas to broad cel-colour shapes, around four/five colours per food, retain the same 5x2 order, muted contextual backgrounds, no text. Bread/berry detail is still somewhat richer than the target and awaits user review. This replaces the first atlas at its stable resource path. City, stores and character work are unchanged.

Typography: Lora Bold from cyrealtype/Lora-Cyrillic fonts/ttf/Lora-Bold.ttf; SIL Open Font License included beside it. Body uses the project's LiberationSans font with its included OFL licence. Font and artwork provenance is recorded in ASSET_LICENSES.md. Latest verified renders: Data/Previews/VerdantShelf.png and VerdantCart.png. These show an isolated day-two fixture, not the player's save.

### Strict simplified reference correction (2026-10-26)

User rejected the detailed food revision and specified clipboard-72195476-2837-4952-b4f2-45ca0b354507.png as the strict target: broad flat food colours (roughly four/five tones), no noisy textures, gradients allowed on backgrounds, slightly irregular plaques, thin shadows and small foreground overlaps.

Current atlas source: exec-532d5b7d-5bce-4d60-bcbd-10859533d5c9.png, built-in image generation. Prompt brief: exact 5x2 portrait food grid in current content order; flat limited-tone foods, no pores/seeds/powder speckles, brighter upper/darker lower backgrounds, contextual silhouettes, no text. Corrected tomato star-shaped calyx and mixed berries after an interim generation confused fruit identities. Earlier detailed exec-e46063f1-dc00-48f8-8808-602525f884f6.png is rejected and not the runtime atlas.

Decoration source: exec-e5776e03-3dc1-4bff-b48c-10dad6dee12a.png. Prompt: transparent 3x2 flat sprite sheet containing white leaf logo, white cart silhouette, ivory/dark irregular plaques, pale oak rail and simple green tomato leaves. Imported to ShelfDecor.png; source rects in decor.json. These are separate runtime art layers, not baked text. Review the final Unity render before declaring this batch approved; generated food colour counts are an artistic target, not a mechanically enforced palette.

Final captures use a day-one isolated session to avoid random day-two event closures obscuring the visual comparison; this supersedes the earlier day-two fixture note.

### Muted palette and expanded shelves (2026-10-26)

User accepted the simpler shapes but requested foreground saturation about20% lower and backgrounds about40–50% lower. Current Verdant atlas uses exec-1ee554db-c59a-4739-bc70-2b02779600c8.png: colour-only brief preserving compositions, dusty teal behind coral tomatoes, subdued complementary backgrounds, gentle gradients. These percentages are art-direction targets, not measured palette transforms.

New runtime atlases (all built-in image generation, complete food/background compositions):
- Staples.png: exec-c5873b1a-ab24-413a-a054-e6f331b207f4.png. Oats, canned beans, frozen vegetables, dry beans, brown rice; cabbage, tofu, apple, broccoli, carrots. Final edit used accepted muted Verdant atlas as strict flat-style reference, removing realistic texture from an initial rejected draft.
- DryGoods.png: exec-2a1438ad-89d1-449b-8cda-09e4e244278b.png. Lentils, chickpeas, quinoa, flaxseed; walnuts, pumpkin seeds, turmeric, water. Same strict flat-style reference and detail reduction.
- EdamameBarley.png: exec-460fa563-96bd-4184-92d0-fceff57441ec.png. Two complete muted flat compositions, edamame and barley.

Prompts requested four/five broad tones per food, no photorealism or granular textures, strongly muted gradient backdrops, no baked text, and calm lower areas for labels. These remain reviewable production placeholders. CheapCart uses subdued neutral/cool shelf colours, MegaBulk muted brown/sage; no invented premium leaf branding on those stores. The five shelves share geometry and reuse the same food art by food ID.

### Contextual backgrounds and card-back companions (2026-10-26)

User requested consistent moderate detail: expressive broad highlights/shadows, about four/five principal food tones, muted saturation, no granular realism. Backgrounds must describe their own food rather than repeat generic foliage. Produce fronts were revised with berry circles, wheat ears, spinach leaves, tomato vines, banana curves and potato silhouettes. Existing Staples/DryGoods fronts are retained; their new reverse companions have specific motifs.

OpenAI image-generation sources (reviewable placeholders):
- VerdantProduce.png: exec-8eb56ceb-0340-40fc-92d5-d119637fbbbc.png.
- ProduceBacks.png: exec-62808c21-87e2-4135-8022-ea111c5fcc8d.png.
- StapleBacks.png: exec-3ac0eb0b-bbc2-4977-8830-2d1806d23286.png.
- Wellness.png: exec-3f1876d5-3929-4e7f-9d29-789a055331a7.png.
- WellnessBacks.png: exec-8c249cc5-8b2b-40c0-9686-62e9b170cb9a.png.
- DryGoodsBacks.png: exec-7040fb90-309a-4b7e-9d7a-51b17eae4896.png.
- EdamameBarleyBacks.png: exec-81a0f08f-d424-4337-a188-b25c6ff00ee9.png.

Brief for companions: preserve the corresponding front palette, simple food-specific silhouettes around edges, gentle gradient and quiet centre for information, no text or foreground food. Wellness front brief: same limited-tone illustrative language, composed food/background together, no health claims. Eight foods: chicken, olive oil, eggs, salmon, beef, protein bar, allulose dessert and cheese. These illustrations do not communicate or change nutritional classifications.

### Remaining food coverage (2026-10-26)

Added 25 illustrated fronts and matching background-only backs for the remaining Luxury, Eye Level, Bargain Bin and Pallets listings. Complete composed food/background tiles; dynamic title, servings, prices and plaque layers remain in UI. New resources and OpenAI generated sources:
- Everyday.png: exec-09d01deb-93ff-45fb-b088-6e23b587abfb.png; EverydayBacks.png: exec-d48177de-866e-4496-90d4-d58cd918b04a.png.
- Treats.png: exec-e02c3464-c6ee-4b42-a9bf-72a03204021a.png; TreatsBacks.png: exec-3293ebd5-4055-44bf-9c34-6e8eeed41a2c.png.
- Luxury.png: exec-153d56ba-5b51-49c2-a35b-ed26d154ead0.png; LuxuryBacks.png: exec-f5ce0271-9ba5-4401-9a28-1514ee0455b5.png.

Style reference: existing VerdantProduce.png. Generation brief: equal portrait tiles, food-specific muted gradient/silhouette background, broad cel highlights and shadows, four/five principal food tones, no baked text/claims/logos, quiet lower area for labels. Front drafts were edited to remove crumb/powder/pore textures, simplify noodle loops/cereal rings and sourdough/truffle detail, and reduce saturation. First drafts exec-a1a35372-585b-4ba5-b99d-f57e14f0addc.png, exec-86751a49-39cc-49c9-9c2a-2e989071381f.png and exec-7f5569e0-477b-48ba-a583-965f26b3942e.png are not runtime assets. Reverse brief: remove foreground food/container, retain food-specific edge motifs and quiet middle. Colour-count/saturation requests are artistic targets, not exact measured palette limits. Final approval remains with user.

### City/store navigation integration (2026-10-26)

Runtime city.png copies City_Waterfront_Selected_20261025.png; S001/S002/S003.png copy the respective Store_*_Architecture_v2.png concepts. Originals are retained. world.json contains approximate hand-authored convex projected hit polygons and separate UI labels, keyed to stable IDs. These flattened concepts include baked illustrative signage, not seamless matching 3D geometry.

StoreLogos.png: OpenAI-generated exec-93c8ff11-95a7-4410-bde1-833a86846d71.png. Brief: transparent white silhouette shopping basket/tag for CheapCart and stacked boxes for MegaBulk, no text, broad readable shapes. First draft exec-4239dbea-5ce8-45f1-8acd-3cdb6d89d4c0.png not shipped. Existing Verdant leaf retained. All are prototype branding pending visual review.

Recipe art production (2026-10-27): built-in image generation produced Recipes.png (source exec-50398f94-67d7-4f1f-a78c-a11423e2b156.png) and Categories.png (source exec-5e38e141-4eee-48de-918f-09e7816ce3da.png), following the approved recipe front/back concept. Prompt direction: equal 3x5 dish atlas in CSV order, muted gradient backgrounds and food-specific motifs, cel-shaded illustrative dishes, lower third reserved for live UI, no text. Category prompt: 4x3 cream icon atlas in enum order Beans, Fruit, Vegetable, Grain, StarchyVegetable, Meat, MeatSubstitute, Dairy, Egg, Fat, Beverage, Other; flat simple icons, no labels/health symbols. Generated output is more detailed than the original limited-colour target and remains reviewable. No changes to approved city/store art.

### Service placeholder integration (2026-10-27)
Selected Bistro_Midtone_Selected_20261025.png is copied without alteration to Resources/BistroArt/ServiceRoom.png. Background and foreground counter are separate UI image layers referencing the same image; the counter uses its bottom 30% crop. No new generated art this pass. Central silhouette is temporary. Recipe backs use gradient-only atlas crops, removing the dish silhouette.


### Compact dialogue and cooking update — 2026-10-28
User rejected the misty cream gradient panels and full-page guide. `BistroUIScreen.DialoguePanel.cs` now provides a shared bottom dark fade, speaker/name column, light text, pagination, final action and optional Back. Dialogue, tutorial instructions, guest information, dish review/result, day summary and return-to-menu confirmation use it. Browsing interfaces remain card/grid views.

Cooking buttons and ingredient slots use the illustrated palette; seated ingredient miniatures use their food artwork. The persistent “Comfort needs…” line was removed. The existing CSV request lesson explains Comfort using `{comfort}`, expanded from `BalanceConfig` through `IntroMealPlan`; no scoring rule changed. Tutorial review reads encountered lesson text without replaying actions. City Recenter was removed and replaced with Tutorial; toolbar Help is always available.

Recipe backs keep background-only art. Customer silhouette is still a placeholder. Future 3D replacement must retain the independent overlay structure. Earlier notes approving the cream dialogue veil are superseded by this decision.

Latest visual verification (2026-10-28): Data/Previews/2026-10-28-CSV contains offscreen captures using the authored theme and CSV-imported individual sprites. These supersede the earlier JSON-atlas service previews. Dialogue/guide text occupies the dark bottom region; cooking retains category icons and illustrated miniature ingredients. This is implementation verification, not final user art approval.

## Recipe background-only asset pass — 2026-10-28

Replaced Assets/Art/Recipes/R001_back.png through R015_back.png in place, keeping existing metas and CSV references. Each was generated separately; no atlas or JSON art lookup. The corresponding Rxxx_dish.png served as hue/motif reference (R001 used the sandwich description). No dish fronts changed. These are implemented reviewable assets, not a new claim of final art approval.

Prompt direction: full-bleed portrait 3:4 background-only cooking-card texture; muted hue with a lighter quiet upper center and slightly darker bottom; three closely related colors plus background gradient; simple ingredient silhouettes at outer edges; central 75% quiet enough for dark text; no plate, bowl, meal, lettering, white panels, border, grain or realistic texture.

| Recipe | Palette | Motifs |
|---|---|---|
| R001 | Terracotta | Tomato leaves and tomatoes |
| R002 | Sage teal | Beans and grain/leaf sprigs |
| R003 | Ochre | Sweet potatoes and greens |
| R004 | Lavender | Oats and berries |
| R005 | Sage | Parsley, beans and tomatoes |
| R006 | Blue teal | Tofu cubes and broccoli |
| R007 | Olive | Peppers and beans |
| R008 | Mauve | Figs and berries |
| R009 | Golden ochre | Chickpeas and carrot leaves |
| R010 | Dusty rose | Strawberries and oats |
| R011 | Slate blue | Cucumber slices and seeds |
| R012 | Ochre | Potatoes and chives |
| R013 | Lavender | Citrus and leaves |
| R014 | Terracotta | Tomatoes and lettuce |
| R015 | Olive sage | Potato wedges and herbs |

Start menu now exposes more of the selected city behind a menu-only soft blur, with a sage panel and authored leaf accent. The playable map uses a larger default view (1.4x cover). This presentation remains independent of future low-poly scenes.

### Toolbar, markers and sorting surfaces (2026-10-29)
The global toolbar and map labels now use subtle vertical shading, fine accent rules and the existing authored botanical sprig. Toolbar opacity is 90%; active map labels are 89%, closed labels 85%. Map marker icons use StoreData.logo when available, with a speech-bubble fallback for News; hotspot geometry and navigation remain unchanged. Sorting workspaces and bins receive the same restrained surface treatment without changing their placement or classification rules.

BistroShelfIcon keeps the existing semantic shapes but rasterizes their union at 4x sampling into cached 128px alpha textures with mipmaps/trilinear filtering. Circle outlines use 64 segments. This avoids hard mesh-edge aliasing without AI-generated replacements or per-food art lookups. Assigned sprite icons still take precedence. Decorations never receive raycasts. BistroSurface changes only panel vertex tint; all food/recipe/store art continues to come from CSV sprite fields.

### Shared contextual action buttons (2026-10-29)
BistroActionButton.Create accepts an explicit BistroActionRole (Primary, Secondary, Caution), caller-owned bounds, label and callback. Theme actionPrimary/actionSecondary/actionCaution/actionInk fields control the palette: muted sage, warm cream, soft clay and dark ink by default. This replaces inherited deep-green blocks for cooking actions, dialogue Next/Previous/Back, optional event-dialogue actions, sorting submission and continuation. Next/continue/review are primary; back/recipes/previous are secondary; skip customer is caution. There is no label-string inference.

All roles share rounded geometry, subtle gradient, thin rim, upper highlight and light shadow. Hover/pressed/disabled use Selectable colors; selection adds a focus rim; press reduces the shadow. Reduced motion disables color-fade timing. Existing main-menu controls, toolbar items and world indicators retain their separate designs. No gameplay, CSV content or art ownership changed.

Validation: graphics-enabled Unity ShelfCapture passed its existing dialogue paging/final-action, service, recipe, shelf and world routing checks. Reviewed service-review.png with all three roles and dialogue navigation; copied to Data/Previews/2026-10-29-Actions. Keyboard focus/disabled visuals were implemented but not manually exercised in a full playthrough; WebGL unverified.

### Ending placeholder batch (2026-10-30)
Reference: Art/UI/cityBackground.png, the selected waterfront city. Prompt direction: a recognizable cream waterfront corner bistro with teal frames, medium-wide elevated three-quarter view, simple low-poly-like forms, muted colour and quiet lower area for dialogue; no characters, logos or baked text. Five individual outputs: Blue (blue/green, planted and flourishing), Popular (coral sunset, expanded terraces), Niche (pale cool mist, sparse tables), Bankrupt (shutter, stacked chairs, small facade crack), Struggling (gray, sparse and weary but still open). Integrated as review placeholders; architecture varies slightly between generated variants and awaits user art review.

### Event editorial icons (2026-10-30)
Built-in image generation, one transparent PNG per event, saved in Assets/Art/Events/EV_<ID>_icon.png. User's retro poster reference guided the paper/editorial composition; imagery is original. Current compact and expanded views reuse the same simple illustration through separate CSV slots.

Shared prompt: square transparent production game UI illustration; retro newspaper editorial cut-paper icon; bold simple silhouette; four or five flat muted colours; restrained printed grain within shapes; no gradients, realistic shading, 3D, tiny details, words, letters, numbers, border, background rectangle or humans; one balanced central emblem readable as a thumbnail.

Subjects:
- SALE: A simple ochre grocery bag with two chunky vegetables peeking out, one large downward price arrow and a blank hanging sale tag. Muted ochre, sage and charcoal.
- OATS: A single recognizable oat stalk and small bowl of rolled oats, next to one bold rising price chart arrow. Muted wheat gold, dusty blue and charcoal.
- CLOSED: A small sage grocery storefront with a large simple leaf emblem, shutter down and a crossed wrench symbol. Verdant maintenance closure. Sage, cream and charcoal.
- PUBLICITY: A small teal bistro storefront, three large upward arrows, and a nearby sage leaf seal representing the partner grocery store. Local newspaper publicity. Dusty teal, sage and warm cream.
- CHEAP_CLOSED: A small ochre convenience shop with closed shutter, two stacked cardboard boxes and a simple circular restocking arrow. Muted ochre, clay and charcoal.


## Event icon expansion (2026-11-19)
User postponed further 3D work to focus on gameplay, UX and content. Eight new event icons use EV_OATS_icon.png as the existing visual reference: transparent square, bold cut-paper silhouettes, four/five principal muted colours, restrained printed grain, no baked text or background rectangle. Individual built-in image_gen calls generated HARVEST, BEAN_WEEK, FESTIVAL, COLD_SNAP, WORKSHOP, FREEZER_DELAY, SWEET_POTATO and HEAT_WAVE. Preserved generated alpha and original pixel data. Saved under Assets/Art/Events and linked in Events.csv; still reviewable artwork. Exact prompts and source filenames: Data/EventArt_20261119.json. Unity reimport and runtime review remain pending (see Events.md).

### Reusable panel family — 2026-11-20
Generated six transparent UI assets with warm cream paper, quiet sage gradients and dusty-teal accents; a clay stamp and coral sticker provide status accents. User requested generated artwork with procedural sizing/reuse. Interior text regions stay quiet, frame centres transparent, words absent from source art. Built-in image generation; exact prompts and source paths in PanelArt_20261120.md. Unity nine-sliced renders reviewed in Data/Previews/2026-11-20-Panels; these are integrated review placeholders, not final approved production art.
