# Tutorial framework

Six lessons use eight authored steps (`T_REQUESTS` added 2026-10-24: a text lesson triggered by `okinawa_chef_arrived` during day-4 service) (the rent lesson `T_RENT` was added 2026-10-22; it opens at the market after the first rent is charged and supports a `{rent}` text token). Run **Tools > Blue Zone Bistro > Import All CSV Content**, then Play and **New Game** to try the full introduction. Editor setup also refreshes tutorial content and assigns the database automatically.

| Lesson | Behaviour |
| --- | --- |
| Shopping (day-1 lesson 1, 2026-11-18) | Steps `recipes` → `plan` → `close` → `shop` → `store` → `shelf`, advanced by the player's action (entering a store and opening a shelf are steps, so no separate first-store/first-shelf tips). Before shopping, other clicks (map, top bar) still respond but the Guide says to plan first; the map can still be dragged. Inside the book the player may flip cards and plan/unplan (one dish); Close needs one planned dish. The player plans exactly one dish they can afford (others greyed), closes the book, then the first checkout is refused, with a Guide dialogue saying what's missing, until the plan panel shows nothing missing. The lesson ends at that checkout; the player is free until the bistro opens. Lesson 2 is FirstMeal (cook and serve any complete dish; the planned one is not required, Owen 2026-11-19). While a hard lesson runs, contextual tips (`tutorial.toolbar_*`, `tutorial.seen_*`) are dropped and their flag cleared, so they show the next time. |
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

- (2026-10-30) Lesson text rewritten shorter and friendlier (Owen: too much text, debug-sounding). T_SHOPPING no longer lists every recipe's requirements (`{examples}`); it points to Recipes. Mechanics detail (caps, thresholds already shown elsewhere, retry rules) was trimmed; framework wording in T_SORTING kept. The shopping checkout error also no longer lists every recipe. Customer dialogue now uses contractions; three disclaimer-style lines were rewritten (chef, Struggling and Bankrupt endings).

## Text lessons added 2026-11-18
One-shot TextOnly rows (priority 50–55), each triggered by its flag (`TutorialFlags`): Pantry (`seen_pantry`), Guests (`seen_guests`), clicking a store not open yet (`clicked_locked_store`) or closed by an event (`clicked_closed_store`), an event closing a store today (`store_closed_event`), new recipes this morning (`new_recipes`, day 2+; first time only for now), a second checkout in a day, the first sorting mistake, the first skipped guest, the first dish that didn't build its recipe, and the first trip to the city during service (easy mode).
Rules: text lessons wait for a safe moment (no guest dialogue or dish result showing) and show over any open panel without closing it. While a hard lesson (Shopping, FirstMeal) runs, a flag that would start a text lesson is cleared instead of queued, so that lesson shows the next time it happens. Store map regions that are greyed still answer a click (for the locked/closed lessons).

Click alternatives to dragging (2026-11-18): + on shelf cards (add a package), − on cart rows, + on pantry cards (one serving to SELL), − on rows in SELL and in meal ingredient slots.

## Idle chatter (2026-11-18)
Not a tutorial: a small non-blocking bubble under Help that fades out. Lines live in `Assets/Resources/Idle/IdleChatter.csv` (loaded at runtime; edit directly): `id, kind (click|idle), places (City;Store;Shelf;Sorting;Service;Results or Any), condition, weight, seconds, text`. Click lines show when Help is clicked with no lesson in progress; idle lines after ~12 s without a click, key or scroll, once per screen visit, at most every 30 s, never over a lesson, panel or guest dialogue. Conditions are named checks in `IdleConditions`, asked live when a line is picked (nothing stored, unlike tutorial flags). Join several with `&`. Plain checks: money_low/negative/rich, pantry_empty/full, expiring, no_plan, plan_missing/ready/full/room, cart_empty/some/big/over_budget, sort_none/some/all/perfect/mistakes, building, not_building, guest_request/no_request, cant_cook, last_guest, many_guests, dish_built/incomplete/healthy, first_day, last_day, popularity_high/low, store_closed. Checks with a value: store:ID, shelf:ID, day:N, flag:NAME, noflag:NAME, missing:Category, craving:Tag, recipe:ID, planned:ID. An unknown name just keeps that line from showing. The UI builds the snapshot (`IdleContext`) these read. 100 lines as of 2026-11-18 (79 click, 21 idle); Help clicks have a cooldown until the current line starts to fade. Placeholders: {money}; {category} {food} {store} {aisle} (a navigation suggestion for something the plan still needs). Blank seconds = by text length (3–8 s), then a 0.75 s fade.
Food expiring tomorrow is a one-time text lesson (T_EXPIRING) plus a click line, so it never nags.

### Guide and planning art — 2026-11-20
The existing non-blocking Guide chatter now uses guideNotePanel, and the draggable plan/suggestion use planPanel, panelPaper and suggestionFrame from the UI theme. The New sticker and Sold out stamp have live text. Blocking tutorial explanations still use the shared bottom dialogue; this is an artwork pass, not a change to tutorial progression or permission rules. Reuse the theme/helper contract documented in UIFramework.md.

## Supervisor notes follow-up (2026-11-20)
- `T_REQUESTS` now states the exact cost of an unanswered request with `{missedCraving}` (BalanceConfig `popularityPerMissedCraving`). The Comfort rule moved to its own lesson.
- `T_POPULARITY_DROP` (flag `tutorial.first_popularity_drop`, set by `GameLoop` when a correctly built dish still lowers popularity, which only happens through missed requests) explains why.
- `T_COMFORT` (flag `tutorial.first_comfort_request`, set in `TutorialController.Sync` when the current guest asks for Comfort) explains the `{comfort}` appeal threshold.
- The bistro's recipe choice now shows how each recipe fits the guest's requests: a green rim when it can answer every request, amber when it answers some or none, matching request badges filled, and a footer such as "Fits 1 of 2 requests". Comfort counts as possible on every recipe (it depends on appeal).
- Pending idea (Owen, after Dec 3): a lesson replay sandbox, where a lesson can be replayed from Help with temporary game state that never touches the real save. A lesson log in Help waits for it.
