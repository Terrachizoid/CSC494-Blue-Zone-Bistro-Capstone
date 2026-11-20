// File responsibility: On-screen FPS counter and graphics/debug panel (IMGUI, so it needs no scene setup and works
// in WebGL and on phones). Created automatically at startup. Tap/click the FPS readout, or press F3, to open the
// panel. Every change is saved and applied at once (BistroGraphics). Meant for testing and tuning; a polished
// settings screen in the game menu can use the same BistroGraphics API later.
using UnityEngine;

public sealed class BistroGraphicsPanel : MonoBehaviour
{
    private bool open;
    private float smoothedDelta = 1f / 60f, worstDelta;
    private float worstTimer;
    private Vector2 scroll;
    private GUIStyle box, label, small;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        BistroGraphics.Load();
        BistroGraphics.Apply();
        var go = new GameObject("Graphics settings (runtime)");
        DontDestroyOnLoad(go);
        go.AddComponent<BistroGraphicsPanel>();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        smoothedDelta = Mathf.Lerp(smoothedDelta, dt, 0.08f);
        worstTimer += dt;
        if (dt > worstDelta || worstTimer > 2f) { worstDelta = dt; if (worstTimer > 2f) worstTimer = 0; }
    }

    private void OnApplicationQuit() => BistroGraphics.RestoreAssets();
    private void OnDestroy() => BistroGraphics.RestoreAssets();

    private void Styles(float scale)
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
        label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1, 1, 1, .75f) } };
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Styles(scale);
        float w = Screen.width / scale, h = Screen.height / scale;

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F3) { open = !open; e.Use(); }

        if (BistroGraphics.ShowFps || open)
        {
            float fps = 1f / Mathf.Max(0.0001f, smoothedDelta);
            string text = $"{fps:0} fps  {smoothedDelta * 1000f:0.0} ms  (worst {worstDelta * 1000f:0} ms)";
            var r = new Rect(8, h - 30, 250, 24);
            GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
            if (GUI.Button(r, text, label)) open = !open;
        }
        if (!open) return;

        var area = new Rect(8, 8, 300, Mathf.Min(h - 50, 640));
        GUI.color = new Color(0, 0, 0, .78f); GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16));
        scroll = GUILayout.BeginScrollView(scroll);
        bool changed = false;

        GUILayout.Label("Graphics (3D city)   F3 to close", label);
        int preset = GUILayout.Toolbar((int)BistroGraphics.Preset, new[] { "Low", "Medium", "High", "Custom" });
        if (preset != (int)BistroGraphics.Preset && preset != (int)GraphicsPreset.Custom) { BistroGraphics.SetPreset((GraphicsPreset)preset); }

        GUILayout.Space(6);
        GUILayout.Label($"Render scale {BistroGraphics.RenderScale:0.00}", label);
        changed |= Slider(ref BistroGraphics.RenderScale, .5f, 1f);
        GUILayout.Label("MSAA", label);
        int msaaIndex = BistroGraphics.Msaa >= 4 ? 2 : BistroGraphics.Msaa >= 2 ? 1 : 0;
        int newMsaa = GUILayout.Toolbar(msaaIndex, new[] { "Off", "2x", "4x" });
        if (newMsaa != msaaIndex) { BistroGraphics.Msaa = newMsaa == 2 ? 4 : newMsaa == 1 ? 2 : 1; changed = true; }

        GUILayout.Space(6);
        changed |= Toggle(ref BistroGraphics.DetailLayer, "Extra detail (window frames, balconies, rocks, fences)");
        changed |= Toggle(ref BistroGraphics.Shadows, "Sun shadows");
        changed |= Toggle(ref BistroGraphics.SoftShadows, "Soft shadow edges (PCF)");
        GUILayout.Label($"Shadow map {BistroGraphics.ShadowResolution}", label);
        int resIndex = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(BistroGraphics.ShadowResolution / 512f, 2)), 0, 3);
        int newRes = GUILayout.Toolbar(resIndex, new[] { "512", "1024", "2048", "4096" });
        if (newRes != resIndex) { BistroGraphics.ShadowResolution = 512 << newRes; changed = true; }

        GUILayout.Space(6);
        changed |= Toggle(ref BistroGraphics.Ssao, "SSAO (needs the renderer feature)");
        changed |= Toggle(ref BistroGraphics.Ssr, "SSR on water");
        GUILayout.Label($"SSR steps {BistroGraphics.SsrSteps}", label);
        float steps = BistroGraphics.SsrSteps;
        if (Slider(ref steps, 4, 64)) { BistroGraphics.SsrSteps = Mathf.RoundToInt(steps); changed = true; }
        GUILayout.Label($"SSR max distance {BistroGraphics.SsrMaxDistance:0} m", label);
        changed |= Slider(ref BistroGraphics.SsrMaxDistance, 10, 250);

        GUILayout.Space(6);
        var city = BistroCity3D.Current;
        if (city != null)
        {
            // Live camera angle, to find the best view (copy the numbers into BistroCity3D's defaults afterwards).
            GUILayout.Label($"Camera  yaw {city.Yaw:0}  pitch {city.Pitch:0}  lens {city.FieldOfView:0}", label);
            Slider(ref city.Yaw, -180f, 180f);
            Slider(ref city.Pitch, 10f, 89f);
            Slider(ref city.FieldOfView, 10f, 60f);
            city.Orthographic = GUILayout.Toggle(city.Orthographic, " Orthographic");
            if (GUILayout.Button("Reset camera angle")) { city.Yaw = -28f; city.Pitch = 42f; city.FieldOfView = 30f; city.Orthographic = false; }
            GUILayout.Space(6);
        }
        bool lookChanged = false;
        lookChanged |= Toggle(ref BistroGraphics.Outlines, "Outlines");
        lookChanged |= Toggle(ref BistroGraphics.TiltShift, "Tilt-shift blur (miniature look)");
        lookChanged |= Toggle(ref BistroGraphics.Grading, "Colour grading");
        lookChanged |= Toggle(ref BistroGraphics.BakedGI, "Baked lighting (lightmaps)");
        lookChanged |= Toggle(ref BistroGraphics.Reflections, "Reflections (probe)");
        lookChanged |= Toggle(ref BistroGraphics.ShowFps, "Show FPS");
        GUILayout.Label("Debug view", label);
        int view = GUILayout.SelectionGrid(BistroGraphics.DebugView, BistroGraphics.DebugViews, 3);
        if (view != BistroGraphics.DebugView) { BistroGraphics.DebugView = view; lookChanged = true; }
        GUILayout.Label("Indirect = baked/ambient light only; Direct = sun only; SSR hits = white where SSR found something.", small);

        if (changed) BistroGraphics.Preset = GraphicsPreset.Custom;
        if (changed || lookChanged) { BistroGraphics.Save(); BistroGraphics.Apply(); }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private bool Toggle(ref bool value, string text)
    {
        bool next = GUILayout.Toggle(value, " " + text);
        if (next == value) return false;
        value = next; return true;
    }

    private bool Slider(ref float value, float min, float max)
    {
        float next = GUILayout.HorizontalSlider(value, min, max);
        if (Mathf.Approximately(next, value)) return false;
        value = next; return true;
    }
}
