// File responsibility: Graphics settings (quality preset, render scale, MSAA, shadows, SSAO, SSR, baked GI,
// reflections, debug view, FPS counter). Saved per player (PlayerPrefs) and applied to the shaders (globals),
// the URP asset (shadows), the city's Sun and the 3D renderer's features (SSAO). Only the 3D city uses them; the
// 2D UI is unaffected. The on-screen panel is BistroGraphicsPanel; a menu screen can call the same API later.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum GraphicsPreset { Low, Medium, High, Custom }

public static class BistroGraphics
{
    public static GraphicsPreset Preset = GraphicsPreset.Medium;
    public static float RenderScale = 1f;          // 0.5..1, 3D city only
    public static int Msaa = 2;                    // 1, 2, 4
    public static bool Shadows = true, SoftShadows = true;
    public static int ShadowResolution = 2048;     // 512..4096
    public static bool Ssao;                       // needs the SSAO renderer feature on City3DRenderer
    public static bool Ssr = true;
    public static int SsrSteps = 24;               // more steps = longer/finer reflections, more cost
    public static float SsrMaxDistance = 80f;      // metres a reflected ray may travel
    public static bool BakedGI = true, Reflections = true;
    public static bool Outlines = true, TiltShift = true, Grading = false;  // city post pass (BlueZone/CityPost)
    public static bool DetailLayer = true;     // window frames, balconies, extra rocks, fences, crosswalks ("*_detail" meshes)
    public static int DebugView;                   // 0 final .. 8, see DebugViews
    public static bool ShowFps = true;

    public static readonly string[] DebugViews = { "Final", "Albedo", "Indirect", "Direct", "Specular", "AO", "Normals", "Shadow", "SSR hits" };

    private static bool loaded;
    private static UniversalRenderPipelineAsset pipeline;
    private static int originalShadowResolution = -1, originalMsaa = -1;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        bool mobile = Application.isMobilePlatform;
        SetPreset(mobile ? GraphicsPreset.Low : GraphicsPreset.Medium, false);
        if (PlayerPrefs.HasKey("gfx.preset"))
        {
            Preset = (GraphicsPreset)PlayerPrefs.GetInt("gfx.preset");
            RenderScale = PlayerPrefs.GetFloat("gfx.scale", RenderScale);
            Msaa = PlayerPrefs.GetInt("gfx.msaa", Msaa);
            Shadows = PlayerPrefs.GetInt("gfx.shadows", Shadows ? 1 : 0) == 1;
            SoftShadows = PlayerPrefs.GetInt("gfx.soft", SoftShadows ? 1 : 0) == 1;
            ShadowResolution = PlayerPrefs.GetInt("gfx.shadowres", ShadowResolution);
            Ssao = PlayerPrefs.GetInt("gfx.ssao", Ssao ? 1 : 0) == 1;
            Ssr = PlayerPrefs.GetInt("gfx.ssr", Ssr ? 1 : 0) == 1;
            SsrSteps = PlayerPrefs.GetInt("gfx.ssrsteps", SsrSteps);
            SsrMaxDistance = PlayerPrefs.GetFloat("gfx.ssrdist", SsrMaxDistance);
            DetailLayer = PlayerPrefs.GetInt("gfx.detail", DetailLayer ? 1 : 0) == 1;
        }
        BakedGI = PlayerPrefs.GetInt("gfx.bakedgi", 1) == 1;
        Outlines = PlayerPrefs.GetInt("gfx.outlines", 1) == 1;
        TiltShift = PlayerPrefs.GetInt("gfx.tiltshift", 1) == 1;
        Grading = PlayerPrefs.GetInt("gfx.grading", 0) == 1;
        Reflections = PlayerPrefs.GetInt("gfx.reflections", 1) == 1;
        // FPS readout: on by default only in the editor and development builds, so a release web build stays clean.
        ShowFps = PlayerPrefs.GetInt("gfx.fps", Debug.isDebugBuild ? 1 : 0) == 1;
        DebugView = 0;   // debug views never persist
    }

    public static void SetPreset(GraphicsPreset preset, bool apply = true)
    {
        Preset = preset;
        switch (preset)
        {
            case GraphicsPreset.Low:
                RenderScale = .7f; Msaa = 1; Shadows = true; SoftShadows = false; ShadowResolution = 1024; Ssao = false; Ssr = false; SsrSteps = 12; SsrMaxDistance = 40; DetailLayer = false; break;
            case GraphicsPreset.Medium:
                RenderScale = .85f; Msaa = 2; Shadows = true; SoftShadows = true; ShadowResolution = 2048; Ssao = false; Ssr = true; SsrSteps = 20; SsrMaxDistance = 70; DetailLayer = true; break;
            case GraphicsPreset.High:
                RenderScale = 1f; Msaa = 4; Shadows = true; SoftShadows = true; ShadowResolution = 4096; Ssao = true; Ssr = true; SsrSteps = 40; SsrMaxDistance = 140; DetailLayer = true; break;
        }
        if (apply) { Save(); Apply(); }
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("gfx.preset", (int)Preset);
        PlayerPrefs.SetFloat("gfx.scale", RenderScale);
        PlayerPrefs.SetInt("gfx.msaa", Msaa);
        PlayerPrefs.SetInt("gfx.shadows", Shadows ? 1 : 0);
        PlayerPrefs.SetInt("gfx.soft", SoftShadows ? 1 : 0);
        PlayerPrefs.SetInt("gfx.shadowres", ShadowResolution);
        PlayerPrefs.SetInt("gfx.ssao", Ssao ? 1 : 0);
        PlayerPrefs.SetInt("gfx.ssr", Ssr ? 1 : 0);
        PlayerPrefs.SetInt("gfx.ssrsteps", SsrSteps);
        PlayerPrefs.SetFloat("gfx.ssrdist", SsrMaxDistance);
        PlayerPrefs.SetInt("gfx.detail", DetailLayer ? 1 : 0);
        PlayerPrefs.SetInt("gfx.bakedgi", BakedGI ? 1 : 0);
        PlayerPrefs.SetInt("gfx.outlines", Outlines ? 1 : 0);
        PlayerPrefs.SetInt("gfx.tiltshift", TiltShift ? 1 : 0);
        PlayerPrefs.SetInt("gfx.grading", Grading ? 1 : 0);
        PlayerPrefs.SetInt("gfx.reflections", Reflections ? 1 : 0);
        PlayerPrefs.SetInt("gfx.fps", ShowFps ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void Apply()
    {
        Load();
        Shader.SetGlobalFloat("_BZB_SSR", Ssr ? 1f : 0f);
        Shader.SetGlobalFloat("_BZB_SSRSteps", SsrSteps);
        Shader.SetGlobalFloat("_BZB_SSRMaxDistance", SsrMaxDistance);
        Shader.SetGlobalFloat("_BZB_SSRThickness", 1.5f);
        Shader.SetGlobalFloat("_BZB_DisableBakedGI", BakedGI ? 0f : 1f);
        Shader.SetGlobalFloat("_BZB_DisableReflections", Reflections ? 0f : 1f);
        Shader.SetGlobalFloat("_BZB_DebugView", DebugView);
        Shader.SetGlobalFloat("_BZB_Outline", Outlines && DebugView == 0 ? 1f : 0f);
        Shader.SetGlobalFloat("_BZB_TiltShift", TiltShift && DebugView == 0 ? 1f : 0f);
        Shader.SetGlobalFloat("_BZB_Grade", Grading && DebugView == 0 ? 1f : 0f);

        pipeline = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            if (originalShadowResolution < 0) originalShadowResolution = pipeline.mainLightShadowmapResolution;
            if (originalMsaa < 0) originalMsaa = pipeline.msaaSampleCount;
            pipeline.mainLightShadowmapResolution = ShadowResolution;
            pipeline.msaaSampleCount = Msaa;   // the city's render texture also gets Msaa samples (BistroCity3D)
        }
        var lighting = Object.FindAnyObjectByType<CityLighting>();
        if (lighting != null)
        {
            // Detail layer: the "<group>_detail" children of the city (window frames, balconies, rocks, fences...).
            foreach (Transform child in lighting.transform)
                if (child.name.EndsWith("_detail") && !child.name.Contains("_interior")) child.gameObject.SetActive(DetailLayer);
            if (lighting.sun != null) lighting.sun.shadows = !Shadows ? LightShadows.None : SoftShadows ? LightShadows.Soft : LightShadows.Hard;
            if (lighting.rendererData != null)
                foreach (var feature in lighting.rendererData.rendererFeatures)
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion")) feature.SetActive(Ssao);
        }
    }

    // In the editor the URP asset is a real file: put its shadow resolution back when play mode ends.
    public static void RestoreAssets()
    {
        if (pipeline != null && originalShadowResolution > 0) pipeline.mainLightShadowmapResolution = originalShadowResolution;
        if (pipeline != null && originalMsaa > 0) pipeline.msaaSampleCount = originalMsaa;
    }
}
