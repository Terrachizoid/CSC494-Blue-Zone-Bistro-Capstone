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
    public Sprite cityBackground;
    public Sprite serviceBackground;
    public Sprite cardFrame;
    public Sprite slotFrame;
    public Font font;
    [Tooltip("Extra classification labels only appear in editor/development builds.")]
    public bool debugCardDetails;
}

