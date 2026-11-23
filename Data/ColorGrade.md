# Colour grade (2D saturation filter)

Added 2026-11-22. Replaces the parked 3D saturation idea with a 2D one: a colour grade on UI artwork.

## What is graded

| Artwork | Driven by | Look |
|---|---|---|
| Scenery: city map, store interiors, bistro room and counter | Popularity and community wellbeing together | drifts toward the mood of the ending the player is heading for |
| Paper panels and rails (`panelPaper`, `guideNotePanel`, `planPanel`, `suggestionFrame`, `shelfRail`) | same mood | at `panelMoodStrength` (default 30%) so the UI sits in the same light |
| Each guest's portrait (art or silhouette) | Displayed health: `health + visualScore` (rush +20, crash -20; `BalanceConfig.rushVisualBoost` / `crashVisualDrop`) | health 50 = original, lower greyer, higher more vibrant; never green |

Never graded: text, food and recipe card art, colour-coded buttons, rims and sorting bins (their colours carry
meaning), the ending screen, the main menu.

### Scenery moods (colour lookup tables)

A mood is a colour lookup table (LUT): for 16x16x16 sample colours, what each one becomes, stored as a 256x16 strip
(16 squares side by side; in a square x = red, y = green; square = blue). The shader reads each pixel's colour, finds
it in the strip and draws the stored colour (blending between neighbouring samples). Unlike a filter, a LUT can treat
colour ranges differently (greens to olive while water stays blue). It changes colours, not places.

Four moods sit on the corners of a square; the game sits somewhere on it and mixes the nearby moods at once
(no order). At the starting popularity and wellbeing (centre) the art is exactly as drawn.

|  | Wellbeing low | start | high |
|---|---|---|---|
| **Popularity high** | `moodPopular`: golden, commercial warmth | Popular/Blue mix | `moodBlue`: clear water, fresh greens |
| **start** | Popular/Struggling mix | **original art** | Blue/Niche mix |
| **low** | `moodStruggling`: dull, dusty, dim | Struggling/Niche mix | `moodNiche`: soft, airy |

Where values sit on the square is set per axis in the theme (`popularityMoodAtZero/AtStart/AtMax`,
`wellbeingMoodAtZero/AtStart/AtMax`, each -1..1). Defaults after the first in-game look (Owen, 2026-11-22):
popularity -1 / 0 / 1; wellbeing -1 / -0.3 / 0.2, so the start already leans slightly unhealthy and full
wellbeing stops at a gentle healthy look (the full healthy corner looked too blue). The panels pass also covers
the food-card plaques and the top bar.

Previews of the defaults: `Data/Previews/ColorMoods/city_moods.png`, `bistro_moods.png`
(re-render with `Tools/Art/ColorMoods.py` after editing its MOODS to match the theme).

Changes ease in over `gradeSeconds` (default 1.5 s). A guest's colours only move while they are on screen, starting
`guestGradeDelay` (0.6 s) after they appear: the rush brightens them on the dish result, and the crash shows as a drop
shortly after they arrive (a guest seen for the first time starts from their health without the offset). Reduced motion snaps instead.

## Tuning (no code)

`Assets/GameData/BistroUITheme.asset` > Colour grade:
- `colorGrade` on/off, `gradeSeconds`, `panelMoodStrength`, `guestGradeStrength` (pushes guest looks further from
  the original so health changes read clearly).
- Each mood: a list of colour swaps (`from` colour, `to` colour, `reach` = how far around `from` it spreads), then
  saturation, contrast and brightness. Pick colours with the colour picker (the eyedropper can sample the Game view).
  Or drop a hand-graded 256x16 strip into `painted` (Texture Read/Write on) to replace that mood entirely.
- Guests: `guestLow`, `guestStart` (keep neutral), `guestHigh` (saturation, contrast, brightness, curves).
- Tuning loop: enter Play mode, F2 > Colour grade, drag popularity and wellbeing to a corner, edit that mood in
  the Inspector; it updates live in the editor and the asset keeps the changes after Play mode.

## Checking it quickly

F2 debug panel (editor and development builds) > Colour grade: a community wellbeing slider (with 0 / Start /
Max), a popularity slider (with back to start), and per guest a health slider plus Rush (+20), Crash (-20, also queues their crash conversation) and Clear.
Values apply at once; the screen then eases into the new grade. Only the guest on stage is visible.

## Code

- Shader: `Assets/Resources/Shaders/BistroGrade.shader` (UI/Default plus LUT, curves and adjustments; in Resources so Web builds include it).
- `Scripts/UI/BistroColorGrade.cs`: `BistroLutMood` and `BistroLut` (build and blend LUT strips), `BistroGradeProfile`
  (guest looks) and the material; textures are rebuilt only when values change (every frame in the editor, for live tuning).
- `Scripts/UI/BistroUIScreen.Grade.cs`: scenery, panel and per-guest materials, kept across screen rebuilds, eased in
  `LateUpdate`. Scenery images call `GradeScenery(image)`; `GradePanels()` runs after each rebuild; portraits get `GuestGrade(id)`.
- If the shader is missing or unsupported, everything draws as before.
