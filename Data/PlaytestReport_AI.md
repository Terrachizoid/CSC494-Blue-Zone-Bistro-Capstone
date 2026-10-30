# AI playtest report: rounds 1–7 (2026-10-24)

Five AI personas with no knowledge of the game and different partial nutrition beliefs (see
`Tools/Playtest/personas.md`) each played one full six-day run per round in the text harness
(`Tools/Playtest`). Same seed per persona across rounds. Content/config were adjusted between rounds;
no game mechanics were changed (one display fix). Rounds 1–3 cost ≈ 1.6M tokens (15 valid runs plus 4 runs
lost to a harness bug in round 3, which was fixed and re-run). Round 4 (30 one-day runs) cost ≈ 2M tokens.

## Results by round

| Persona | Round 1 | Round 2 | Round 3 | Round 4 (human limits) |
| --- | --- | --- | --- | --- |
| Maya (impulsive student) | Niche, 56 | Blue, 61 | Blue, 59 | Blue, 73 |
| Daniel (budget dad) | Blue, 104 | Blue, 76 | Blue, 74 | Blue, 71 |
| Ruth (careful retiree) | Blue, 135 | Struggling, 40 | Blue, 56 | Struggling, 2 |
| Kai (gamer) | Blue, 116 | Niche, 69 | Niche, 59 | Blue, 77 |
| Lena (protein-first trainer; foodie in round 4) | Blue, 115 | Blue, 64 | Blue, 55 | Blue, 57 |

Sorting accuracy stayed 80–100%. Typical mistakes match persona beliefs: oats/banana/bread → Yellow
(carb fear), walnuts → Yellow (fat), nuggets/eggs/olive oil → Green (labels and beliefs), chips and
cereal → Yellow. Non-green servings cooked: 0 in rounds 1 and 3; Ruth used olive oil in round 2.

## Changes made

| Round | Change | Why |
| --- | --- | --- |
| after 1 | Walk-in income 0.08 → 0.05 per popularity above 450 | Walk-ins exceeded dish income; money ended 100–135 |
| after 1 | Publicity event +500 → +200 | +500 jumped straight to 4 seats; dominated 4 of 5 runs |
| after 1 | New event: CheapCart closes for a day | Events never mattered because only Verdant could close |
| after 1 | New lesson `T_REQUESTS` when the chef arrives (day 4) | Players learned about requests only by accident |
| after 2 | `sorting_explanation` for all 54 foods (Foods.csv) | Every persona asked why; "explanation pending" taught nothing |
| after 2 | Results show "(guest was already fully delighted)" when delight +0 | "Delight +0" on a great dish confused 4 personas |

Sorting explanations use only the plan's Traffic Light definitions (whole plant food / processed
plant food / unprocessed animal food / ultra-processed or processed animal food) and say that
marketing labels don't change the class. They give no "moderation" or "avoid" advice, so they don't
pre-empt the decision on the sorting tutorial wording. Pending supervisor review.

## What players said (round 3, after changes)

- Explanations worked: Lena and Maya understood their wrong answers (whole plant food, nothing added;
  eggs are an unprocessed animal food).
- Money: tight on days 3–4 after rent, comfortable by day 5–6 via walk-ins; nobody went bankrupt.
- Fun 5–7/10. Best part: matching requests. Main complaint: once a cheap recipe covering common
  requests is found, nothing gets harder.

## Round 4: human-limited players

Rounds 1–3 let the AI read everything and remember the whole run, so it played far better than a
person with 10–15 minutes would. Round 4 added limits to the harness (no content or config changes
since round 3):

- **Patience:** a browsing budget per market visit (shelf/flip/recipes/pantry/news/more). After it is
  spent the player must buy from what they have seen.
- **Previews:** 1–3 dish previews per guest before the guest "is waiting".
- **Memory:** each day is played by a fresh agent. The only carry-over is a note of 40–80 words that the
  player writes at the end of the day (`remember`), shown at the next market.
- **Skimming:** skimmer personas see the first 2 sentences of tutorials and 1 of dialogue (`more`
  reveals the rest, at a browsing cost).

Per-persona limits (browse/previews/memory words/skim): Maya 4/1/40/yes, Daniel 8/2/60/no,
Ruth 10/2/80/no, Kai 6/3/50/yes, Lena 6/2/60/no. Ruth and Lena were redescribed for round 4 (Ruth 67,
slow careful reader; Lena a cheese-and-butter foodie), so their rows are not strictly comparable.

Results: four Blue endings at 57–77, one Struggling at 1.69. Fun 4–7/10. No bankruptcy.

- **Ruth's spiral came from mistakes, not healthy food.** Every dish she cooked was green. On day 3
  she served two incomplete dishes (missing a slot item): delight −30 and popularity −40 each, which
  dropped her from 384 to 304. She stayed below the threshold for a third guest and for walk-ins,
  bought cheese that turned out Red, half-met cravings (−15 each) and on day 6 had $0.31 after rent:
  nothing in any store costs less than $0.80, so she could not cook. This fits the design goal
  ("struggling players fall behind via requests and popularity"), but recovery for a careful beginner
  is close to impossible once popularity drops on day 3.
- **Forgetting showed up.** Lena repeated the same banana → Yellow mistake on two days (12 sorts, 3
  wrong). Others were 100%: memory notes carried "beans, oats, walnuts, bottom shelf of CheapCart".
- **Money now ends at about 55–77 for a competent player**, versus 100–135 in round 1. Owen's own
  playthroughs ended well under 100, which matches.
- **Everyone converged on cheap green staples again.** Lena (the foodie) wanted cheese/butter/bread and
  found the food "austere"; nobody was tempted into red foods by the game itself.

Recommendation: no CSV/config change from this round. The problems found are UI/design (below), not
numbers. Ruth's run is the case to watch if a softer recovery is wanted (e.g. a warning before
serving an incomplete dish, or a smaller mismatch penalty — a design decision).

## Open design questions (not fixable by tuning)

1. **No temptation, so trap lessons are rarely seen.** Cheap green staples are cheapest, tasty enough,
   and match requests, so players almost never buy yellow/red foods and sort mostly greens. Raising
   taste weight was tested with bots: it inflates everyone's popularity instead of creating temptation.
   Real temptation would need design: e.g. a recipe or request that junk fits naturally, a starter
   pantry/free samples containing trap items to sort, or limited staple stock.
2. **Eggs (and nuggets) have no usable slot.** No recipe accepts Egg or MeatSubstitute; Lena bought eggs
   and they spoiled. Decide: add optional slots, remove the listings, or keep as sorting-only items.
3. **One recipe wins.** No variety pressure beyond requests (Kai). Possible: request rotation, mild
   repetition penalty, or tighter package sizes.
4. **Incomplete-dish penalty vs beginners.** Serving a dish with an empty or wrong slot costs −30
   delight / −40 popularity. Two such mistakes on day 3 effectively decided Ruth's run (round 4).
5. **Broke with no way to cook.** Rent can leave less money than the cheapest listing ($0.80), so the
   last day becomes skip-or-serve-incomplete. Options: a free "staff meal" pantry item, a rent warning
   the day before, or letting checkout go negative (purchases already may if money > 0).
6. **"Moderation" wording.** In round 2 Ruth concluded "oil is for moderation" — directly from the
   sorting tutorial line. Owen/supervisor decision.

## UI notes for the real game

- Show each card's recipe category (Grain, Fat, …) on the card front; several players guessed.
- Show who is being served and their request on the meal-building screen header.
- Make the walk-in income line prominent in daily results (Kai never noticed it).
- Mention rent and requests earlier (players asked for a heads-up before day 3 and day 4).
- Consider showing which recipes answer each guest's request. (Round 4: asked for by 3 of 5.)
- Show pantry items by name and with their slot category; show "use within N days" prominently and
  warn about food that spoils before tomorrow (Ruth lost food to spoilage in round 4).
- Warn before serving a dish that does not match the recipe.
- Show the remaining money after tomorrow's rent at checkout.

## Limits

In rounds 1–3 AI players read every word and used previews more than people do, so clarity results
were optimistic. Round 4's limits narrow the gap but are crude (fixed budgets, written notes instead of
fuzzy recall).
They cannot judge visuals, layout or feel. Human playtests remain the final check.

## Harness changes after round 4 (2026-10-24)

The playtest tool now matches the game changes made after round 4: `preview` shows only recipe match,
warnings and requests answered (`forecast=true` restores the old full forecast for debugging), `sell
<FOOD>` sells pantry stock at the market, rent is announced the day before it starts, a selling lesson
appears when money first goes negative, and cards show appeal and category. Rounds 1–4 used the full
forecast, which is part of why the AI players did so well: they could compare outcomes before serving.

## Round 5: no outcome forecast, appeal visible (2026-10-24)

Same personas, seeds and human limits as round 4, one agent per day. New since round 4: Review dish shows only
plate match and requests answered (no health/delight/pay forecast), cards show appeal and category, Recipes
panel, pantry selling, farmer's food aid, rent letter the day before, 9 luxury foods. No CSV/config tuning.

| Persona | Round 4 | Round 5 | Notes |
| --- | --- | --- | --- |
| Maya | Blue, 73 | **Popular**, 58 | Sorted olive oil Green, then cooked 13 yellow servings with it → wellbeing 68 |
| Daniel | Blue, 71 | **Niche**, 72 | All green, popularity 738 (just under 750) |
| Ruth | Struggling, 2 | **Struggling**, 43 | No mistakes this time, but 2 guests on days 3–4 and one skip → popularity 686, wellbeing 66.5 |
| Kai | Blue, 77 | Blue, 77 | Didn't notice walk-in income ("$19 jump unexplained") |
| Lena | Blue, 57 | Blue, 61 | Cheese → Yellow three times (it is Red); still cooked mostly green |

Endings now spread across four outcomes and follow how people played: the olive-oil player got "full house,
unfinished promise", the slow careful player struggled, informed players flourished. Money 43–77; nobody went
negative, sold food or needed the farmer (the bots show those nets matter for careless players). Fun 6–7/10.

What players said:
- **Everyone missed the outcome forecast** (5/5). Daniel and Kai asked only for "will this satisfy X of Y
  requests", which Review dish already showed but weakly; it now reads "Requests answered: 1 of 2 (Creamy) ·
  not answered: Hearty". Maya, Lena and Ruth wanted a taste/delight hint — Owen's call; hiding it is what
  makes the AI play like people now.
- Rent letter landed: several players planned around "$9 from tomorrow".
- Patience limits bit: Maya never found beans (bargain bin/bottom shelf blocked once impatient).
- Ruth's "a chance to begin again" with $43 and all-green cooking felt unearned to her.

Open questions from round 5:
1. **Ending gap.** Popularity < 750 and wellbeing < 70 falls to the Struggling fallback even for a solvent,
   mostly green player (Ruth). Consider a gentler middle ending, or Niche at wellbeing ≥ 65 (Endings.csv only).
2. **Delight hint.** Optional: a qualitative taste word on Review dish (e.g. "plain / tasty") from mean appeal,
   which players can already estimate from the cards. Not added.
3. **Walk-in income visibility** for skimmers (Kai, two rounds).

Bot matrix for the same rules: `Tools/BalanceSim/README.md` (Round-5 matrix). Round 5 cost ≈ 2M tokens.

## Rounds 6–7: tuning for tighter money (2026-10-24)

Owen's target: a competent player should not end far above $50; planning and spoilage should matter. Loop:
bots search → AI round → report → change → AI round.

**Bot grid** (`Tools/BalanceSim/grid_rent_walkin.py`, 40 runs per cell, rent 9–14 × walk-in 0.015–0.05): chose rent
$11 and walk-in 0.035 (balanced bot $36, frugal $29, health $24, taste-chaser $6.5, careless $3.5 with none bankrupt).
Rent 12+ pushed careless bots into bankruptcy; rent 14 hurt even good play.

| Persona | Round 5 (rent 9, walk 0.05) | Round 6 (rent 11, walk 0.035) | Round 7 (+ mismatch penalty 20, earlier sell lesson) |
| --- | --- | --- | --- |
| Daniel | Niche, 72 | Blue, 51 | Blue, 54 |
| Kai | Blue, 77 | Niche, 37 | Blue, 48 |
| Lena | Blue, 61 | **Struggling, −0.4** | Blue, 49 |
| Ruth | Struggling, 43 | Niche, 29 | Blue, 35 |
| Maya | Popular, 58 | Niche, 39 | **Struggling, 10** |

Round 6 findings: planning now mattered (Daniel, Kai, Ruth planned each shop); spoiled servings 5–20 per player.
Lena spiralled: two plate mismatches (sweet potato ≠ Vegetable; missing Fat) at −40 popularity each kept her below
450 (no walk-ins), 20 servings spoiled unsold, and the selling lesson only appeared once money was already negative
and the pantry empty. Fun 4/10. Also fixed: the harness's Review listed requests as answered on a mismatched plate.

Changes after round 6:
1. `failedRecipePopularityPenalty` 40 → 20 (bots: removes careless-player bankruptcy, no change for good play).
2. `T_SELL` triggers on `tutorial.money_low` (money below one day's rent after rent starts).
3. Review dish: "Requests answered: none until the plate matches the recipe" on a mismatched plate (game and harness).

Round 7: four Blue at $35–54 and one Struggling at $10 (Maya: impulsive buys, olive oil, one mismatch, two farmer days,
three skipped guests). Fun 5–7. Spoiled servings 8–11 each; money tight on days 3–4 and comfortable by day 6 for
competent players. The farmer saved Maya's day 5–6 and Kai once. Nobody sold food (values feel small: "$0.25 for rice").

Remaining asks (UI, not balance): show which recipes answer which requests while shopping (Recipes panel already does;
playtesters rarely opened it under the browse limit), price per serving on shelf cards, a warning for food that spoils
tonight (several were surprised pantry items "vanished"), clearer colour explanation before the first sort.

Decision: stop tuning here. Rounds 6–7 cost ≈ 4M tokens.

## Round 8 (2026-10-25) — Comfort, recipe fatigue, crowd popularity, Daily Dozen novelty, R012–R015

Rules under test: Comfort craving (any plate with average appeal 7+), global recipe fatigue (4+ serves in 2 days halves popularity gain), crowd popularity (15 per appeal point above 7), Daily Dozen health only for groups the guest has not eaten (2 per group), four new junk-friendly recipes, guest info and plate appeal visible, outcome forecast hidden.

| Persona | Ending | Money | Popularity | Wellbeing | Notes |
|---|---|---|---|---|---|
| Daniel | Niche | 46.13 | 724 | 73.5 | Never bought junk; rotated recipes when the fatigue note appeared |
| Kai | Niche | 38.42 | 624 | 68.5 | Skimmed past the Comfort rule |
| Lena | Popular | 35.43 | 793 | 59.8 | 9 red servings; tempted by cheese; mis-sorted cheese 3 times |
| Maya | Popular | 32.22 | 775 | 68.8 | One farmer day |
| Ruth | Struggling | 34.25 | 674 | 63.5 | Did not understand Comfort; fatigue cut a +36 bonus to +2 when she had no other recipe |

Findings
- Money is in range: every tester ended between $32 and $46. No one sold pantry food; spoilage was 5–18 servings each.
- Nobody reached Blue. With the novelty rule, health gains shrink after day 2, so wellbeing tops out around 69–74.
- Comfort appeared on plates but players rarely knew they had answered it.
- Junk is now tempting (Lena), and fatigue does change behaviour (Daniel).
- Fun 6–7/10.
- Players asked for:
  - the ending criteria or a running health score before the end
  - the Comfort rule shown up front
  - a "what can I cook with what I have" list
  - price per serving
  - a "spoils tonight" marker
- They also noticed staples on the bottom shelf, which is intended.

Changes after round 8
- Ending thresholds: Blue and Popular split at wellbeing 65 (was 70); Niche from 60 (was 65). Under the new thresholds Maya would be Blue and Ruth Niche.
- The guest request line now spells out Comfort: "Comfort = any plate with average appeal 7+".

Bot check on the new thresholds (60 runs each)

| Bot | Main ending | Money | Wellbeing |
|---|---|---|---|
| Balanced | Blue 56/60 | $37 | 75 |
| Health | Niche 60/60 | $22 | 75 |
| Frugal | Niche | $27 | 75 |
| Taste | Split Popular/Niche | $34 | 62 |
| Junk | Popular 53/60 (most money) | $46 | 15 |
| Luxury | Niche | $25 | 67 |
| Careless | Niche | $8 | 68 (40% end negative) |
