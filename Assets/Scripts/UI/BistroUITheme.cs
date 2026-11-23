// Shared visual settings. Assign optional artwork without changing gameplay rules.
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="Blue Zone Bistro/UI Theme")]
public class BistroUITheme : ScriptableObject
{
    public Color background = new Color(.17f,.21f,.21f);
    public Color panel = new Color(.25f,.30f,.29f);
    public Color card = new Color(.88f,.86f,.79f);
    public Color ink = new Color(.16f,.20f,.19f);
    public Color accent = new Color(.60f,.70f,.61f);
    public Color warning = new Color(.78f,.43f,.38f);
    // Screen-wide art (not per food/recipe/store, which lives in the CSVs). Drag a sprite in to
    // replace one. Empty slots are filled from Assets/Art/UI/<slot name>.png by the editor setup.
    [Header("Screen art")]
    public Sprite cityBackground;
    [Range(1.1f,2f)] public float cityMapZoom=1.4f;
    public Sprite serviceBackground;
    [Tooltip("Counter strip drawn over the bottom of the service room.")]
    public Sprite serviceCounter;
    [Tooltip("Light and dark name plaques on illustrated food cards (FoodData.darkPlaque picks one).")]
    public Sprite plaqueLight;
    public Sprite plaqueDark;
    [Tooltip("Wooden rail under each row of shelf cards.")]
    public Sprite shelfRail;
    [Tooltip("Leaf sprig beside the plaque on cards whose card_style includes sprig.")]
    public Sprite cardSprig;
    [Tooltip("Icon at the top of the shelf cart panel.")]
    public Sprite cartIcon;
    public Sprite cardFrame;
    public Sprite slotFrame;
    [Header("Reusable illustrated surfaces")]
    public Sprite panelPaper;
    public Sprite guideNotePanel;
    public Sprite planPanel;
    public Sprite suggestionFrame;
    public Sprite soldOutStamp;
    public Sprite newTag;
    public Font font;
    [Header("Contextual action buttons")]
    public Color actionPrimary=new Color(.67f,.77f,.58f);
    public Color actionSecondary=new Color(.91f,.89f,.81f);
    public Color actionCaution=new Color(.84f,.68f,.57f);
    public Color actionInk=new Color(.10f,.18f,.15f);
    [Header("Procedural motion")]
    [Tooltip("Instant flips and panels; disables hover scaling. Gameplay is unchanged.")]
    public bool reducedMotion;
    [Range(.1f,.5f)] public float cardFlipSeconds=.26f;
    [Range(.08f,.4f)] public float panelRevealSeconds=.18f;
    // Cross-fade between the old and new screen when the place or an overlay changes (opening and closing panels).
    [Range(.05f,.4f)] public float screenFadeSeconds=.16f;
    [Tooltip("Show each food's ingredient appeal (0-10) as a badge on the card front. Placeholder for delight icons.")]
    public bool showCardAppeal = true;
    [Tooltip("Extra classification labels only appear in editor/development builds.")]
    public bool debugCardDetails;
    [Header("Colour grade (2026-11-22)")]
    [Tooltip("Grades the scenery (city, store interiors, bistro room) by community wellbeing and each guest by their displayed health. Cards, text and panels are never graded. Off = artwork exactly as drawn.")]
    public bool colorGrade = true;
    [Tooltip("Seconds the colours take to settle after a change.")] [Range(0, 4)] public float gradeSeconds = 1.5f;
    // Scenery moods (2026-11-22): the city, stores and bistro room drift toward the look of the ending the player is
    // heading for. Popularity and community wellbeing place the game on a square; at the starting values (centre)
    // the art is exactly as drawn, at a corner it is that mood, in between the nearby moods mix (all at once, no order).
    // Each mood = colour swaps (pick colours with the picker) + saturation/contrast/brightness, or a painted LUT strip.
    // In Play mode in the editor, edits show live (pair with the F2 sliders). Previews: Data/Previews/ColorMoods.
    [Tooltip("Popular and healthy (Blue ending): clear sky-blue water, fresh greens, clean whites.")]
    public BistroLutMood moodBlue = new BistroLutMood { saturation = 1.12f, contrast = 1.06f, brightness = 1.02f,
        shifts = new List<BistroColorShift> {
            new BistroColorShift(0.41f, 0.51f, 0.58f, 0.33f, 0.54f, 0.64f, 0.22f),
            new BistroColorShift(0.49f, 0.5f, 0.46f, 0.38f, 0.56f, 0.36f, 0.14f),
            new BistroColorShift(0.38f, 0.39f, 0.36f, 0.27f, 0.42f, 0.28f, 0.12f),
            new BistroColorShift(0.8f, 0.78f, 0.76f, 0.86f, 0.84f, 0.78f, 0.2f) } };
    [Tooltip("Popular but unhealthy (Popular ending): golden, commercial late-afternoon warmth; olive greens, murky water.")]
    public BistroLutMood moodPopular = new BistroLutMood { saturation = 1.1f, contrast = 1.08f, brightness = 1.0f,
        shifts = new List<BistroColorShift> {
            new BistroColorShift(0.8f, 0.78f, 0.76f, 0.93f, 0.8f, 0.64f, 0.25f),
            new BistroColorShift(0.41f, 0.51f, 0.58f, 0.42f, 0.52f, 0.52f, 0.2f),
            new BistroColorShift(0.49f, 0.5f, 0.46f, 0.55f, 0.53f, 0.38f, 0.15f),
            new BistroColorShift(0.3f, 0.31f, 0.3f, 0.32f, 0.25f, 0.24f, 0.18f) } };
    [Tooltip("Healthy but quiet (Niche ending): soft, airy, gentle greens and blues.")]
    public BistroLutMood moodNiche = new BistroLutMood { saturation = 0.95f, contrast = 0.92f, brightness = 1.04f,
        shifts = new List<BistroColorShift> {
            new BistroColorShift(0.49f, 0.5f, 0.46f, 0.45f, 0.56f, 0.45f, 0.15f),
            new BistroColorShift(0.41f, 0.51f, 0.58f, 0.44f, 0.57f, 0.63f, 0.2f),
            new BistroColorShift(0.8f, 0.78f, 0.76f, 0.86f, 0.85f, 0.8f, 0.2f) } };
    [Tooltip("Neither (Struggling ending): dull, dusty and dim, a faint orange haze.")]
    public BistroLutMood moodStruggling = new BistroLutMood { saturation = 0.75f, contrast = 0.88f, brightness = 0.94f,
        shifts = new List<BistroColorShift> {
            new BistroColorShift(0.8f, 0.78f, 0.76f, 0.76f, 0.68f, 0.6f, 0.25f),
            new BistroColorShift(0.41f, 0.51f, 0.58f, 0.46f, 0.48f, 0.47f, 0.2f),
            new BistroColorShift(0.49f, 0.5f, 0.46f, 0.51f, 0.47f, 0.4f, 0.15f) } };
    [Tooltip("Where each popularity value sits between the moods: -1 = fully the unpopular moods, 0 = original art, 1 = fully the popular moods. At popularity 0 / starting / maximum.")]
    [Range(-1, 1)] public float popularityMoodAtZero = -1f, popularityMoodAtStart = 0f, popularityMoodAtMax = 1f;
    [Tooltip("Where each wellbeing value sits between the moods: -1 = fully the unhealthy moods, 0 = original art, 1 = fully the healthy moods. At wellbeing 0 / starting / maximum. Defaults (Owen, 2026-11-22): the start already leans a little unhealthy and full wellbeing stops at a gentle healthy look.")]
    [Range(-1, 1)] public float wellbeingMoodAtZero = -1f, wellbeingMoodAtStart = -.3f, wellbeingMoodAtMax = .2f;
    [Tooltip("How strongly the scenery mood also tints paper panels and rails (0 = never, 1 = like the scenery). Text, cards and colour-coded buttons are never tinted.")]
    [Range(0, 1)] public float panelMoodStrength = .3f;
    [Tooltip("Exaggerates how far a guest's health moves their look from the original (1 = as the looks say).")]
    [Range(1, 3)] public float guestGradeStrength = 1.6f;
    [Header("Music intensity (2026-11-22)")]
    [Tooltip("Below this popularity the music switches to its desolate (.low) version.")]
    public float musicLowBelow = 200;
    [Tooltip("From this popularity the music switches to its full (.high) version. The start is 300.")]
    public float musicHighFrom = 500;
    [Tooltip("Seconds a guest is on screen (arrival, dish result) before their colours start changing, so the change is seen.")]
    [Range(0, 2)] public float guestGradeDelay = .6f;
    [Tooltip("A guest at displayed health 0 (health plus the rush/crash offset): greyed out. Never tinted green.")]
    public BistroGradeProfile guestLow = new BistroGradeProfile { saturation = .35f, contrast = .94f, brightness = .94f };
    [Tooltip("A guest at displayed health 50 (where every guest starts): the original art.")]
    public BistroGradeProfile guestStart = new BistroGradeProfile();
    [Tooltip("A guest at displayed health 100: vibrant, a little more contrast.")]
    public BistroGradeProfile guestHigh = new BistroGradeProfile { saturation = 1.18f, contrast = 1.07f, brightness = 1.03f };
    [Header("Recipe reading overlays")]
    [Range(0,1)] public float recipeFrontVeilOpacity=.50f;
}

