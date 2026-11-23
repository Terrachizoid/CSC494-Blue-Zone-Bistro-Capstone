# AI playtest harness

`play.py` is a turn-by-turn text version of the game for AI (or human) playtesters. It shows only
what the Unity UI shows: no Traffic Light answers outside sorting feedback, no formulas, no hidden
customer stats. Every action and `note` is recorded. Rules come from `../BalanceSim/bzb_sim.py`
(a port of the C# services) plus events, sorting, tutorials, dialogue and endings read from the
real CSVs and `BalanceConfig.asset`.

```
python Tools/Playtest/play.py <session.pkl> new <seed> <project_root>
python Tools/Playtest/play.py <session.pkl> screen | recipes | plan R001 | close | find Grain | shelf S002 [SHELF] | flip L004 | add L004 2 | checkout
python Tools/Playtest/play.py <session.pkl> sort P001=G P013=R | bistro | preview R002 0=P017 1=P005 2=P008 | serve ... | skip | city | next
python Tools/Playtest/play.py <session.pkl> guests | pantry | sell P006 2 | news | info day|popularity|money | note <text>
python Tools/Playtest/analyze.py <folder with session .pkl files>
```

## Day flow (same as the game since 2026-11-13; ported here 2026-11-22)

- **Market:** shop at any open store and check out as often as you like. Each checkout with new foods goes to a
  quick sort (`sort ...`), then you are back at the market. `bistro` opens the bistro; nothing else ends shopping.
- **Recipe book and Today's plan:** `recipes` opens the book; `plan <RECIPE>` / `unplan <RECIPE>` (one dish per guest),
  `close` closes it. The market and service screens show the plan and what it still needs. `find <food type>` is the
  plan panel's navigation helper: one open listing of that type, its store and aisle.
- **Day 1 lessons:** lesson 1 (open the book, plan one affordable dish, close, enter a store, open a shelf, buy everything
  the plan needs) refuses other actions with the Guide's reason, like the game; lesson 2 needs one fully built dish.
- **Service:** story encounters, then today's guests not yet served. `city` leaves the bistro (BalanceConfig
  `cityDuringService`); the guest waits, and `bistro` brings you back to them. Pantry selling works at the market only.
- **Dialogue:** the full `DialogueService` ladder (request, missed request, fatigue, appeal, delight-full, red-served
  rungs, variants that never repeat back to back, Guide tips shown as `Guide:` lines, tokens). The closing dialogue
  plays right after the dish result.
- **Lessons:** the same flags and priorities as `TutorialController` (`[GUIDE]` lines). Lessons that name a click or a
  drag get a short "(Text mode: ...)" hint with the matching command. One lesson at a time: a text lesson raised while
  a day-1 lesson runs is dropped, so it shows the next time.
- **Events:** all effect kinds (store closure, food or shelf sold out, store/food/category prices, publicity, request
  weights, sorting refund boost, shelf life) with `EventService`'s rules for which events can run together.
- **Foods in two categories** (tofu: Beans or Meat substitute) count wherever a recipe or plan needs them.

## Human limits

`new <seed> <root> browse=N previews=N memory_words=N skim=true` makes the player behave more like a
person with 10–15 minutes:

- `browse`: shelf/flip/recipes/pantry/news/more actions per market visit; extra ones are refused
  ("You're getting impatient…").
- `previews`: dish previews per guest; extra ones are refused ("The guest is waiting…").
- `memory_words`: cap for `remember <text>`, the only note carried to the next day (`recall` shows it;
  it also appears at the next market). Run one agent per day for real forgetting (`personas.md`).
- `skim`: tutorials cut to 2 sentences and dialogue to 1, with `[...]`; `more` shows the rest.

- `forecast=true`: `preview` shows the full outcome (health, delight, pay). Default off, matching the game,
  where Review dish only checks the recipe match.

The farmer's food aid is mirrored (shown as `[FARMER]`). Market also has `sell <FOOD> [servings]` (pantry selling, soonest-to-spoil batches first, as in the game's Pantry SELL slot).

Defaults (no options) are unlimited, as in rounds 1–3.

## Running a round

1. Copy the project content (or point `<project_root>` at it). Edit CSV/config between rounds.
2. Start one agent per persona (`personas.md`), each with its own session file and a fixed seed per
   persona so rounds are comparable. A cheaper model is fine for players; analyse with a stronger one.
3. Agents may only run `play.py`; they must not read files or code.
4. Run `analyze.py` on the folder, read the persona reports, change CSV/config, repeat.

Cost observed (Oct 2026): ~80–100k tokens per six-day run, ~20–35 commands; human-limited day-split runs ~65k tokens per day (~400k per persona).

## Fidelity limits

Approximates the Unity game: dialogue selection follows `DialogueService` rules; tutorials are shown
as text at the matching moments; the shopping/first-meal tutorial gates are enforced. Not modelled:
visuals (colour grade, character expressions, music), drag-and-drop friction, voice, the city map, idle chatter
bubbles, Help replays. Random draws (events, guests, requests, variants) use the session seed, so they match
the game's rules but not a given Unity run. Without limits, AI players read every word and use previews
more than humans, so treat unlimited results as optimistic about clarity.
