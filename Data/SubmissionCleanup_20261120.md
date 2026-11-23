# 2D submission cleanup — 2026-11-20

User requested external storage of postponed 3D work and a smaller current submission. No Git history rewrite, commit or push was performed.

## Archive and removal

External archive: `C:/Users/TERRA/Desktop/Blue Zone Bistro Archives/3D-20261120`. Its `SHA256.json` and `ArchivePlan.json` record 466 verified files: 457 removed files (43.48 MiB), plus nine original shared-file snapshots retained for restoration context. Each removed file was verified against its archive before deletion.

Removed generated city meshes, city runtime/editor tools, 3D materials/shaders/renderer, baked scene lighting, 3D previews/trials and recovery scenes referencing those assets. Retained the main scene, 2D artwork, UI shaders and gameplay. Removed the 3D map branch/theme fields, extra renderer reference and scene lighting references. A scan of Assets and ProjectSettings found no remaining references to the removed GUIDs.

To restore experiments, consult the external manifest and restore removed files with their original metas in a separate branch/project. Merge shared-file snapshots deliberately; do not overwrite newer UI code or theme settings wholesale.

## Source-size finding

Commit `47c97c3` had 454,517 additions. Of these, 398,510 lines across 1,575 files were Tools/Art/.deps Python libraries; 44,581 lines across 425 files were classified as 3D-related. Dependencies are now outside the repository at `C:/Users/TERRA/Desktop/Blue Zone Bistro Archives/Python-ImageTools-20261120`. Ignore rules cover .deps, .venv, __pycache__ and pyc. These deletions reduce the next snapshot, not already-pushed Git history.

## Web settings

Six panel sprites have WebGL overrides: max size 1024, automatic format, normal-quality compression. Nine-slice borders and GUIDs are preserved. Default imports also use compression. Project settings, the existing Web build profile and the build helper use release settings, Gzip, decompression fallback and data caching. The profile's historical filename still contains Development; its development flag is off.

No new distributable Web build was measured or published, so download-size savings are not yet established.

## Verification

- Disposable Unity review project compiled after 3D removal; PanelArtCapture logged PANEL ART PASS.
- WebGL-target CategoryColourCapture logged CATEGORY COLOUR PASS for original/category variants of three stores. This checks rendered layout, not browser interaction or a deployed build.
- Test-ContentImport.ps1 passed during this work. GameOperations and CsvContent checks remain blocked by the existing Tools/ComfortFatigueChecks.cs:48 float-to-int compilation error.
- Main scene lighting removal consists of clearing LightingDataAsset and LightingSettings. No gameplay rules were changed.

## Colour study

Live food art and CSV paths are unchanged. 63 filtered front/back preview pairs and three store comparisons are stored outside the repository under the Codex visualizations folder, CategoryColours_20261120. Tools/Art/CategoryPalette.csv defines experimental recipe-category colours, unrelated to Traffic Light classification. Fronts are flattened images, so selective filtering is approximate; review before production replacement. Run PreviewCategoryColours.py with external --deps and --output paths; set BZB_COLOUR_PREVIEW_DIR for the disposable capture harness.

Colour-study follow-up: user approved the comparisons and 126 food PNGs were replaced in place. Original images and application hashes are stored externally; .meta hashes were verified unchanged. This supersedes the preview-only status above.
Post-application verification: disposable WebGL-target Unity import/compile and three-store capture passed (Tools/UI/ReviewProject/category-applied.log). Reviewed the imported Verdant shelf. This does not establish deployed browser interaction.

## Compression verification — 2026-11-22
The user's successful release rebuild at Documents/UofT/Build contains 33,404,243 bytes across its four build files (31.86 MiB), versus the recorded same-directory baseline 123,892,016 bytes (118.15 MiB): 73.0% smaller. This is a before/after observation, not a perfectly controlled benchmark; concurrent dialogue/character work exists. The surrounding UofT folder contains unrelated lecture PDFs and must not be used as the game-size total.

Latest Unity report: textures 67.3 MiB (previously 132.3), all user assets 70.9 MiB, complete build 31.9 MiB, meshes zero. Thirteen event icons now report 1 MiB each, down from 6 MiB. Web overrides are 1024, automatic compressed format, quality 75; originals and GUIDs are retained. Release profile confirms Development off, Gzip, fallback and caching on. No additional runtime conversion or gameplay changes were needed.

Fresh disposable WebGL-target EventPaperCapture passed all 13 current event images and newspaper/Codex/tower/close checks. Its legacy log says 'five illustrations', but the fixture loops the database and generated 13 event captures. Harvest and Festival captures visually reviewed at expanded display size; grain, silhouettes and edges remain readable. Log: Tools/UI/ReviewProject/events-compressed.log. This is an Editor rendering/navigation test, not a deployed Safari/browser test. Reused the user's completed release build as build evidence rather than starting a redundant build while dialogue development continues.

Remaining optional size candidates: store logos S002/S003 (2.6/2.4 MiB imported), suggestionFrame (2.4 MiB), guideNotePanel (1.3 MiB), and extra TMP resources. These are not all proven uncompressed, nor are their imported sizes download savings. No further blanket resolution reduction was applied. Character/dialogue framework and CSV work were left untouched.
