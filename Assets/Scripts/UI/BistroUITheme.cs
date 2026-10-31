// Shared visual settings. Assign optional artwork without changing gameplay rules.
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
    [Header("Recipe reading overlays")]
    [Range(0,1)] public float recipeFrontVeilOpacity=.50f;
}

