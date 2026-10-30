# Checkpoints, main menu and rush/crash

The game opens at a main menu. New Game replaces the active checkpoint only after
confirmation and a successful initial save. Starting money comes from BalanceConfig.
Resume reconstructs services around the saved session directly; it never replays actions.

Checkpoints are captured once after FinishDay applies expiry, finance history and the
next day's visual state. Day 1 completion saves Day 2 Market. Closing during Day 2
reverts to that checkpoint. Returning to the menu warns about this; it does not save
mid-day. Completion is saved with the final day summary and resumes as Completed.

CheckpointStore uses versioned JSON, a checksum and a previous-checkpoint recovery copy.
Unity PlayerPrefs supplies local browser/desktop storage. Browser saves belong to that
browser/site and can be removed by clearing site data; no account/cloud sync exists.
Save errors remain visible with a retry button. Retry writes the captured checkpoint,
not whatever unfinished state the player has reached since then. Saves with missing
food/customer definitions are rejected on resume rather than silently losing data.

Saved: inventory identity/source/cost/expiry, customer health/delight/finance/history,
flags, dialogue history, learned foods, rush day, visual score and pending
crash dialogue. Cart/selection/active-dialogue cursors and receipts are not restored.

## Presentation-only rush/crash

Serving a red ingredient, in a built or failed recipe, sets visualScore to +10 and
rushDay to the current day. No extra health, delight or finance change is introduced.
At the next day boundary visualScore becomes -15 and crashDialoguePending becomes true.
The following day the offset returns to zero. The pending dialogue persists until the
customer returns and finishes the crash greeting. It replaces the regular arrival
conversation, while existing before-service introductions still run afterward.

Special conversations are authored in DialogueSequences.csv and DialogueLines.csv using
RushAfterService and CrashArrival triggers. Each customer has their own two sequences.
The scheduler chooses the trigger, while DialogueService applies character identity,
eligibility, priority, conditions and history as usual. Missing/ineligible/empty special
conversations are skipped, never generated or replaced with ordinary dialogue. A missing
crash conversation leaves crashDialoguePending true for a later visit; the education flag
is not set until an authored crash conversation actually completes. Existing before-service
introductions may still play independently after the arrival stage is skipped.

visualScore is an OFFSET intended for future portrait presentation, not a second health
score. No saturation shader or tutorial overlay has been added in this change.

Stable tutorial flags:
- tutorial.first_purchase: successful nonempty purchase.
- tutorial.first_served: committed meal, including a failed recipe.
- tutorial.first_recipe_built: committed successfully built recipe.
- tutorial.first_crash_dialogue_completed: crash dialogue's final line acknowledged.

Attendance guarantees one eligible non-chef customer if all ordinary rolls fail. The
forced selection is cached; subsequent queries cannot create an extra customer after
one has already been served. No visitor is invented when no customer is unlocked.

## Verification

Tools/Test-CsvContent.ps1 includes ProgressChecks for attendance, visual-only state,
pending dialogue, completion flags and day completion. The Unity menu
Tools > Blue Zone Bistro > Test Checkpoint Round Trip checks real JsonUtility and
PlayerPrefs with unique temporary keys; it does not touch the player's checkpoint.

Manual browser check: New Game, finish Day 1, spend some money on Day 2, close/reopen,
Resume, and confirm Day 2's starting money/pantry are restored. Verify flags/history
are retained and previously learned foods stay learned. Browser persistence and menu
interactions require this check even when editor serialization tests pass.

Verification completed: real Unity batch-mode checkpoint smoke test passed in an isolated
project, including private flags/history serialization, snapshot isolation, previous-save
recovery after corruption, checksum rejection and completed-run summary restoration.
The browser close/reopen and main-menu interactions still require Play Mode/Web testing.


2026-10-27 presentation update: main menu now uses city art and a cream welcome panel. Removed the always-visible technical storage explanation. Return confirmation retains the consequence in plain language: restarting today. New-game replacement confirmation, resume and save-error handling are unchanged.
