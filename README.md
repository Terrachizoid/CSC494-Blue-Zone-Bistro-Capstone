# Blue Zone Bistro

Unity 6000.4.1f1 Week 3 greybox for the solo capstone project.

Repository: https://github.com/Terrachizoid/CSC494-Blue-Zone-Bistro-Capstone

## Run locally

1. Open the project in Unity 6000.4.1f1 and allow scripts to compile.
2. Run **Tools > Blue Zone Bistro > Import All CSV Content**. This is required after cloning or changing the authored CSV tables.
3. Open `Assets/Scenes/SampleScene.unity` and press Play.

The playable loop includes shopping, customer/story encounters, dialogue, meal previews and service, and daily results across six days. Content currently includes 40 foods, 54 listings, 6 customers, 8 recipes, and 97 dialogue sequences. Values and narrative remain prototype content pending review.

See [Developer guide](Data/DeveloperGuide.md) for architecture, content authoring, validation, and limitations, and [Game operations](Data/GameOperations.md) for gameplay rules.

## Checks

Run in PowerShell from the project folder:

```powershell
pwsh -NoProfile -File Tools/Test-CsvContent.ps1
pwsh -NoProfile -File Tools/Test-ContentImport.ps1
```

The CSV checks also run the isolated operation/dialogue checks. They use Unity stubs and do not replace a Unity Play Mode or browser build test.

## Web deployment

No web-hosting destination or automated deployment configuration is currently recorded in this repository. Source pushes alone do not publish a new Unity Web build. Import content before building, then publish the resulting Web build through the existing host once that destination is identified. Generated build output is excluded from source control.
