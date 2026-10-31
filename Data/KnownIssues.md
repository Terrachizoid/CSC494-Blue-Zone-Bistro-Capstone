# Known issues and backlog

Review of code, CSV content and guides on 2026-10-22 (state after the week 6 commit plus uncommitted events/endings/art work). Each item says where it lives, why it matters and the smallest fix. "CSV-only" means no code change; reimport afterwards.

See also `PlaytestReport_AI.md` (2026-10-24) for open design questions from AI playtests: no temptation toward trap foods, unusable eggs/nuggets, one-recipe dominance, and the moderation wording.

When you fix an item, delete it or mark it done with the date. When you find a new one, add it here.

**Update 2026-10-22 (evening):** a balance simulator now exists (`Tools/BalanceSim`). It corrected item 8 and drove the tuning in item 6. Done: items 6 (partly), 9, 16, 21. Postponed by user decision: items 4 and 5 (store layouts and back-freezer discovery come with the advanced store UI, after balancing).

## A. Nutrition framework (fix before any playtest or supervisor demo)

1. **[Done 2026-10-28: suggested text applied to T_SORTING — supervisor still approves final wording]** **Sorting tutorial uses "moderation" wording.** `Blue_Zone_Bistro_Tutorials.csv`, `T_SORTING`: "Yellow is for moderation, and Red is for occasional choices." Plan §2/§3.1 forbids moderation framing; yellow means "on the way out" and red means "avoid". CSV-only. Suggested text: "Green foods are the everyday foundation and the only foods that build health. Yellow foods are ones customers are moving away from. Red foods are the ones to avoid." Supervisor approves final wording.
2. **[Partly done 2026-10-28: Validate Imported Content warns (does not fail, Owen's call) on *moderation*, *heart-healthy*, *lean protein*, *healthy fat*, *good fat* in tutorial, dialogue and event text; a phrase right after a quotation mark counts as quoting a label. Claim/source rules still open]** **The content validator does not enforce the framework.** `BistroContentValidator` checks structure only. The plan asks the build to fail on: a claim with no source, player-facing text referencing an unapproved claim, a product with no Traffic Light. Mixed meals are a GDD decision, so the plan's "non-green recipe" rule does not apply. A small "framework lint" (forbidden words such as *moderation*, *heart-healthy*, *lean protein* in tutorial/dialogue/event text unless the line is flagged as a trap example) would catch item 1 automatically.
3. **No real claims yet.** `Blue_Zone_Bistro_Claims.csv` has two empty placeholders (C001, C002) and four game-rule explanations. Sorting explanations show a "pending" placeholder. This is the known research gap; the GDD needs only 2–3 approved examples.

## B. Gameplay design inconsistencies

4. **[Stock done 2026-10-22; discovery still postponed]** Prices.csv/Foods.csv restocked: every shelf populated (Verdant 10 produce / 7 wellness / 15 luxury — 7 from day 1, 8 more on day 4; CheapCart 7 eye-level, 6 bargain bin, 23 bottom shelf, 3 back freezer; MegaBulk 5 pallets, 4 bulk sacks, 7 bulk produce). 14 new foods (P041–P054) from the plan's examples, all Pending; 2026-10-24: 9 more luxury foods (P055–P063, green/yellow, expensive, appeal 7–9; listings L079–L087) and the keto dessert moved from LUXURY to WELLNESS so the luxury shelf holds only green/yellow premium foods (Owen's decision); plan trap labels applied to chicken, olive oil and nuggets. Salmon uses category Meat (no Fish category). Original note: **Store layouts did not teach the "each store misleads" lesson.** `Blue_Zone_Bistro_Prices.csv`:
   - CheapCart sells 35 of 54 listings, 30 of them on `BOTTOM_SHELF`, including chicken, olive oil, eggs, cheese and almond drink. Its `EYE_LEVEL` shelf has **no listings** and will render as an empty shelf.
   - Verdant's `LUXURY` shelf has no listings (luxury is a GDD non-goal). Its `WELLNESS` aisle has only chicken and olive oil.
   - MegaBulk's `PALLETS` hold green foods (edamame, purple sweet potato, tofu, broccoli), the opposite of the plan's "pallets of soda and chips, wins in the back sacks".
   - CheapCart is a one-stop shop with the lowest price for almost everything, so there is no reason to visit the other two stores.
   
   CSV-only fix: move junk to CheapCart `EYE_LEVEL`/`BARGAIN_BIN` and MegaBulk `PALLETS`; move chicken, eggs and olive oil to Verdant `WELLNESS` (or give Verdant a better price on some of them); keep CheapCart staples on `BOTTOM_SHELF`/`BACK_FREEZER`; remove `LUXURY` from Verdant's `shelf_definitions` or stock it with a couple of green luxuries. Do this **before** wiring store art hotspots, because the art labels the shelves.
5. **[Postponed: will be a clickable hidden spot in the advanced store UI]** **The back-freezer "discovery" is not in the game.** `Stores.csv` review note: freezer listings are available from day 1 "until discovery interaction exists". The plan's key affordability lesson ("squeeze resolved through knowledge") therefore never happens. Cheapest option: a `required_flag` on the back-freezer listings, set by June's day-2 dialogue hint (dialogue `set_flags`). Uses existing systems, no new code.
6. **[Done 2026-10-22: rent, threshold walk-ins, craving pressure and bankrupt ending implemented; see Economy.md]** **[Earlier note]** Simulator before tuning: any green player got E_BLUE in 300/300 runs, popularity hit its 1000 cap by day 5, money grew 30 → ~140 and was never short. Applied: `startingMoney` 30→15 and `popularityPerDelight` 5→3 in `BalanceConfig.asset`, recipe pay ×0.7 (now 4–5.5). Result: health-only → E_NICHE, informed/tasty → E_BLUE, junk → E_POPULAR, thrift → E_NICHE; money is tight on day 1, grows 15 → ~70. **Still open:** no knob available today makes money bind later, because the cheapest green dish costs ~1.5 and pays 4+. Needs a design choice: a daily overhead (small code change), money in an ending, or accept money as a light day-1 constraint. Original note: **Money stops mattering after day 1.** Cheapest green dishes cost about 1.2–2.0 in ingredients and pay 6–8 plus a 20% tip (4–6× margin). Starting money is 30. Money is not part of any ending. The squeeze now lives between **delight/popularity** (junk has appeal 8–9, many staples 3–4) and **health/wellbeing**, which is a good and arguably more fun tension. But then either (a) say so in the GDD and keep money as a light constraint, or (b) make money bite with one knob, e.g. a daily overhead or a lower `startingMoney`, or (c) add a money term to an ending. Decide before balance work.
7. **Junk food is not cheaper than green food.** Soda 0.50/serving vs water 0.12; chips 0.50 vs carrots 0.33; bacon 1.00 vs dry beans 0.42. The plan's lie "cheap means junk" is not represented. If money matters (item 6), make a few red items the cheapest per serving at eye level.
8. **[Corrected by simulation, not an issue]** A competent green player ends with customers averaging health ~71–75; 56–70% reach the top band and June ends ~78–82. The original estimate below was wrong. Varying `starting_health` is still optional flavour. Original note: **Customers barely change in six days.** All six start at health 50 (June with heart disease is the same as everyone else). A good dish gives about +5 to +10 health. With 1–3 seats a day shared by 4–6 customers, each customer eats 2–4 meals, so nobody reaches the high band, and nothing visual shows health yet. Options: vary `starting_health` (e.g. June 30, Sam 35), raise per-serving health weights, or both. Check with the CSV six-day simulation.
9. **[Done 2026-10-22: chef `min_day` = 4; Test-CsvContent now expects the chef first on day 4]** **The chef arrived a day before Okinawa content.** `D_CHEF_OKINAWA_ARRIVAL` has `min_day=3`; R003/R006 and MegaBulk unlock by `unlock_day=4`; the flag `okinawa_chef_arrived` gates nothing. Simplest: set the chef's `min_day` to 4 so the visit opens Chapter 2. CSV-only.
10. **Some yellow/red foods can never be part of a successful dish.** No recipe has an `Egg` or `MeatSubstitute` slot, so eggs and vegan nuggets can only be extras, which always fail the recipe. `Meat`/`Dairy` appear only as optional slots in R001 (Sandwich). That is fine if intended, but then eggs and nuggets work only as sorting questions. Either accept that and note it, or add optional slots.
11. **[Done 2026-10-28: refund is now 50% of each correctly sorted new food's own price; known foods earn nothing]** **Sorting refund applies to the whole cart.** `refund = cart total × 30% × accuracy`, where accuracy covers only new foods. One correctly sorted new food in a large cart refunds 30% of everything. Small exploit; acceptable, but consider basing the refund on the new foods' cost only.
12. **Prosperity is thin.** Finance only changes the tip percentage. Visit frequency and order tier (plan S6) are not linked to finance. Fine under the GDD's non-goals; just do not describe a "prosperity loop" in the demo.
13. **Shelf life differs from the plan** (fresh 2 days vs 3). Game tuning, not a defect; note it in the final GDD.
14. **Prices are synthetic.** `observed_price_cad` is empty in every row. The plan wanted a real 15-staple price table; the GDD drops large surveys. Filling about 10 rows from one flyer would let the demo say "ratios are grounded".

## C. Hard-coded values that should be data

15. **[Done 2026-10-22: the first lesson accepts any recipe on today's menu]** `IntroMealPlan.RecipeId = "R001"` (C# const). The tutorial recipe should be a column in `Tutorials.csv` or a field on `TutorialDatabase`. Note R001 is the only recipe with Meat/Dairy optional slots, so the very first lesson invites a sandwich with meat or cheese.
16. **[Done 2026-10-22: `unlock_popularity` column in Customers.csv → `CustomerData.unlockPopularity`; BalanceConfig list removed]** `BalanceConfig.customerPopularityUnlocks` defaults named U005/U006. Better as an `unlock_popularity` column in `Customers.csv`, so a new customer needs no code or asset edit.
17. Duplicate defaults (still open; note the isolated tests deliberately build `new BalanceConfig()` / `new GameSessionState()` and assert the C# defaults, e.g. money 30 and popularity ×5, so change code defaults and tests together): `GameSessionState` (`money=30`, `popularity=300`, `communityWellbeing=50`) and `CustomerState` (`delight=50`, `finance=20`, `healthAtLastDayEnd=50`) repeat BalanceConfig values. They are overwritten at creation today, but they will silently disagree after a tuning change. Set them to 0 / leave unset and always initialise from BalanceConfig.
18. UI layout and all player-facing UI strings are literals inside `BistroUIScreen*.cs` (see section E).
19. Event artwork reuses `Assets/Art/Endings/*` placeholders (documented). Replace when art exists.

## D. Code cleanup and optimisation

20. **[Done 2026-10-28: code, Stores.csv columns, tests and `PriceDisplayStyle.Membership` removed]** **Remove dormant membership code** (user decision: memberships cut). Touches `StoreData.requiresMembership/membershipCost`, `StoreCsvImporter`, `Stores.csv` columns, `GameSessionState.storeMembershipIds`, checks in `PurchaseService`, `CustomerVisitService`, `EventService`, `GreyboxUIController`, the null check in `CheckpointStore`, and `CheckpointSmokeTest`. JsonUtility ignores unknown fields, so old saves still load. Low risk, do it with item 21.
21. **[Done 2026-10-22; file emptied 2026-10-28 — delete `Assets/Scripts/UI/GreyboxUIController.cs` + `.meta` in Unity]** **Delete `GreyboxUIController`** (21 KB). It is created at startup only so `BistroUIScreen` can disable it. Remove its creation in `GameBootstrap.Awake` and the `fallback` parameter of `BistroUIScreen.Initialize`.
22. **Throwaway session at startup.** `GameBootstrap.Awake` calls `BuildSession()` on a default state before the menu, which rolls events and attendance for a session nobody plays. Harmless today; build the session only on New Game / Resume.
23. **Repeated object creation in hot-ish paths.** `new MarketEffects(state)` is created per listing inside loops (`CustomerVisitService.AvailableRecipes`, `EventService`, `IntroMealPlan.ShoppingFeasible` DP inner loop), and `PurchaseService.Buy/CreateBatch` call `effects.Quote` several times per item. Negligible at 54 listings; hoist one instance per call if content grows.
24. **Style inconsistency.** (Formatter not run yet: no .NET formatter is available in the AI workspace; use the IDE's Format Document.) Some files are compressed one-liners (`CustomerVisitService`, `CommunityProgress`, `IntroMealPlan`, parts of `BistroUIScreen`), others are fully formatted. For the handover deliverable, run the IDE formatter on those files (no behaviour change).
25. **Legacy UI text.** The theme uses `UnityEngine.Font` / legacy `Text`, while the plan's stack is TextMeshPro. Switch when converting screens to prefabs (better sharpness on Web and easier styling).
26. **[Done 2026-10-28: circle/triangle/octagon on sorting bins and results]** **Traffic Light accessibility.** Sorting slots are labelled "GREEN/YELLOW/RED" in text, so they are readable without colour. The plan also asks for distinct shapes (circle GO, triangle SLOW, octagon STOP); add them with the UI art pass.

## E. UI architecture notes for the next UI/art pass

- Every click destroys and rebuilds the whole current screen. That works, but it (a) creates garbage on each interaction in WebGL, (b) makes animation and art polish hard because nothing persists between frames, and (c) blocks designer iteration because there are no prefabs to edit.
- Recommended direction for the "simple, interactive" UI: keep the GameLoop/Queries contract, but build each screen (City, Store, Service, Results, Menu) as a prefab or persistent object that **updates** its widgets instead of being rebuilt. Start with the screen that changes most (City → Store), using the approved concept image as background and shelf-ID hotspots.
- Keep hotspots keyed by store/shelf ID so they can later become 3D colliders.

## F. Documentation

- Guides were refreshed on 2026-10-22 (README, AGENTS, DeveloperGuide, GameOperations, UIFramework). Remaining guides (Checkpoints, Tutorials, PopularityAndCravings, Events, Endings, ArtDirection) were checked and are current.
- `ASSET_LICENSES.md` (plan deliverable) does not exist yet. Start it before importing any asset pack or AI-generated art, and record the concept images' origin.

## Fixed 2026-10-24 (Owen's playtest)

- Low money was unrecoverable: rent pushed money negative, stock spoiled and nothing could be bought. Added
  pantry selling (`PantryService`, `Data/Economy.md`), a rent warning the day before, and a selling lesson.
- Dish review forecast health, delight, popularity, pay and tips before serving, which let players (and AI
  testers) pick the most profitable dish. Now outcome numbers appear only after serving (debug builds excepted).
- No way to check recipes while shopping: added a Recipes button and panel.
- Test stubs lacked `TooltipAttribute`, which `BalanceConfig` uses since the rent work; added to
  `Test-GameOperations.ps1` and `Test-ContentImport.ps1`.
- Empty pantry + no money was a dead end even with selling. Added the farmer's food aid (`FoodAidService`,
  `Data/Economy.md`): once a day, free low-appeal green food for one recipe when nothing can be cooked.

## Fixed 2026-10-25

- Junk foods had almost no slots (chips only in two day-4 recipes; eggs and nuggets none). Added R012 Loaded potato
  plate (day 1), R013 Breakfast plate (day 1, optional Egg/Fruit), R014 Bean burger plate (day 2, optional
  MeatSubstitute/Dairy/Vegetable); each still has an all-green build. Pending supervisor review.
- No reason to choose high-appeal food: added the Comfort request. One recipe dominated: added global recipe fatigue.
- Players couldn't see guest state, plate appeal or what they had achieved: guest info line, plate appeal, run recap.

## UI art batch validation — 2026-10-25

- Verdant produce art/shelf first batch is implemented but real Play Mode layout/input remains unverified. Bread/berries atlas details exceed desired simplification; review before mass production. Header/cart review remain existing UI, not full concept parity.
- During this pass Test-GameOperations and Test-CsvContent were blocked by existing Tools/ComfortFatigueChecks.cs:48 float-to-int assignment after concurrent dishHealthEffect changes. Do not interpret this as a shelf UI regression. Test-ContentImport passed. Gameplay semantics were left to the concurrent author.

### Shelf correction verification

- Fixed missing food: atlas had imported as Cube rather than Texture2D. Corrected metadata preserves GUID; a dedicated editor importer enforces 2D settings.
- Replaced the old shelf toolbar/cart presentation with illustrated cards, thumbnails and matching review panel. Earlier statement that header/cart review remain old placeholders is superseded.
- Actual Unity offscreen Canvas rendering and injected flip/single-drop/cart refresh passed. Manual Play Mode/browser checks remain outstanding; do not equate the capture with a full playtest. Food detail remains subject to review before expansion.
- Runtime assembly builds without warnings/errors; content-import checks pass. GameOperations and CsvContent checks are still blocked by the existing ComfortFatigueChecks.cs:48 float-to-int mismatch in concurrent gameplay test work.

### Layered shelf art refinement (2026-10-26)

- Corrected generated atlas row boundaries through presentation rowEdges metadata; no adjacent tile colour should leak into card footers.
- Added a simplified food review batch, thin shadows, illustrated leaf/cart, irregular plaques, optional foreground overlap and shelf materials. Art still awaits user approval. Broad adoption of the header and fully separate food/background assets remain future work.
- Runtime compilation passed with no warnings/errors. Isolated Editor rendering and injected card checks are used for this pass; manual Play Mode/browser verification is still outstanding.

### Multi-shelf follow-up

- Five shelves now use the shared illustrated renderer and have complete current listing-art coverage; other shelves retain fallback cards.
- Muted palette replacement and shared active-game toolbar implemented. Main title/ending screens remain separate. Generic menu bodies still need their own visual pass.
- Art is mapped through presentation JSON, not gameplay CSV; food/background are combined compositions, not separately adjustable layers. Foreground/plaque/text remain separate. Do not describe this as fully separated layer production.
- Compilation, five-shelf art coverage and isolated render/injected card checks passed. Manual Play Mode/browser verification remains outstanding.

### Card-back and Wellness follow-up (2026-10-26)

- Replaced white illustrated-card backs with food-specific companion artwork and a reading veil; information unchanged. Added Wellness shelf styling and eight food illustrations (38 total).
- Produce front motifs revised; older Staples/DryGoods front compositions remain and may need later artistic review. Art consistency is still subject to user review.
- Latest isolated Unity verification is pending; earlier five-shelf captures do not validate this revision. Manual Play Mode/browser checks remain outstanding.

Latest verification supersedes the pending note above: runtime build passed (zero warnings/errors), and isolated Unity capture completed with SHELF PASS across six styled shelves. Card backs and Wellness screenshots were visually reviewed. Disposable-project initial package import/path errors occurred before successful capture; manual Play Mode/browser validation remains open.

### Full current shelf-art coverage (2026-10-26)

- Added the four remaining shelf themes and 25 food illustrations with companion backs. All 63 current food IDs and all ten shelves now have presentation mappings; earlier fallback-only shelf notes are superseded for current content.
- New foods still need authored art; fallback remains safe. Shelf geometry/wood rail is still shared; per-store bespoke shelf materials and manual Play Mode/browser testing remain open.

Remaining-shelf verification: runtime build passed with zero warnings/errors; isolated Unity capture and injected card checks passed. All current listing foods resolve to artwork. New four-shelf captures and lower Luxury rows reviewed. Manual Play Mode/browser testing remains outstanding; art detail/palette remains subject to user review.

### City/store placeholder navigation verification (2026-10-26)

- Integrated the selected waterfront city and three distinct store cutaways. Presentation JSON defines polygon regions against stable store/shelf IDs. Current three shop routes and ten shelf routes are connected; future unmapped content retains fallback controls.
- Added CheapCart and MegaBulk raster logos to listing cards and store headers. No gameplay/content CSV changes in this pass.
- Isolated Unity compilation/capture passed WORLD PASS and SHELF PASS: injected shop/shelf clicks, drag-click suppression, day-one MegaBulk lock, artwork lookup, card flip, duplicate-drop protection and cart refresh. City framing and all three store captures visually reviewed.
- Manual Play Mode/browser pointer targeting, resizing and panning still require verification. Injected callbacks do not prove end-to-end pointer raycasting. Regions are approximate authored polygons over flattened concepts; baked image signage remains. Bistro/tower routes, animated transitions and 3D remain future work.

City footer cleanup (2026-10-26): removed redundant bottom controls from illustrated overviews, moved tutorial Help and cart access into the toolbar, and connected the tower to news/archive and anchored notification bubbles. Supersedes the earlier note that tower navigation is absent. Isolated NEWS/WORLD/SHELF checks passed and captures reviewed; manual interaction verification remains open.

Supporting menus (2026-10-27): shared cream/sage styling and typography now applied to legacy overlays; main menu uses city art, pantry batches use illustrated foods. Text-list layouts remain intentionally simple. Isolated compilation/captures passed; full manual menu interaction and smaller-screen checks remain outstanding. Gameplay/save/sell semantics unchanged.

Recipe book (2026-10-27): illustrated reference cards replace the old recipe text list; 15 recipe images and 12 category icons mapped in presentation JSON. Current isolated compile/coverage/flip checks passed and captures reviewed. Manual pointer/scroll and narrow-screen testing remain open; ingredient groups with many categories scroll internally. Art uses a reading veil on the reverse rather than independent motif-only backs. Cooking selection remains its existing workflow.

Recipe opacity follow-up: replaced hard reading panels with feathered alpha masks, front/back opacity exposed in UI theme. Isolated compile/recipe checks passed and renders reviewed; lower-opacity backgrounds remain subject to readability/art review.

- 2026-10-27: service now has layered bistro/placeholder/counter and illustrated recipe selection. Final character art, responsive small-screen review, and manual service drag/serve checks remain. Recipe backs no longer repeat dish artwork; background motifs can be authored separately later.

### 2026-10-28 dialogue/UI handoff
- Done: full-page guide and cream mist veil replaced by shared compact bottom dialogue; paged text for tutorial, conversation, dish review/results, day summary, guests, return confirmation, event/Codex explanations and ending text. Browsing/selection remains a card interface.
- Done: cooking controls and slots reskinned, category icons added, seated mini-cards use food artwork, duplicate “Available” footer removed. Recenter and its callback removed from illustrated/fallback city; replaced with tutorial review. Help remains accessible after lessons complete.
- Done: recursive atlas catalogue DTO flattened to a root catalogue plus nonrecursive atlas records, addressing the serialization-depth warning without changing JSON IDs or layout.
- Comfort explanation moved into the authored request lesson using the live balance value; CSV reimported with the existing importer in the disposable project and the resulting TutorialDatabase asset copied back after verifying its diff (text and fingerprint only). GUID unchanged.
- Verification: Unity compiled and rendered the revised UI in an isolated project; callback checks cover recipe selection, illustrated mini-card existence, tutorial Next not advancing tutorial state, and final Continue completing the lesson. ContentImport passes. GameOperations/CsvContent remain blocked by the pre-existing float-to-int mismatch at Tools/ComfortFatigueChecks.cs:48. Initial fresh-clone package import produced transient package/compiler messages; subsequent runs completed. Runtime Destroy warnings occur in this Edit Mode harness.
- Still unverified: live pointer/raycast, keyboard/controller navigation, small/mobile aspect ratios and end-to-end serving in Play Mode/WebGL. Screenshots are synthetic sessions, not balance evidence. Customer silhouette remains a placeholder; final art and animation are deferred.

## Art moved into the CSVs — 2026-10-28

- Done: food, recipe, category and store art are CSV columns imported into Sprite slots (blank path keeps an Inspector-assigned sprite); screen-wide art is on `Assets/GameData/BistroUITheme.asset`. Rule recorded in `AGENTS.md` ("Where art lives").
- Owen to delete in Unity (no longer used): `Assets/Scripts/UI/BistroShelfArt.cs`, `Assets/Scripts/UI/BistroRecipeArt.cs`, `Assets/Editor/BistroShelfArtImport.cs` (each now a one-line stub), and from `Assets/Resources/BistroArt/`: `catalog.json`, `recipes.json`, `decor.json` and every `.png` sheet (city, S001–S003, ServiceRoom, ShelfDecor, StoreLogos, Categories, Recipes, VerdantProduce, ProduceBacks, Staples, StapleBacks, DryGoods, DryGoodsBacks, EdamameBarley, EdamameBarleyBacks, Wellness, WellnessBacks, Everyday, EverydayBacks, Treats, TreatsBacks, Luxury, LuxuryBacks), with their `.meta` files. Keep `world.json` and `Fonts/`. Everything in `Resources` ships in the build, so the old sheets (42 MB of PNGs, imported uncompressed) stay in it until removed.
- Fonts still load from `Resources/BistroArt/Fonts` by name; the theme's `font` slot is unused. Low priority.

- Fixed 2026-10-28: guide lesson and guest dialogue shown together, with Next not advancing (shared page counter). Only one dialogue box is drawn per screen now; see UIFramework.md "One dialogue box at a time".
- Fixed 2026-10-28: sorting screen still used the old greybox look; restyled to the illustrated kitchen with Traffic Light shapes. Help no longer lists every past lesson.

2026-10-28 follow-up: the current CSV/per-item-Sprite UI snapshot passed isolated dialogue/service/recipe/news/world/shelf checks. Captures are in Data/Previews/2026-10-28-CSV. Current ContentImport passes; GameOperations/CsvContent still stop at ComfortFatigueChecks.cs:48. No manual Play Mode/WebGL claim. The old recursive JSON catalogue no longer exists in runtime art code, so its earlier DTO workaround is historical.

### Verification: recipe/menu/map polish (2026-10-28)

Unity 6000.4.1f1 compiled and ran the updated ShelfCapture in disposable Temp/UIReview28. All shelf, recipe, service, dialogue, news and world assertions passed; additional assertions verify map overflow on both axes and the start-menu blur material. Reviewed rendered menu and recipe backs; captures are in Data/Previews/2026-10-28-MenuCards. Separate RecipeFlipCheck entered Play Mode and passed animation narrowing, paused-time progression, repeated-click suppression, disable cleanup and instant reduced-motion flip.

Test-ContentImport.ps1 passed. Test-GameOperations.ps1 and Test-CsvContent.ps1 remain blocked by the existing Tools/ComfortFatigueChecks.cs:48 float-to-int compile error; this UI pass did not change that gameplay test. No WebGL build or manual end-to-end playthrough was run. Other small-menu art/layout polish remains separate future work.

### Surface/icon verification (2026-10-29)
Completed toolbar/marker/bin surface polish and HUD icon antialiasing. Graphics-enabled Unity capture in Tools/UI/ReviewProject passed shelf, recipe, dialogue, service, world and news checks, plus the sorting icon-cache assertion. Reviewed city and sorting captures; final previews saved under Data/Previews/2026-10-29-Surfaces. The disposable project's initial package import emitted transient ShaderGraph/package-cache errors; the final fresh capture completed without those errors. Existing full-suite ComfortFatigueChecks.cs:48 float/int test compile blocker remains unchanged. No new manual end-to-end Play Mode or WebGL validation for this surface-only pass; earlier dedicated recipe flip Play Mode check passed.

2026-10-29: Replaced inherited solid-green cooking/dialogue action controls with the shared three-role button family. Unity capture and routing checks passed. Full manual keyboard/disabled-state and WebGL checks remain outstanding; the previously recorded ComfortFatigueChecks float/int harness error is outside this presentation change.

### Ending visual refresh (2026-10-30)
Old framed ending screen replaced with full-scene individual illustrations and the shared bottom dialogue. All ending dialogue image overrides now use the corresponding new image. Character-free assets are provisional; generated architecture varies slightly, and final 3D scenes/character art remain outstanding.

Legacy ending art cleanup: seven unused files removed; five remain as event illustrations. Moving/replacing those is a separate event-art task, not safe blanket deletion of the Endings folder.

2026-10-30 event-art migration supersedes the earlier note retaining five legacy ending images: all five now have their own CSV-linked editorial icon and the old files are removed. Event paper layout reviewed at 1672x940; smaller windows/Web still need checking. Broad capture currently stops at its shopping drag assertion after concurrent slot changes; focused event checks pass.

## Fixed 2026-10-30 (Owen's playtest): market stuck after checkout
- Checking out a cart of already-known foods goes straight to service. That step asked the tutorial first; the money-low lesson switched on at that moment (money now below rent), so the move was refused after the purchase had already gone through. The market then refused further buying ("today's checkout is already complete") and nothing retried the move. Fix: `GameLoop.Checkout` now calls an internal `StartService` that does not ask the tutorial (the lesson shows on the next screen); the money-low flag is only set at the market before today's checkout. Regression check at the end of `Tools/TutorialChecks.cs`. Other tutorial-gated steps refuse before changing anything, so they were not affected.
