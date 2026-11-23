# Blue Zone Bistro

New developer or AI tool: start with [AGENTS.md](AGENTS.md). It indexes every system guide, records design decisions and the current art handover, and links the open backlog in [Data/KnownIssues.md](Data/KnownIssues.md).

Solo CSC494 capstone in Unity 6000.4.1f1: a six-day, data-driven 2D management game teaching whole-food, plant-based eating. The player shops at three stores, sorts new foods by Traffic Light, builds category-slot meals for customers and sees the effect on health, delight, popularity and community wellbeing.

Repository: https://github.com/Terrachizoid/CSC494-Blue-Zone-Bistro-Capstone

## What is implemented (as of 2026-10-22)

- Main menu, New Game / Resume with day-start checkpoints ([Checkpoints](Data/Checkpoints.md)).
- Market: draggable city map → store → shelf, flip cards, cross-store cart, one atomic checkout per day.
- Food sorting: each new food sorted correctly refunds 50% of what was paid for it that day; correct foods are remembered.
- Service: story encounters, customer dialogue, category-slot meal building with preview/confirm, dish results and daily results ([Game operations](Data/GameOperations.md)).
- Popularity-driven attendance, delight, delayed finance (tips), cravings from day 4, community wellbeing ([Popularity and cravings](Data/PopularityAndCravings.md)).
- Visual-only rush/crash state with authored rush and crash dialogue.
- Four tutorials ([Tutorials](Data/Tutorials.md)), morning events and news ([Events](Data/Events.md)), four endings ([Endings](Data/Endings.md)).
- All gameplay screens are Canvas UI generated at runtime ([UI framework](Data/UIFramework.md)).

Current content (2026-10-28): 63 foods, 87 store listings, 3 stores, 12 food categories, 15 recipes, 6 customers, 114 dialogue sequences (217 lines), 5 events, 5 endings, 11 tutorials (13 steps). All content is prototype and `Pending` supervisor review.

Not yet done: researched claims / Codex content, balance tuning, final art integration (concepts exist, see [Art direction](Data/ArtDirection.md)), final music (procedural tracks currently used; CC0 sound effects integrated, see [Sound](Data/Sound.md)).

## Run locally

1. Open the project in Unity 6000.4.1f1 and allow scripts to compile.
2. Run **Tools > Blue Zone Bistro > Import All CSV Content**. Required after cloning or editing any CSV in `Assets/GameData/CSV`.
3. Open `Assets/Scenes/SampleScene.unity` and press Play. The game opens at the main menu.

Progress is saved only when a day is finished. Closing mid-day returns to that day's start.

## Checks

From the project folder in PowerShell:

```powershell
pwsh -NoProfile -File Tools/Test-GameOperations.ps1
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

Balance: `python Tools/BalanceSim/bzb_sim.py .` plays 300 six-day runs per bot play style from the real CSVs and BalanceConfig (see `Tools/BalanceSim/README.md`).

These use isolated fixtures and Unity stubs. They do not replace a Play Mode walkthrough or a browser build test. Editor menu checks: **Validate Imported Content**, **Test Checkpoint Round Trip**, **Preview Endings**.

## Web deployment

The game is hosted on [Unity Play](https://play.unity.com/en/games/b5fffbe8-dd33-480e-ade1-8caca952edf1/bluezonebistro) with **Unlisted** visibility. Preserve that entry and visibility when updating it.

Source pushes alone do not publish a build. `BistroWebBuild.Build` is the batch-build entry point: it imports all CSV content, validates it and builds the gameplay scene with gzip decompression fallback. Set `BISTRO_WEB_OUTPUT` to an output directory and invoke Unity with `-batchmode -nographics -quit -buildTarget WebGL -executeMethod BistroWebBuild.Build`. Use a separate project copy if the working project is open in Unity.

Zip the output with `index.html` at the archive root. On Unity Play open the game's **Options > Edit Game Details**, replace **Upload game build**, click **Update**, then verify the hosted game loads. Build output is excluded from source control; credentials are never stored in this repository.

Current UI handover (2026-10-28): see AGENTS.md and Data/UIFramework.md for the integrated illustrated city/store/recipe/service UI and shared compact dialogue panel. Data/Previews holds offscreen Unity captures; manual Play Mode/WebGL interaction remains a separate check.

Current submission (2026-11-20): the postponed 3D prototype has been archived outside the repository; the game uses the illustrated 2D city/store views. See Data/SubmissionCleanup_20261120.md. The Web build profile is configured as release (despite its legacy filename), gzip compression, decompression fallback and data caching. Use BistroWebBuild.Build for repeatable publication. Local Python packages must not be committed.
