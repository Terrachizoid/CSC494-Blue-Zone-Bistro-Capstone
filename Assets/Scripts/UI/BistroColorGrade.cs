// File responsibility: Colour grades for 2D artwork. Each layer blends three looks by a 0-1 value (scenery: popularity
// for clarity and community wellbeing for the colour cast; a guest: their displayed health). Each look is a BistroGradeProfile set in the UI Theme:
// numbers plus tone curves drawn like a paint app's Curves panel. The curves are baked into a 256x1 ramp texture only
// when the value changes, so it costs nothing per frame. No gameplay values are read or changed here.
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BistroGradeProfile
{
    [Range(0, 2)] public float saturation = 1;
    [Range(.5f, 1.5f)] public float contrast = 1;
    [Range(.5f, 1.5f)] public float brightness = 1;
    [Tooltip("Colour tint that keeps brightness (white = none).")] public Color tint = Color.white;
    [Range(0, 1)] public float tintAmount;
    [Tooltip("Haze: blends toward the fog colour.")] [Range(0, 1)] public float fog;
    public Color fogColor = new Color(.80f, .80f, .80f);
    [Tooltip("Tone curve for all channels, like Curves in a paint app: input brightness (x, 0-1) to output (y, 0-1).")]
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("Per-channel curves, applied after the main curve.")]
    public AnimationCurve red = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve green = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve blue = AnimationCurve.Linear(0, 0, 1, 1);

    // Curves applied to one colour, channel by channel (main curve, then that channel's curve).
    internal Color Tone(Color c) =>
        new Color(Eval(red, Eval(curve, c.r)), Eval(green, Eval(curve, c.g)), Eval(blue, Eval(curve, c.b)), 1);

    private static float Eval(AnimationCurve c, float x) =>
        c == null || c.length == 0 ? x : Mathf.Clamp01(c.Evaluate(x));
}

// One axis of a grade: three looks (low at value 0, mid at `midpoint`, high at 1) and where the game is on that axis.
// At the midpoint the look is the mid look; keep mid looks neutral so the art shows exactly as drawn there.
public struct BistroGradeLayer
{
    public BistroGradeProfile low, mid, high;
    public float midpoint, value;
    public BistroGradeLayer(BistroGradeProfile low, BistroGradeProfile mid, BistroGradeProfile high, float midpoint, float value)
    { this.low = low; this.mid = mid; this.high = high; this.midpoint = midpoint; this.value = value; }
}

// One colour swap of a mood: colours near `from` move toward `to`; `reach` is how far around `from` it spreads
// (0.1 = only very close colours, 0.4 = broad). Colours far from every swap stay as they are.
[Serializable]
public class BistroColorShift
{
    public Color from = Color.grey;
    public Color to = Color.grey;
    [Range(.05f, .6f)] public float reach = .2f;
    public BistroColorShift() { }
    public BistroColorShift(float fr, float fg, float fb, float tr, float tg, float tb, float reach)
    { from = new Color(fr, fg, fb); to = new Color(tr, tg, tb); this.reach = reach; }
}

// A scenery mood (one ending corner): a few colour swaps, then saturation, contrast and brightness. It is turned
// into a colour lookup table (LUT): a 256x16 strip holding, for 16x16x16 sample colours, what each one becomes.
// `painted` (optional) replaces all of it with a hand-graded 256x16 strip (texture Read/Write enabled).
[Serializable]
public class BistroLutMood
{
    [Tooltip("Optional hand-painted 256x16 LUT strip (Read/Write enabled). Overrides everything below.")]
    public Texture2D painted;
    public List<BistroColorShift> shifts = new List<BistroColorShift>();
    [Range(0, 2)] public float saturation = 1;
    [Range(.5f, 1.5f)] public float contrast = 1;
    [Range(.5f, 1.5f)] public float brightness = 1;

    public Color Map(Color c)
    {
        float r = c.r, g = c.g, b = c.b, dr = 0, dg = 0, db = 0;
        if (shifts != null)
            foreach (var s in shifts)
            {
                if (s == null) continue;
                float ex = r - s.from.r, ey = g - s.from.g, ez = b - s.from.b, reach = Mathf.Max(.01f, s.reach);
                float w = Mathf.Exp(-(ex * ex + ey * ey + ez * ez) / (reach * reach));
                dr += w * (s.to.r - s.from.r); dg += w * (s.to.g - s.from.g); db += w * (s.to.b - s.from.b);
            }
        r += dr; g += dg; b += db;
        float l = .299f * r + .587f * g + .114f * b;
        r = l + (r - l) * saturation; g = l + (g - l) * saturation; b = l + (b - l) * saturation;
        r = ((r - .5f) * contrast + .5f) * brightness; g = ((g - .5f) * contrast + .5f) * brightness; b = ((b - .5f) * contrast + .5f) * brightness;
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1);
    }
}

// LUT strips: pixel (x = blue step * 16 + red step, y = green step), 16 steps per channel, gamma-space values.
public static class BistroLut
{
    public const int Steps = 16, Width = Steps * Steps, Count = Width * Steps;

    public static Color GridColor(int i)
    {
        int x = i % Width, y = i / Width;
        return new Color((x % Steps) / (Steps - 1f), y / (Steps - 1f), (x / Steps) / (Steps - 1f), 1);
    }

    public static Color[] Build(BistroLutMood mood)
    {
        var pixels = new Color[Count];
        if (mood != null && mood.painted != null && mood.painted.width == Width && mood.painted.height == Steps && mood.painted.isReadable)
        {
            var painted = mood.painted.GetPixels();
            if (painted.Length == Count) return painted;
        }
        for (int i = 0; i < Count; i++) pixels[i] = mood != null ? mood.Map(GridColor(i)) : GridColor(i);
        return pixels;
    }

    // Blends the four ending moods on a square: u = popularity, v = community wellbeing, both -1..1 with 0 at the start
    // values. The centre is the original art; an edge midpoint is the average of its two corners; between them the
    // mix is bilinear. Corners: Blue (+u +v), Popular (+u -v), Niche (-u +v), Struggling (-u -v). No order: all at once.
    public static void Blend(Color[] into, Color[] blue, Color[] popular, Color[] niche, Color[] struggling, float u, float v)
    {
        u = Mathf.Clamp(u, -1, 1); v = Mathf.Clamp(v, -1, 1);
        float a = Mathf.Abs(u), b = Mathf.Abs(v);
        Color[] cu1 = u >= 0 ? blue : niche, cu2 = u >= 0 ? popular : struggling;   // this popularity side
        Color[] cv1 = v >= 0 ? blue : popular, cv2 = v >= 0 ? niche : struggling;   // this wellbeing side
        Color[] corner = u >= 0 ? (v >= 0 ? blue : popular) : (v >= 0 ? niche : struggling);
        float w0 = (1 - a) * (1 - b), wu = a * (1 - b), wv = (1 - a) * b, wc = a * b;
        for (int i = 0; i < Count; i++)
        {
            Color c = GridColor(i) * w0 + (cu1[i] + cu2[i]) * (.5f * wu) + (cv1[i] + cv2[i]) * (.5f * wv) + corner[i] * wc;
            c.a = 1;
            into[i] = c;
        }
    }
}

public sealed class BistroColorGrade
{
    private const int RampSize = 256;
    private static Shader shader;
    private readonly Texture2D ramp;
    private readonly Texture2D lut;
    private readonly Color[] pixels = new Color[RampSize];
    private float applied = float.NaN;

    public Material Material { get; }

    public static BistroColorGrade Create(string name)
    {
        if (shader == null) shader = Resources.Load<Shader>("Shaders/BistroGrade");
        return shader == null || !shader.isSupported ? null : new BistroColorGrade(name);
    }

    private BistroColorGrade(string name)
    {
        Material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
        ramp = new Texture2D(RampSize, 1, TextureFormat.RGBA32, false, true)
            { name = name + " curves", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        Material.SetTexture("_ToneRamp", ramp);
        Material.SetFloat("_ToneAmount", 1);
        lut = new Texture2D(BistroLut.Width, BistroLut.Steps, TextureFormat.RGBA32, false, true)
            { name = name + " LUT", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var identity = new Color[BistroLut.Count];
        for (int i = 0; i < identity.Length; i++) identity[i] = BistroLut.GridColor(i);
        lut.SetPixels(identity); lut.Apply(false);
        Material.SetTexture("_Lut", lut);
        Material.SetFloat("_LutAmount", 0);
    }

    // Several layers stack (e.g. scenery: popularity sets clarity, wellbeing sets the colour cast): saturation,
    // contrast and brightness multiply, curves apply one after another, tints and haze combine by their amounts.
    // force: re-apply even if the values did not change (the editor does this so Inspector edits show live).
    public void Apply(bool force, params BistroGradeLayer[] layers)
    {
        float key = 0;
        foreach (var layer in layers) key = key * 1.618f + Mathf.Clamp01(layer.value) + layer.midpoint * .001f;
        if (!force && Mathf.Abs(key - applied) < .0005f) return;
        applied = key;
        float saturation = 1, contrast = 1, brightness = 1, keepTint = 1, keepFog = 1, tintWeight = 0, fogWeight = 0;
        Color tint = Color.clear, fogColor = Color.clear;
        var blends = new (BistroGradeProfile a, BistroGradeProfile b, float t)[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            float value = Mathf.Clamp01(layer.value), midpoint = Mathf.Clamp(layer.midpoint, .01f, .99f);
            BistroGradeProfile a, b; float t;
            if (value <= midpoint) { a = layer.low; b = layer.mid; t = value / midpoint; }
            else { a = layer.mid; b = layer.high; t = (value - midpoint) / (1 - midpoint); }
            a = a ?? new BistroGradeProfile(); b = b ?? new BistroGradeProfile();
            blends[i] = (a, b, t);
            saturation *= Mathf.Lerp(a.saturation, b.saturation, t);
            contrast *= Mathf.Lerp(a.contrast, b.contrast, t);
            brightness *= Mathf.Lerp(a.brightness, b.brightness, t);
            float tintAmount = Mathf.Lerp(a.tintAmount, b.tintAmount, t);
            if (tintAmount > 0)
            {
                // A tint fades in with its amount, so blending toward "no tint" never drifts through odd colours.
                var colour = Color.Lerp(a.tintAmount > 0 ? a.tint : b.tint, b.tintAmount > 0 ? b.tint : a.tint, t);
                tint += colour * tintAmount; tintWeight += tintAmount; keepTint *= 1 - tintAmount;
            }
            float fog = Mathf.Lerp(a.fog, b.fog, t);
            if (fog > 0)
            {
                fogColor += Color.Lerp(a.fogColor, b.fogColor, t) * fog; fogWeight += fog; keepFog *= 1 - fog;
            }
        }
        Material.SetFloat("_Saturation", saturation);
        Material.SetFloat("_Contrast", contrast);
        Material.SetFloat("_Brightness", brightness);
        Material.SetColor("_GradeTint", tintWeight > 0 ? tint / tintWeight : Color.white);
        Material.SetFloat("_GradeTintAmount", 1 - keepTint);
        Material.SetColor("_FogColor", fogWeight > 0 ? fogColor / fogWeight : Color.grey);
        Material.SetFloat("_Fog", 1 - keepFog);
        for (int i = 0; i < RampSize; i++)
        {
            float x = i / (RampSize - 1f);
            var c = new Color(x, x, x, 1);
            foreach (var blend in blends) c = Color.Lerp(blend.a.Tone(c), blend.b.Tone(c), blend.t);
            pixels[i] = c;
        }
        ramp.SetPixels(pixels);
        ramp.Apply(false);
    }

    // Scenery moods: the blended LUT strip (BistroLut layout) and how strongly it applies (0 = off, 1 = full).
    public void SetLut(Color[] pixels, float amount)
    {
        if (pixels != null && pixels.Length == BistroLut.Count) { lut.SetPixels(pixels); lut.Apply(false); }
        Material.SetFloat("_LutAmount", Mathf.Clamp01(amount));
    }

    public void Release()
    {
        if (lut != null) UnityEngine.Object.Destroy(lut);
        if (Material != null) UnityEngine.Object.Destroy(Material);
        if (ramp != null) UnityEngine.Object.Destroy(ramp);
    }
}
