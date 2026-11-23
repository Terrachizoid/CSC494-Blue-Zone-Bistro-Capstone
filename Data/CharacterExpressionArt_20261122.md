# Character expression art — 2026-11-22

Completed 24 new portraits: happy, concerned, disappointed and tired for June (U001), Dev (U002), Rosa (U003), Sam (U004), Marco (U005) and Priya (U006). Together with the seven existing neutrals there are 31 individual PNGs. Visiting Chef retains exactly one neutral/default image. Default silhouette/tint rows added concurrently remain intact.

## Visual decisions

Use the approved simple chunky cartoon style, muted clothing colours, thick brown contours, mostly frontal head-to-waist framing, and human facial features. Preserve identity and outfit. Expression-specific gestures are allowed: thinking/chin touches, open conversational palms, lowered shoulders, low folded arms, temple or neck touches for fatigue. Sam's hands stay outside the hoodie. June's first three variants retain the neutral pose; tired adds a temple touch. No fixed ages or new character biographies are implied. These remain generated review art, with minor silhouette/scale variation between poses; they are not interchangeable animation layers.

The neutral sources and references are in CharacterNeutralArt_20261122.md. Exact new generation prompts and filenames are recorded in `Tools/Art/CharacterExpressionSources_20261122.json`, an offline provenance record, never a runtime art catalogue. Each generation used the character's approved neutral PNG as its reference.

## Preparation and integration

`Tools/Art/PrepareCharacterTexture.ps1` prepares each source on a transparent 1024×1024 canvas, proportional fit, horizontal centering and bottom alignment. It preserves alpha and does not stretch or redraw the character. Original generated sources remain outside the repository. No downloaded image libraries were introduced.

This preparation matters: the former ~913×1024 neutral imports were RGBA32 despite the compression flag. All 31 prepared images were verified in Unity 6000.4.1f1 with WebGL selected as **1024×1024 DXT5**, automatic compressed import, no mipmaps. DXT5 image data is 1 MiB per image, 31 MiB total, compared with 124 MiB if these same square textures were RGBA32. This is texture storage, not a claim about the compressed Web download or total runtime memory. The 31 PNG source files total **17,084,877 bytes (16.29 MiB)**. No new release build was measured after this addition.

Files occupy the already-authored `image_path` slots in `Blue_Zone_Bistro_CharacterExpressions.csv`; no row IDs or emotion mappings were changed. Reimported only the character expression database. Existing neutral and database GUIDs were preserved; all 31 sprite GUIDs are unique. No runtime framework, dialogue content, scene logic, or gameplay changes were needed in this pass.

## Verification and limits

`Tools/UI/CharacterExpressionCapture.cs` is a disposable-project-only import/render check, outside live Assets. It compiled and passed in Tools/UI/ReviewProject: 31 portraits resolve, each named expression and emotion alias selects its expected art, one chef row, all textures DXT5 at 1024 square. It rendered all 31 through the actual BistroCharacterPortrait component on a cloned authored UI theme, room and counter. Full source overview and representative compressed bistro renders were visually inspected: transparent compositing, no obvious edge/crop defects, readable emotions. CSV hashes and existing metadata were checked before copying assets/database back. Five Default silhouette rows were retained.

Review outputs: external `C:/Users/TERRA/.codex/visualizations/2026/09/21/01a0c490-ad67-7e80-9c87-d625cf4b4e02/CharacterExpressions_20261122/Expressions-Overview.jpg`; ignored Unity captures/log under Tools/UI/ReviewProject (`expression-*.png`, `character-expressions.log`).

Not covered: live dialogue traversal, expression-transition animation across all poses, mobile/deployed-browser rendering, or new release download size. Next visual review should focus on pose-to-pose scale continuity during dialogue. Music was discussed but no music assets were generated or integrated; no music-generation service is connected in this session.
