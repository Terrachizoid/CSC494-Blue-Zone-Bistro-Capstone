# Isolated shelf capture

ShelfCapture.cs is an Editor-only verification harness. Copy it into Assets/Editor of a disposable Unity project containing this project's Scripts, GameData, Resources and Art folders plus the Editor folder (importers and BistroArtImport.cs). The harness runs Import All CSV Content first, so food, recipe, category and store art come from the CSVs exactly as in the game. Do not add it to the live project's Assets. It creates a day-one session in memory, renders the Canvas to shelf-render.png and cart-render.png, and exits the Editor. It does not load or write player saves.

Run the disposable project with Unity -batchmode -projectPath <absolute-clone-path> -executeMethod ShelfCapture.Run -logFile <log-path>. Do not use -nographics. Successful output contains SHELF PASS.

Checks include food sprite lookup, face switching, duplicate-drop protection and cart refresh. This uses injected pointer events in Edit Mode, not a manual Play Mode/browser test. Edit Mode can log warnings from runtime Destroy calls; inspect screenshots and exceptions rather than treating those warnings as gameplay failures.


The harness also captures card-backs.png (first four produce cards flipped), WELLNESS.png, the four other themed shelves and city-toolbar.png. Current front and reverse coverage is presentation-only; this does not test gameplay outcomes. On 2026-10-26 the disposable project's initial package imports logged path/compiler errors before a later successful compilation and SHELF PASS; do not describe that setup as a clean full-project validation.

The capture now includes all ten themed shelves. The four new shelves also produce *-backs.png, and LUXURY-bottom.png verifies the final rows after setting the shelf scroll position to the bottom. Data/Previews holds the reviewed results. The harness changes only its isolated in-memory session.

World navigation checks now capture S001-overview.png, S002-overview.png and S003-overview.png. WORLD PASS verifies three shop routes, ten shelf routes, drag-click suppression and the day-one MegaBulk lock through injected button events. This does not exercise physical pointer raycasting or manual pan behaviour. The city capture uses day four so all shop markers are available.

NEWS PASS additionally verifies the tower archive callback and notification detail callback. tower-news.png captures a seeded in-memory event bubble; no player saves are changed.

The 2026-10-28 harness also captures service-dialogue, service-guide, service-review, service-result, service-slotted and day-summary. DIALOGUE PASS verifies use of the shared bottom panel and that Next pages text without advancing tutorial state, while final Continue completes the lesson. It imports the current tutorial CSV in the disposable project. Service fixtures bypass sorting and use synthetic dish results; these are presentation checks, not end-to-end gameplay tests.


2026-10-28 CSV migration follow-up: the harness clones the authored BistroUITheme asset (rather than creating an empty theme), imports the CSV-owned individual sprites, and pages through any number of tutorial pages before invoking the final action. Do not copy generated/imported test assets back over a concurrently edited live project. Only reviewed screenshot outputs are handed back. Runtime art catalogues are removed; world.json is layout-only.

### Recipe flip Play Mode check (2026-10-28)

Copy RecipeFlipCheck.cs into the disposable project's Assets/Editor and run `-executeMethod RecipeFlipCheck.Run` with a graphics-capable Unity batch process. It enters Play Mode and checks animated narrowing, final face, unscaled-time progress while timeScale=0, repeated-click suppression, interruption cleanup and reduced-motion immediate flip. Do not install the harness into live Assets. ShelfCapture also asserts city map extents exceed the viewport on both axes and that the menu blur shader is assigned.

Use Tools/UI/ReviewProject with a local `*` .gitignore for disposable Unity reviews instead of Temp/UIReview28: the live editor can clear Temp. Copy source into the review project only; never copy old review scripts back into live Assets. ShelfCapture now also renders sorting-polish.png and checks the HUD icon cache size.
