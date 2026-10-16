# Tutorial framework

Four lessons use six authored steps. Run **Tools > Blue Zone Bistro > Import All CSV Content**, then Play and **New Game** to try the full introduction. Editor setup also refreshes tutorial content and assigns the database automatically.

| Lesson | Behaviour |
| --- | --- |
| Shopping | Checkout requires enough pantry plus cart ingredients for the introductory sandwich. Extra purchases and pantry inspection remain available. |
| Sorting | Read the Traffic Light and refund introduction, then continue to answer normally. |
| Cooking | Select the introductory recipe, build it successfully, and serve. Wrong recipe selection, skipping the customer, and committing an unsuccessful meal are blocked. Previewing and changing ingredients are allowed. After serving, read feedback about the actual ingredients, health change and Daily Dozen coverage. |
| Rush/crash | Read the explanation after the first completed customer crash dialogue. Serving red food alone does not trigger this lesson. |

## Ownership

- `Assets/GameData/CSV/Blue_Zone_Bistro_Tutorials.csv` owns lesson IDs, trigger flags, activation phases, priorities, step wording and completion flags. Repeated rows form one lesson.
- `TutorialCsvImporter` validates all rows before replacing `TutorialDatabase`. Unknown behaviours, inconsistent metadata, missing steps and duplicate steps reject the import.
- `TutorialController` observes shared session flags, queues lessons, chooses an eligible lesson and records completion or skipping. It knows no buttons or screen coordinates.
- `TutorialBehaviours` defines procedural steps, permitted actions and feasibility. Shopping and cooking each have a behaviour; text lessons share one.
- `IntroMealPlan` shares the introductory recipe between shopping and cooking. Change `RecipeId` here to change that recipe. Its feasibility check considers pantry stock, available listings, package sizes and money without buying anything.
- `GameLoop` checks tutorial permission before committing progression commands, then notifies the controller only after success. Preview remains available.
- `BistroUIScreen.Tutorial` renders instructions and acknowledgement. The header's Pantry and Menu remain accessible. The instructions button reopens help for an interactive step.

## Scheduling and extension

A trigger flag makes a lesson pending, not immediately visible. Activation also requires the matching phase and a safe boundary; cooking waits for meal selection after dialogue. Existing flags are rechecked when a session is rebuilt, so event timing alone does not decide whether a lesson runs.

Add text lessons with behaviour `TextOnly` and step `explain`. More complicated lessons implement `ITutorialBehaviour` and register their behaviour ID in `TutorialController`. Keep instruction text in CSV and action rules in the behaviour. Supported text substitutions include `{recipe}`, `{requirements}`, `{maxDiscount}`, `{healthDelta}`, `{dailyDozen}` and `{ingredients}`.

Unavailable ingredients, unaffordable introduction, missing step text or unknown behaviour skip the lesson with a recorded reason and release restrictions. Skipping does **not** set successful-completion flags. Authoring errors should still be fixed; skipping is protection against trapping a player.

## Persistence and verification

Tutorial records belong to `GameSessionState`. Completed/skipped records are saved with the existing day-start checkpoint. Quitting mid-day intentionally returns to that day's beginning and can replay lessons completed since the checkpoint. Older saves tolerate the new list; introductions are not forced when their first-action milestone has already passed.

`Tools/TutorialChecks.cs`, included in `Tools/Test-CsvContent.ps1`, exercises the connected four-lesson flow, blocked commands without state mutation, acknowledgements, completed-record reconstruction and infeasible shopping. `Tools/Test-ContentImport.ps1` checks the real CSV and rejects malformed definitions. `CheckpointSmokeTest` checks tutorial records through real Unity JSON and PlayerPrefs alongside the existing checkpoint checks.

Manual Play Mode check: start a new game, try an insufficient cart, buy the required sandwich ingredients, acknowledge sorting, try choosing another recipe and previewing an invalid meal, then serve a valid sandwich and acknowledge feedback. Later serve red food, finish the day and wait for that customer's crash dialogue; its completion should open the crash lesson. Check Pantry and Menu remain usable during each explanation.
