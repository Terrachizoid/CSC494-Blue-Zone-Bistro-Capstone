# Blue Zone Bistro

Unity 6000.4.1f1 solo capstone greybox, including the Week 5 shopping and sorting flow.

Repository: https://github.com/Terrachizoid/CSC494-Blue-Zone-Bistro-Capstone

## Run locally

The game now opens at a main menu. New Game creates a Day 1 checkpoint; Resume loads
the last day-start checkpoint. Progress is saved when finishing a day, not mid-day.
See [checkpoints and rush/crash](Data/Checkpoints.md) for persistence, tutorial flags,
visual-only customer state and verification steps.

Four introductory lessons now cover shopping, sorting, cooking and rush/crash.
See [Popularity and cravings](Data/PopularityAndCravings.md) for morning attendance,
ingredient appeal, day-four requests, global wellbeing and all prototype balance formulas.
See [Tutorial framework](Data/Tutorials.md) for CSV authoring, behaviour ownership,
safe skipping, checkpoint behaviour and a Play Mode walkthrough.

1. Open the project in Unity 6000.4.1f1 and allow scripts to compile.
2. Run **Tools > Blue Zone Bistro > Import All CSV Content**. This is required after cloning or changing the authored CSV tables.
3. Open `Assets/Scenes/SampleScene.unity` and press Play.

The playable loop includes a cross-store shopping cart, food sorting, customer/story encounters, dialogue, meal previews and service, and daily results across six days. Content currently includes 40 foods, 54 listings, 6 customers, 11 recipes, and 109 dialogue sequences. Values and narrative remain prototype content pending review.

Add packages to the cart, review quantities, and confirm checkout. The full cart must be affordable. Answer each new food's Traffic Light classification once; correct answers are remembered, while wrong foods can return after a later purchase. Accuracy refunds up to 30% of the checkout total. Empty or entirely mastered purchases skip sorting with no refund. Pantry inspection and a basic Codex are available throughout the day; detailed educational explanations/research remain pending. This greybox still exposes classifications in inspection screens.

See [Developer guide](Data/DeveloperGuide.md) for architecture, content authoring, validation, and limitations, and [Game operations](Data/GameOperations.md) for gameplay rules.

## Checks

The first Canvas UI milestone now replaces market and sorting with city/store/shelf navigation,
flip cards, drag/drop slots and slot contents panels. Meal building now uses grouped food cards and recipe slots; dialogue and results also use Canvas. See [UI framework checkpoint](Data/UIFramework.md) for setup, ownership,
manual checks and the remaining meal-slot milestone.

Run in PowerShell from the project folder:

```powershell
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

The CSV checks also run the isolated operation/dialogue checks. They use Unity stubs and do not replace a Unity Play Mode or browser build test.

## Web deployment

The existing game is hosted on [Unity Play](https://play.unity.com/en/games/b5fffbe8-dd33-480e-ade1-8caca952edf1/bluezonebistro), with **Unlisted** visibility. Preserve that game entry and visibility when updating it.

Source pushes alone do not publish a new Unity Web build. `BistroWebBuild.Build` is the editor batch-build entry point: it imports all CSV content, validates it, and builds the gameplay scene with gzip decompression fallback. Set `BISTRO_WEB_OUTPUT` to an output directory and invoke Unity with `-batchmode -nographics -quit -buildTarget WebGL -executeMethod BistroWebBuild.Build`. Use a separate project copy if the working project is open in Unity.

Zip the output contents with `index.html` at the archive root. Sign in to Unity Play, open the existing game's **Options > Edit Game Details**, replace **Upload game build**, then click **Update**. Verify the hosted game loads afterward. Generated build output is excluded from source control; authentication is handled in the browser, not stored in this repository.
