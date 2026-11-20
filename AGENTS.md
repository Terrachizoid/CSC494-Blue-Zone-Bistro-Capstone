# Blue Zone Bistro: developer and AI entry point

Read this file before changing the project. It is a shared handover for humans and AI tools; it does not replace the user's current instructions.

## Read first

1. `README.md`: Unity version, what is implemented, local startup, checks, Web deployment.
2. `Data/DeveloperGuide.md`: architecture, ownership, invariants and content authoring.
3. `Data/KnownIssues.md`: open design/content/code issues from the 2026-10-22 review. Check it before "fixing" something; many items are deliberate or already triaged.
4. The guide for the area being changed:
   - `Data/GameOperations.md`: purchasing, sorting, meals, inventory, days, dialogue scheduling.
   - `Data/UIFramework.md`: Canvas cards, slots, navigation and interaction.
   - `Data/Checkpoints.md`: main menu, day-start saves and visual rush/crash.
   - `Data/Tutorials.md`: tutorial content and action rules.
   - `Data/PopularityAndCravings.md`: attendance, appeal, cravings and wellbeing.
   - `Data/Events.md`: temporary modifiers, news and event authoring.
   - `Data/Endings.md`: ending selection, presentation and debug preview.
   - `Data/Economy.md`: rent, walk-in income, craving pressure, negative money and the bankrupt ending.
   - `Data/PlaytestReport_AI.md` and `Tools/Playtest/`: AI persona playtests, harness and findings.
   - `Data/ArtDirection.md`: selected visual references and prototype status.

The guides were brought up to date with the code on 2026-10-22. If a guide and the code disagree, trust the code and fix the guide.

## Scope documents and precedence

- `Blue_Zone_Bistro_Capstone_Plan` (instructor, 12-week plan): nutrition framework (§2–§4) and design context.
- `Blue_Zone_Bistro_8_10_Hour_GDD` (student, 2026-09-12): the approved solo scope. Framework before volume; mixed green/yellow/red meals are playable; economy simulation, Café Fund, luxury shelf and complex prosperity are non-goals.
- Later user decisions (below) take precedence over both.

The nutrition framework in Plan §2 is a fixed client requirement: no player-facing text may present oils, meat, fish, eggs or dairy as healthy, neutral or "fine in moderation", and yellow is "on the way out", not "okay". Supervisor approves classifications and claims; developers do not reclassify or soften content.

## Design decisions (user, binding)

- **Store memberships are cut.** MegaBulk Club simply opens on day 4 (Chapter 2). There is no membership fee, Café Fund or purchase UI. Reason: added complexity without fun or educational value. The dormant `requiresMembership` / `membershipCost` / `storeMembershipIds` fields still exist in code and CSV; do not build UI for them. Removal is tracked in `KnownIssues.md`.
- **Mixed meals.** Recipes are category-slot templates; yellow and red foods can be served and their effects come from the foods chosen (`allowNonGreenChoices=true` on all current recipes).
- **Delight never adds health; finance follows health change with a 2-day lag** and currently only drives tips.
- **Verdant's luxury shelf is stocked** (overrides the GDD non-goal): expensive, high-appeal green/yellow foods only, no red. It is shelf content, not a new mechanic.
- **Multiple checkouts per day (2026-11-13, replaces "one checkout per day").** Checkout (shelf panel or cart review) buys; if the purchase has new foods, that purchase is sorted right away (its own receipt and refund), then the player returns to the same store and may check out again. Clicking the Bistro on the city map and confirming is the only way to end shopping and start service; a non-empty cart offers "Check out first". Sorted food can be sold back at the market as before.
- **City during service (easy-mode switch `BalanceConfig.cityDuringService`, 2026-11-13).** `GamePhase` is where the player is; today's service progress (`GameLoop.Service`, `BistroOpened`) survives a trip to the city. When on, a "← City" button shows in the bistro; the Bistro on the map then returns straight to the guests. An unserved guest waits (meal selection resumes; an unfinished dialogue restarts without its effects); a guest already served is done and the next one starts on return. Leaving clears the meal being built. Off = same code, no button. With the switch on, the farmer only helps when money is also below `foodAidMoneyBelow`.
- **Six days, two chapters.** Days 1–3 Gray City; day 4 opens MegaBulk, Okinawa-inspired recipes and cravings.
- **Art lives in the CSVs (2026-10-28).** Every food, recipe, category and store gets its art from a path column in its CSV, imported into a Sprite slot on the asset. Never add a JSON catalogue, sprite-sheet coordinates or ID-keyed art lookups in C#. See "Where art lives" below.
- **Visual target:** 2D placeholders that read as low-poly 3D now; eventual low-poly 3D scenes with 2D characters. Keep shelf/store IDs independent of 2D hotspots so they can become 3D colliders.

## Working boundaries

- Preserve the solo six-day educational scope. Older instructor-plan proposals are context, not automatic authorization to add mechanics.
- Inspect current code and the working-tree diff before editing. Preserve unrelated/uncommitted work. Do not assume a previous AI's summary proves something was tested.
- GameBootstrap connects objects; GameLoop coordinates phases; services validate and apply gameplay; GameSessionState owns mutable progress; GameContentQueries supplies presentation data. UI never calculates authoritative money, scores or outcomes.
- Share session flags, day and dialogue history. Preview must not mutate progress; commit validates again.
- CSVs in `Assets/GameData/CSV` are authored content. Imported ScriptableObjects are runtime content. Reimport after content edits. Do not implement temporary event effects by changing baseline CSVs/assets.
- Save/resume, cravings, sorting, tutorials, events and endings already exist. Do not recreate them.
- Keep optional image/audio references safe when missing. Keep player-facing text free of debugging jargon. Do not expose Traffic Light answers in normal card inspection (only `BistroUITheme.debugCardDetails` in debug builds); preserve intentional sorting feedback.
- Preserve Unity `.meta` files and stable content IDs. Do not casually regenerate asset GUIDs.

## Current visual handover (2026-10-28)

The illustrated UI is now integrated, not only concept work. Preserve the newer implementation rather than restoring the old solid-block screens.

### Where art lives (binding, 2026-10-28)

One image file per item under `Assets/Art/`, referenced from data. Unity imports these folders as sprites automatically (`Assets/Editor/BistroArtImport.cs`).

| Art | File | Set in |
|---|---|---|
| Food card front / back | `Art/Foods/P001_front.png`, `P001_back.png` | `Foods.csv` `icon_path`, `back_path` (+ optional `card_style`: `light_logo`, `dark_plaque`, `sprig`) |
| Recipe dish / card back | `Art/Recipes/R001_dish.png`, `R001_back.png` | `Recipes.csv` `icon_path`, `back_path` |
| Category icon (recipe tiles, meal slots) | `Art/Categories/Grain.png` | `Categories.csv` `icon_path` |
| Store logo, interior, shelf colours | `Art/Stores/S001_logo.png`, `S001_background.png` | `Stores.csv` `logo_path`, `shop_background_path`, `shelf_colours` |
| Screen-wide art (city, bistro room, counter, plaques, shelf rail, sprig, cart icon) | `Art/UI/<slot>.png` | `Assets/GameData/BistroUITheme.asset` slots; empty slots auto-fill from `Art/UI/<slot name>.png` |

- To add or replace art: put the PNG in the folder, set the CSV path, run Tools > Blue Zone Bistro > Import All CSV Content. Or drag a sprite onto the asset's slot in the Inspector; a blank CSV path keeps the dragged sprite.
- Missing art is allowed (plain card fallback); the content validator lists what is missing.
- `Resources/BistroArt/world.json` holds only click regions (polygons and label markers per view), which are layout, not art.
- Food art style: simple forms, few colours, muted backgrounds and gradients; expressive highlights allowed. Keep artwork and title/price text separate. Recipe illustrations are fixed per recipe. Recipe backs are background only, never the dish again (muted gradients with ingredient motifs).
- Active city is based on `City_Waterfront_Selected_20261025.png`; the bistro on `Bistro_Midtone_Selected_20261025.png` (fixed behind-counter view; layers: room, customer portrait or silhouette, counter, dialogue or cooking UI). Gameplay store/shelf IDs stay independent of image coordinates for future 3D colliders.
- **Latest user decision:** remove the misty cream dialogue veil and full-page tutorial takeover. Use one compact bottom dialogue presentation, with dark bottom-to-transparent fade, speaker name and readable light text. Tutorials use “Blue Zone Guide.” Reviews/results and text explanations should reuse this panel; browsing still uses cards. Long text is paged, and only the last page performs the final action.
- Cooking uses the shared illustrated style for buttons, slots and miniature ingredient cards. Slots stay above pantry cards. The Comfort threshold belongs in its own lesson (T_COMFORT, first Comfort request), not permanently in the cooking header; read the value from balance configuration.
- Recenter and the city Tutorial button were removed. Toolbar Help repeats the lesson in progress; with none, it shows a Guide chatter line (`Resources/Idle/IdleChatter.csv`). Reviewing text must never replay tutorial actions or mutate completion.
- Customer artwork/animation is deferred: simple original 2D characters with expressions, upper-body framing behind the counter. Attached character images were style references, not owned production art.
- The goal remains eventual low-poly 3D scenery with 2D characters and independent Canvas overlays. Do not add camera head turning.

### Debug panel (2026-11-20)
F2 in the editor and development builds (`Debug.isDebugBuild`): start any event today, end the day or jump to a day, add money or popularity, stock or empty the pantry, finish all lessons. Rules stay in `GameDebugTools` (Services) and `GameLoop.DebugEndDay`; the IMGUI panel (`BistroDebugPanel`) only calls them. Release builds never create it. The FPS readout (F3 panel) is also off by default in release builds.

## Verification and handoff

From the project root:

```powershell
pwsh -NoProfile -File Tools/Test-GameOperations.ps1
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

For balance, price, recipe or customer changes also run `python Tools/BalanceSim/bzb_sim.py .` and compare with the baseline table in `Tools/BalanceSim/README.md`. If you change scoring formulas in C#, mirror them in the simulator.

These use isolated fixtures/stubs and do not establish Play Mode or browser behaviour. For UI changes, check real interaction and layout in Unity. State clearly what was run and what remains unverified. Documentation-only changes do not require gameplay tests.

After work, update the relevant guide with actual changes, limitations and next steps, and update `KnownIssues.md` when you fix or add an issue. Keep approved versus experimental art distinct. Record prompts/references when continuing concept work. Use one writer per file when several tools work concurrently; review each diff before combining. Never put credentials in handover files.


### Concurrent-agent handoff reminder (2026-10-28)
Before continuing UI work, reread the current code: the CSV/per-item-art migration, one-dialogue-at-a-time rendering, revised top bar and sorting UI supersede earlier JSON/atlas work. Preserve those changes. Use a separate disposable Unity project for captures; never copy stale scripts or generated test assets back into the live project. The capture harness must use a clone of the authored BistroUITheme so it exercises the real room/counter and decoration references.

### Recipe/menu polish (2026-10-28)
Recipe backs R001-R015 are individual background-only motif PNGs at their existing CSV back_path locations; preserve their GUIDs. They contain no plated dish or white reading block. BistroRecipeCard preserves single dish rendering and the unavailable cream wash; flipping now uses unscaled animation with the theme's cardFlipSeconds/reducedMotion. City zoom is theme.cityMapZoom (1.4 default), not a change to hotspot coordinates or store views. The start menu is a sage side panel with authored botanical accent and a menu-only city blur shader. Keep gameplay/data ownership unchanged.

### UI surface polish (2026-10-29)
Toolbar/map markers/sorting bins use BistroSurface shading and authored sprig decoration. BistroShelfIcon preserves its symbols but caches supersampled antialiased alpha textures; sprite overrides still win. Keep decoration non-interactive and CSV art ownership unchanged. Disposable reviews now use Tools/UI/ReviewProject (locally ignored), since Unity can clear the project Temp folder.

### Contextual actions (2026-10-29)
Use BistroActionButton with an explicit Primary/Secondary/Caution role for transient actions. Palette is configurable in BistroUITheme (sage/cream/clay defaults); Next is primary, Previous/Back secondary, Skip customer caution. Keep toolbar/main-menu controls and world indicators separate.

### Ending artwork (2026-10-30)
Five individual Ending_<ID>.png illustrations under Assets/Art/Endings are referenced in the endings CSV and all corresponding dialogue background paths. The full-scene ending uses the shared bottom dialogue and a compact title plaque. Preserve outcome rules and persisted selection. These character-free illustrations are review placeholders, not final 3D art.

Ending cleanup: seven unused legacy ending PNGs/metas removed after path/GUID reference checks. Keep blue_2, niche_0, popular_1, struggling_0 and struggling_2: news events still reference them. Ending panels/buttons already use the current decorated title, shared dialogue and action roles.

### Event newspapers (2026-10-30)
Events.csv owns image_path, optional thumbnail_path, paper_color and accent_color. Individual transparent icons live in Art/Events; current thumbnails reuse their simple full illustration. Expanded events and Codex use newspaper overlays, not the shared dialogue panel. Tower cards show headline/icon only. Keep live text separate, preserve optional art fallbacks and HTTP(S) source gating. All old geometric ending art is now removed: the five event consumers were migrated. Event services and slot semantics were not changed. See Data/Events.md for verification limits.

### Current priority (2026-11-19)
User postponed further 3D asset/Blender work in favour of gameplay, user experience and additional content. Preserve the existing 3D experiments but do not resume them without a new request. New event art follows the existing transparent, muted cut-paper newspaper icon style; keep event text and gameplay effects separate from illustrations.

### Reusable panel art (2026-11-20)
Six individual UI sprites (panelPaper, guideNotePanel, planPanel, suggestionFrame, soldOutStamp, newTag) live in Assets/Art/UI and are referenced by BistroUITheme. Use BistroShelfStyle.PaperPanel / ApplySurface for resizable illustrated surfaces; keep words, icons, and input separate. BistroArtImport sets nine-slice borders on first import; Configure illustrated panel borders restores defaults explicitly. Guide chatter uses the cream note; blocking lessons/reviews still use the existing bottom dialogue. See Data/PanelArt_20261120.md for prompts and Data/UIFramework.md for scope/checks. Further 3D remains postponed.
