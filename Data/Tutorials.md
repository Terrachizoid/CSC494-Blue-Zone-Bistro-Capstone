# Tutorial framework

Six lessons use eight authored steps (`T_REQUESTS` added 2026-10-24: a text lesson triggered by `okinawa_chef_arrived` during day-4 service) (the rent lesson `T_RENT` was added 2026-10-22; it opens at the market after the first rent is charged and supports a `{rent}` text token). Run **Tools > Blue Zone Bistro > Import All CSV Content**, then Play and **New Game** to try the full introduction. Editor setup also refreshes tutorial content and assigns the database automatically.

| Lesson | Behaviour |
| --- | --- |
| Shopping | Checkout requires enough pantry plus cart ingredients for at least one recipe on today's menu (any recipe; the cheapest affordable one is shown as an example). Extra purchases and pantry inspection remain available. |
| Sorting | Read the Traffic Light and refund introduction, then continue to answer normally. |
| Cooking | Select any recipe on today's menu, build it successfully, and serve. Unknown/locked recipes, skipping the customer, and committing an unsuccessful meal are blocked. Previewing and changing ingredients are allowed. After serving, read feedback about the actual ingredients, health change and Daily Dozen coverage. |
| Rush/crash | Read the explanation after the first completed customer crash dialogue. Serving red food alone does not trigger this lesson. |

## Ownership

- `Assets/GameData/CSV/Blue_Zone_Bistro_Tutorials.csv` owns lesson IDs, trigger flags, activation phases, priorities, step wording and completion flags. Repeated rows form one lesson.
- `TutorialCsvImporter` validates all rows before replacing `TutorialDatabase`. Unknown behaviours, inconsistent metadata, missing steps and duplicate steps reject the import.
- `TutorialController` observes shared session flags, queues lessons, chooses an eligible lesson and records completion or skipping. It knows no buttons or screen coordinates.
- `TutorialBehaviours` defines procedural steps, permitted actions and feasibility. Shopping and cooking each have a behaviour; text lessons share one.
- `IntroMealPlan` defines the first-meal rule shared by shopping and cooking: any recipe unlocked today counts (`Candidates`), and `Recipe`/`Requirements` give the cheapest completable one as the example for `{recipe}`/`{requirements}` text (2026-10-22; previously fixed to R001). `Examples` (`{examples}`, 2026-10-24) lists every recipe unlocked today, read from Recipes.csv, as a sentence, e.g. "Sandwich needs 2 Grain and 1 Vegetable; Bean and grain bowl needs 1 Beans, 1 Grain and 1 Vegetable; or Oat and fruit breakfast needs 1 Grain and 1 Fruit". Nothing is hard-coded: changing day-1 recipes changes the text. The shopping lesson and the blocked-checkout message use it so a single example is not read as the requirement. Card backs show each food's category (`IntroMealPlan.CategoryName`, e.g. "Starchy vegetable"). Its feasibility check considers pantry stock, available listings, package sizes and money without buying anything.
- `GameLoop` checks tutorial permission before committing progression commands, then notifies the controller only after success. Preview remains available.
- `BistroUIScreen.Tutorial` renders instructions and acknowledgement. The header's Pantry and Menu remain accessible. The instructions button reopens help for an interactive step.

## Scheduling and extension

A trigger flag makes a lesson pending, not immediately visible. Activation also requires the matching phase and a safe boundary; cooking waits for meal selection after dialogue. Existing flags are rechecked when a session is rebuilt, so event timing alone does not decide whether a lesson runs.

Add text lessons with behaviour `TextOnly` and step `explain`. More complicated lessons implement `ITutorialBehaviour` and register their behaviour ID in `TutorialController`. Keep instruction text in CSV and action rules in the behaviour. Supported text substitutions include `{recipe}`, `{requirements}`, `{examples}`, `{maxDiscount}`, `{healthDelta}`, `{dailyDozen}` and `{ingredients}`.

Unavailable ingredients, unaffordable introduction, missing step text or unknown behaviour skip the lesson with a recorded reason and release restrictions. Skipping does **not** set successful-completion flags. Authoring errors should still be fixed; skipping is protection against trapping a player.

## Persistence and verification

Tutorial records belong to `GameSessionState`. Completed/skipped records are saved with the existing day-start checkpoint. Quitting mid-day intentionally returns to that day's beginning and can replay lessons completed since the checkpoint. Older saves tolerate the new list; introductions are not forced when their first-action milestone has already passed.

`Tools/TutorialChecks.cs`, included in `Tools/Test-CsvContent.ps1`, exercises the connected four-lesson flow, blocked commands without state mutation, acknowledgements, completed-record reconstruction and infeasible shopping. `Tools/Test-ContentImport.ps1` checks the real CSV and rejects malformed definitions. `CheckpointSmokeTest` checks tutorial records through real Unity JSON and PlayerPrefs alongside the existing checkpoint checks.

Manual Play Mode check: start a new game, try an insufficient cart, buy ingredients for any one recipe, acknowledge sorting, preview an invalid meal, then serve that recipe built correctly and acknowledge feedback. Later serve red food, finish the day and wait for that customer's crash dialogue; its completion should open the crash lesson. Check Pantry and Menu remain usable during each explanation.

## Economy lessons (2026-10-24)

- `T_RENT` now triggers on `tutorial.rent_upcoming`, which `TutorialController.Sync` sets on day
  `rentStartDay − 1` when rent is above zero, so players hear about rent a day early.
- `T_SELL` triggers on `tutorial.money_low`, set by `Sync` once rent has started and money is below one day's rent
  (changed from `money_negative` after AI round 6: the lesson came too late to save spoiling food). It explains
  the Pantry SELL slot and uses the `{sellRate}` token (`pantrySellRate` as a percentage).
- Both are plain `TextOnly` lessons: they complete when the player presses Continue. No new behaviour.
- `TutorialFlags` also has `PantrySold` (set by the first sale) for future content.
- `T_FARMER` (Service phase) triggers on `tutorial.food_aid_given`, set by `FoodAidService` on the first gift.
  Later gifts only show the farmer line on the meal screen and in results.
- Flags derived from state live in `Sync`, so new CSV lessons can react to the economy without code.


### Compact dialogue and cooking update — 2026-10-28
User rejected the misty cream gradient panels and full-page guide. `BistroUIScreen.DialoguePanel.cs` now provides a shared bottom dark fade, speaker/name column, light text, pagination, final action and optional Back. Dialogue, tutorial instructions, guest information, dish review/result, day summary and return-to-menu confirmation use it. Browsing interfaces remain card/grid views.

Cooking buttons and ingredient slots use the illustrated palette; seated ingredient miniatures use their food artwork. The persistent “Comfort needs…” line was removed. The existing CSV request lesson explains Comfort using `{comfort}`, expanded from `BalanceConfig` through `IntroMealPlan`; no scoring rule changed. Tutorial review reads encountered lesson text without replaying actions. City Recenter was removed and replaced with Tutorial; toolbar Help is always available.

Recipe backs keep background-only art. Customer silhouette is still a placeholder. Future 3D replacement must retain the independent overlay structure. Earlier notes approving the cream dialogue veil are superseded by this decision.

## Top-bar lessons (2026-10-28)

- `T_DAY`, `T_POPULARITY` and `T_WALLET` are TextOnly lessons triggered by clicking Day, Popularity or Wallet in the top bar. The click calls `GameLoop.InspectToolbar(item)`, which sets `tutorial.toolbar_<item>` (`TutorialFlags.ToolbarPrefix`). Each lesson runs once; later clicks change nothing. If another lesson is active, the clicked one waits until it ends.
- `activation_phase` may now be `Any` (shown on whichever screen the player is on). Priority 110 puts a clicked lesson ahead of other pending ones.
- New text tokens, all read from BalanceConfig: `{totalDays}`, `{rentStart}`, `{cravingsStart}`, `{maxGuests}`, `{guestThresholds}` (e.g. "250, 500 and 750"), `{walkInThreshold}`, `{walkInPer100}` (walk-in income per 100 popularity above the threshold).
- Help (top bar) repeats only the lesson in progress; with none active it says all guides are complete for now. The separate Tutorial button on the city map was removed.
- (2026-10-28) An action step's instruction (e.g. the day-1 shopping list) opens by itself only once. `TutorialRecord.seenStepId` remembers it, so when a clicked toolbar lesson pauses the shopping lesson, the shopping instruction does not pop up again on resume. Before, the "already shown" memory was one UI variable that the other lesson overwrote. Help still shows it on demand.
