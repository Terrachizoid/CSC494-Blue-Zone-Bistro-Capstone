# Expandable endings

## Quick preview without playing

Open **Tools → Blue Zone Bistro → Preview Endings** (Play Mode is not required). Enter popularity and community wellbeing, or use the four quick-score buttons. The window calls the real EndingService on a detached state and shows the selected ID, priority, artwork and dialogue. Use Continue/Previous to inspect changing images. It never modifies the running session or saved checkpoint. This is an Editor content preview, not the in-game Canvas layout. Reimport edited CSVs before pressing Re-evaluate; presets use the initial thresholds and do not override selection rules.

`Blue_Zone_Bistro_Endings.csv` registers endings. Optional `min_money`/`max_money` columns (may be negative; min inclusive, max exclusive) were added 2026-10-22 for `E_BANKRUPT` (money < −10, priority 200); see `Economy.md`. Current prototype thresholds are 750 popularity and 70 community wellbeing. The four rows represent thriving/healthy, niche/healthy, popular/unhealthy, and struggling. These are fictional game outcomes; thresholds still need playtesting.

Each row has a stable `ending_id`, title, optional score bounds, integer priority, boolean fallback, dialogue ID, and optional image asset path. Minimums are inclusive; maximums are exclusive. Blank bounds are unrestricted. Exactly one row must be the fallback. The struggling row is unrestricted with lowest priority so it also safely covers gaps.

`EndingService` selects the highest-priority matching row, then the explicit fallback if nothing matches. Equal priorities use ordinal ending-ID order. Missing content returns no ID; the presentation still offers a completed-run message and Main Menu. Import errors preserve the existing database. Priority resolves overlapping authored conditions, but is not a replacement for reviewing their intent.

Final-day completion runs selection before the checkpoint event. `GameSessionState.endingId` persists the choice; reopening a completed run replays its story from the beginning without recalculating the ending. Older completed saves select once from their saved scores. No intermediate slide progress is saved.

`GameContentQueries.GetEnding` supplies presentation data. `BistroUIScreen.Ending` displays the title, image, ordered dialogue text, Continue and Main Menu. Ending playback is presentational: it does not execute customer dialogue flags or repeat meal effects. Author ending sequences with trigger `Ending`; this keeps them out of ordinary encounter scheduling.

The dialogue-lines CSV has an optional `background_image_path`. On each line, a nonempty imported sprite replaces the background; blank keeps the preceding image (or the ending's default). PNGs must be imported as Sprite (2D and UI). Twelve geometric placeholder city images are supplied under `Assets/Art/Endings`; replace them with final art later. Blank CSV art paths preserve manually assigned references on reimport, consistent with other content importers. To clear a manual reference, clear it on the asset too.

Run **Tools → Blue Zone Bistro → Import All CSV Content** after editing tables. Ending import runs after dialogue import. The editor connects the default EndingDatabase to GameBootstrap, and player builds use that serialized reference.

To add an ending: add its dialogue sequence/lines and a CSV ending row, choose conditions and priority, then import. Do not create another fallback unless replacing the existing one. Video playback and animated illustrations are not implemented.

Validation: `EndingChecks.Run` is intended for an isolated Unity test project; it deliberately feeds invalid content to test atomic rejection. It checks boundaries, overlaps, fallback gaps, saved selection, twelve image references and missing content. Existing gameplay/CSV test suites also cover the added dialogue trigger/content.
