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
- **One checkout per day** across any number of open stores (the plan's two-store limit is not used).
- **Six days, two chapters.** Days 1–3 Gray City; day 4 opens MegaBulk, Okinawa-inspired recipes and cravings.
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

## Current visual handover (2026-10-22)

The active work is art direction and a simpler, more interactive UI, not an automatic replacement of the live UI.

- Approved city: `Assets/Art/Concepts/City_Approved_Direction.png`.
- Selected bistro: `Assets/Art/Concepts/Bistro_Approved_Direction.png` (the earlier muted sage version; later white/misty variants are not selected).
- Store prototypes: `Store_S001_Verdant_Prototype.png`, `Store_S002_CheapCart_Prototype.png`, `Store_S003_MegaBulk_Prototype.png` in the same folder. User-approved initial directions as of 2026-10-22; later revisions remain possible. Not final production backgrounds.
- Store view: elevated roofless cutaway with matching camera across stores; clickable areas correspond to CSV shelf IDs.
- Service view: fixed view from behind the counter toward the glass entrance. Customer upper-body layer sits behind the foreground counter. Meal-building overlay dims the scene, then clears for results/dialogue.
- Images are flattened concepts, not layered assets or live integrations. Final labels should be separate UI. Product dressing is illustrative.
- Before integrating art into stores, note the shelf-data issues in `KnownIssues.md` (empty `EYE_LEVEL` and `LUXURY` shelves; most CheapCart stock on `BOTTOM_SHELF`).

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
