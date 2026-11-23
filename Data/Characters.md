# Characters and expressions

Added 2026-11-22. The service stage shows one character behind the counter, with a face that follows the dialogue.

## How a face is chosen

- Each dialogue line has an `emotion` (DialogueLines.csv). The stage looks up that character's art for it.
- A blank emotion keeps the current face. Narrator lines keep whoever is standing there.
- After serving, while the dish result shows, the guest already wears the first emotion of the closing
  dialogue they are about to say. GameLoop picks that dialogue when the dish is served
  (`ServiceSessionState.pendingAfterDialogue`) and plays the same one after Continue.
- A new guest slides in; the previous one slides out. A skipped guest slides out with no dialogue.
- Fallbacks, in order: a line's own `portrait_path` > expression art > the customer's `portrait_path` > silhouette.
  Expressions are whole-body images; all motion moves the whole image (breathing = ~1% scale from the feet).
  If only some expressions are drawn, a missing one shows that character's neutral art, and an expression change
  still bounces.
- **Base fallback in the CSV:** rows with `character_id` `Default` (`Default_neutral` ... `Default_tired`) are the base
  for any character without art of their own. With no `image_path` they colour the built-in silhouette (`tint`,
  #RRGGBB); give them an image and that image replaces the silhouette for everyone. The silhouette moves like art.

## Adding art

1. Draw one whole image per expression, the same canvas size for all of a character's images, feet/bottom aligned.
   Put it in `Assets/Art/Characters/` (imported as a sprite automatically, 1024 px Web override).
2. Fill a row in `Assets/GameData/CSV/Blue_Zone_Bistro_CharacterExpressions.csv` (rows for U001 to U006 and the
   Visiting Chef already point at `Art/Characters/<id>_<expression>.png`).
3. Run Tools > Blue Zone Bistro > Import All CSV Content. Missing files are listed in one summary line; the game shows
   the old portrait until the art exists.

| Column | Meaning |
|---|---|
| `expression_id` | Unique row ID, `<character>_<expression>` (`U001_happy`, `VisitingChef_neutral`). Blank = built from the next two columns. |
| `character_id` | Customer ID (`U001`) or a dialogue speaker ID (`Visiting Chef`). |
| `expression` | Row name: `neutral`, `happy`, `concerned`, `disappointed`, `tired`, or any new name. A dialogue emotion with the same name matches it directly. |
| `emotions` | Other dialogue emotions this image covers, `|` separated (`Happy|Pleased|Welcoming`). Unknown emotions use `neutral`. |
| `image_path` | Whole character for this expression. |
| `part_paths` | Optional. Same-size layers, back to front (`body|face|hair`). When set they replace the whole image and sway slightly apart. |
| `video_path` | Optional. Looping clip relative to `Assets/StreamingAssets/` (e.g. `Characters/U001_happy.mp4`). Shown once its first frame is ready; the still shows until then, and stays if the clip fails. |
| `tint` | Optional `#RRGGBB`: silhouette colour for this expression (used by the `Default` rows). |
| `notes` | Free text. |

Dialogue emotions in use: Neutral, Welcoming, Cautious, Happy, Pleased, Concerned, Disappointed, Tired (energy-crash
arrivals). New emotions need no code: add the word to a line and to a row's `emotions`.

## Video notes (Web build)

- Files under StreamingAssets are downloaded only when needed, so they do not grow the initial download much, but
  keep clips short (2 to 4 s loops) and small (H.264 MP4, about 720 px tall).
- MP4 has no transparency. Either render the clip on the bistro background crop, or use WebM VP8 with alpha, which
  Safari does not play (it falls back to the still). The whole-image path works everywhere.
- At most three clips are kept loaded; the oldest is released.

## Code

- Data: `Scripts/Data/CharacterExpressionDatabase.cs`; importer: `Editor/Importers/CharacterExpressionCsvImporter.cs`;
  asset: `Assets/GameData/Characters/CharacterExpressionDatabase.asset` (wired into GameBootstrap by Setup).
- Stage rules: `Scripts/UI/BistroUIScreen.Characters.cs`. Drawing and motion: `Scripts/UI/BistroCharacterPortrait.cs`
  (motion runs from stored timestamps, so screen rebuilds never restart it; `reducedMotion` keeps only short fades).

### Default portraits integrated — 2026-11-22
Seven individual neutral PNGs now live at the existing CSV paths under Assets/Art/Characters. The six regulars' remaining expressions were subsequently completed; see the full-set handover below. Visiting Chef has one neutral row covering Neutral/Happy/Pleased/Welcoming (and the existing neutral fallback covers other emotions); no separate happy artwork is needed. No dialogue text or runtime code changed for this art integration.

June and Dev were revised after user feedback about repeated hand-at-waist poses: June arms down; Dev loosely folded arms. Other default poses remain distinct. Source files and prompt constraints are recorded in CharacterNeutralArt_20261122.md.

Verification: disposable WebGL-target Unity compile/import/render passed for seven sprites and six emotion fallbacks per character; chef has one row. Authored room/counter and actual BistroCharacterPortrait component used. Dev/chef compositing inspected and transparent outlines render cleanly, despite brown RGB haze visible in raw generation previews. Imported character database and new metas transferred only after CSV/GUID checks. No live Play Mode or deployed-browser test in this pass. Current Web import reports ~913x1024 RGBA32 rather than block-compressed textures; download-size impact has not been measured and remains a later import optimisation candidate.
Scene handoff: SampleScene lacked the newly introduced characterExpressions reference. Added only that serialized database reference to GameBootstrap, using the existing asset GUID; equivalent to the existing Setup auto-fill. No runtime logic change.

### Full expression set and verified compression — 2026-11-22
All 24 remaining portraits are now at the existing CSV paths and imported into the database: six regulars × five states, plus one chef default (31 PNGs). Existing Default silhouette/tint rows are preserved. Natural pose variation accompanies expressions. All portraits, including neutrals, now use proportionally prepared transparent 1024-square canvases; Unity WebGL imports all 31 as DXT5 with no mipmaps. This supersedes the earlier RGBA32 limitation above. Total PNG bytes: 17,084,877. No new release download measurement. See CharacterExpressionArt_20261122.md for provenance, verification and limits. No runtime or dialogue edits in this expression-art pass.
