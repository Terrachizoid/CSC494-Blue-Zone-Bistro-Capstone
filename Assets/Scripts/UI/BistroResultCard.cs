// Animates the dish result card (2026-11-23): the health and delight bars slide from before to after, the numbers
// count up and each bar glows briefly as it lands. The start time is the guest's colour-grade moment, so the card and
// the guest change together. Time-based: a UI rebuild in the middle continues from the same moment instead of restarting.
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BistroResultCard : MonoBehaviour
{
    public RectTransform healthFill, delightFill;
    public Image healthGlow, delightGlow;
    public Text healthValue, delightValue, popularityValue, paidValue;
    public Color healthColor, delightColor;
    public float healthFrom, healthTo, delightFrom, delightTo;   // bar positions, 0-1
    public int healthDelta, delightDelta;
    public float popularityDelta, paid;
    public float appearAt, startAt, duration = .8f;
    public bool instant;
    private CanvasGroup group;
    private static readonly Color Neutral = new Color(.72f, .72f, .68f);

    private void Awake() { group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>(); }
    private void Start() => Tick();
    private void Update() => Tick();

    private void Tick()
    {
        float now = Time.unscaledTime;
        if (group != null) group.alpha = instant ? 1 : Mathf.Clamp01((now - appearAt) / .25f);
        float u = instant ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01((now - startAt) / Mathf.Max(.05f, duration)));
        Fill(healthFill, Mathf.Lerp(healthFrom, healthTo, u), Color.Lerp(Neutral, healthColor, u));
        Fill(delightFill, Mathf.Lerp(delightFrom, delightTo, u), Color.Lerp(Neutral, delightColor, u));
        if (healthValue != null) healthValue.text = Signed(Mathf.RoundToInt(healthDelta * u));
        if (delightValue != null) delightValue.text = Signed(Mathf.RoundToInt(delightDelta * u));
        if (popularityValue != null) popularityValue.text = "Popularity " + Signed(Mathf.RoundToInt(popularityDelta * u));
        if (paidValue != null) paidValue.text = $"Paid ${paid * u:0.00}";
        // A short light sweep as the bars land: it peaks at the end of the slide and fades within a third of a second.
        float glow = instant ? 0 : Mathf.Clamp01(1 - Mathf.Abs(now - startAt - duration) / .35f);
        Glow(healthGlow, healthDelta != 0 ? glow : 0);
        Glow(delightGlow, delightDelta != 0 ? glow : 0);
    }

    private static void Fill(RectTransform fill, float x, Color color)
    {
        if (fill == null) return;
        fill.anchorMax = new Vector2(Mathf.Clamp(x, .02f, 1), fill.anchorMax.y);
        var image = fill.GetComponent<Image>(); if (image != null) image.color = color;
    }
    private static void Glow(Image image, float a) { if (image != null) image.color = new Color(1, 1, 1, .55f * a); }
    private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
}
