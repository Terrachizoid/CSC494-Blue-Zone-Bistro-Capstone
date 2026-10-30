# AI playtest harness

`play.py` is a turn-by-turn text version of the game for AI (or human) playtesters. It shows only
what the Unity UI shows: no Traffic Light answers outside sorting feedback, no formulas, no hidden
customer stats. Every action and `note` is recorded. Rules come from `../BalanceSim/bzb_sim.py`
(a port of the C# services) plus events, sorting, tutorials, dialogue and endings read from the
real CSVs and `BalanceConfig.asset`.

```
python Tools/Playtest/play.py <session.pkl> new <seed> <project_root>
python Tools/Playtest/play.py <session.pkl> screen | shelf S002 | flip L004 | add L004 2 | checkout
python Tools/Playtest/play.py <session.pkl> sort P001=G P013=R | preview R002 0=P017 1=P005 2=P008 | serve ... | skip | next
python Tools/Playtest/analyze.py <folder with session .pkl files>
```

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
visuals, drag-and-drop friction, voice, the city map. Without limits, AI players read every word and use previews
more than humans, so treat unlimited results as optimistic about clarity.
