# Expandable endings

## Quick preview without playing

Open **Tools → Blue Zone Bistro → Preview Endings** (Play Mode is not required). Enter popularity and community wellbeing, or use the four quick-score buttons. The window calls the real EndingService on a detached state and shows the selected ID, priority, artwork and dialogue. Use Continue/Previous to inspect changing images. It never modifies the running session or saved checkpoint. This is an Editor content preview, not the in-game Canvas layout. Reimport edited CSVs before pressing Re-evaluate; presets use the initial thresholds and do not override selection rules.

`Blue_Zone_Bistro_Endings.csv` registers endings. Optional `min_money`/`max_money` columns (may be negative; min inclusive, max exclusive) were added 2026-10-22 for `E_BANKRUPT` (money < −10, priority 200); see `Economy.md`. Current prototype thresholds are 750 popularity and 65 community wellbeing (Blue/Popular split at 65, Niche 60+). Lowered on 2026-10-25 after AI round 8: the Daily Dozen novelty rule lowers health gains, and no tester reached Blue at 70. The five rows represent thriving/healthy, niche/healthy, popular/unhealthy, struggling, and bankrupt. These are fictional game outcomes; thresholds still need playtesting.

Each row has a stable `ending_id`, title, optional score bounds, integer priority, boolean fallback, dialogue ID, and optional image asset path. Minimums are inclusive; maximums are exclusive. Blank bounds are unrestricted. Exactly one row must be the fallback. The struggling row is unrestricted with lowest priority so it also safely covers gaps.

`EndingService` selects the highest-priority matching row, then the explicit fallback if nothing matches. Equal priorities use ordinal ending-ID order. Missing content returns no ID; the presentation still offers a completed-run message and Main Menu. Import errors preserve the existing database. Priority resolves overlapping authored conditions, but is not a replacement for reviewing their intent.

Final-day completion runs selection before the checkpoint event. `GameSessionState.endingId` persists the choice; reopening a completed run replays its story from the beginning without recalculating the ending. Older completed saves select once from their saved scores. No intermediate slide progress is saved.

`GameContentQueries.GetEnding` supplies presentation data. `BistroUIScreen.Ending` displays the title, image, ordered dialogue text, Continue and Main Menu. Ending playback is presentational: it does not execute customer dialogue flags or repeat meal effects. Author ending sequences with trigger `Ending`; this keeps them out of ordinary encounter scheduling.

The dialogue-lines CSV has an optional `background_image_path`. On each line, a nonempty imported sprite replaces the background; blank keeps the preceding image (or the ending's default). PNGs must be imported as Sprite (2D and UI). Five individual illustrated placeholders (`Ending_BLUE`, `Ending_POPULAR`, `Ending_NICHE`, `Ending_BANKRUPT`, `Ending_STRUGGLING`) are supplied under `Assets/Art/Endings`. Each is referenced by its ending CSV row and every line of its ending dialogue. Seven unreferenced legacy illustrations were removed after path/GUID checks. The five previously retained event illustrations were subsequently migrated to Art/Events and removed too. Blank CSV art paths preserve manually assigned references on reimport, consistent with other content importers. To clear a manual reference, clear it on the asset too.

Run **Tools → Blue Zone Bistro → Import All CSV Content** after editing tables. Ending import runs after dialogue import. The editor connects the default EndingDatabase to GameBootstrap, and player builds use that serialized reference.

To add an ending: add its dialogue sequence/lines and a CSV ending row, choose conditions and priority, then import. Do not create another fallback unless replacing the existing one. Video playback and animated illustrations are not implemented.

Validation: `EndingChecks.Run` is intended for an isolated Unity test project; it deliberately feeds invalid content to test atomic rejection. It checks boundaries, overlaps, fallback gaps, saved selection, authored image references and missing content. Existing gameplay/CSV test suites also cover the added dialogue trigger/content.

### Illustrated ending presentation (2026-10-30)
The ending now fills the scene with cover-fitted artwork, a small decorated epilogue title and the shared bottom dialogue/action buttons. Recap and saved-selection logic are unchanged. No characters are depicted. These are integrated review placeholders, not approved final 3D scenery. Art remains individually CSV-owned; no runtime ID-to-art lookup was added.

Verification: content-import checks passed; disposable Unity ShelfCapture compiled and rendered all five endings, checked every dialogue image reference and non-mutating recap. Screenshots: Data/Previews/2026-10-30-Endings. Live sprite references were compared against the actual isolated CSV import. No full six-day playthrough or Web build was run.

The ending title uses the decorated cream surface; story and recap use the shared bottom dialogue fade. Continue/Finish story/Main menu use Primary actions; Back and Retry save use Secondary actions. The dark dialogue fade is the approved current dialogue style, not the retired solid-block panel.
