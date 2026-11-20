// File responsibility: Lighting values for the low-poly city's Blinn-Phong shader (BlueZone/LowPolyBlinnPhong).
// Sits on the city prefab root next to a child "Sun" directional light. Rotate the Sun to move the light; its
// colour/intensity, the hemisphere ambient (sky above, ground below), fog and the wellbeing tint are set here and
// pushed to the shader as globals every frame (edit mode too), so changes show immediately in the scene view.
// The Sun light itself only matters for URP's shadow map; the shader reads its direction from here.
using UnityEngine;

[ExecuteAlways]
public sealed class CityLighting : MonoBehaviour
{
    [Header("Sun (direct light)")]
    public Light sun;
    public Color sunColor = new Color(1f, 0.95f, 0.86f);
    [Range(0, 3)] public float sunIntensity = 1.05f;

    [Header("Ambient (stand-in for indirect light)")]
    public Color ambientSky = new Color(0.62f, 0.70f, 0.80f);
    public Color ambientGround = new Color(0.55f, 0.52f, 0.46f);
    [Range(0, 2)] public float ambientIntensity = 0.55f;

    [Header("Wellbeing tint (0 = healthy colours, 1 = full tint)")]
    public Color lowWellbeingTint = new Color(0.80f, 0.50f, 0.42f);
    [Range(0, 1)] public float tint;

    [Header("Fog")]
    public bool fog = true;
    public Color fogColor = new Color(0.84f, 0.88f, 0.89f);
    public float fogStart = 450f, fogEnd = 1400f;

    [Tooltip("Index of the 3D (Universal) renderer in the URP asset's renderer list. Set by the generator.")]
    public int rendererIndex = 1;
    [Tooltip("The 3D renderer asset (Assets/Settings/City3DRenderer.asset). Graphics settings switch its features (SSAO) on/off.")]
    public UnityEngine.Rendering.Universal.ScriptableRendererData rendererData;

    private static readonly int SunDir = Shader.PropertyToID("_BZB_SunDir"), SunColor = Shader.PropertyToID("_BZB_SunColor"),
        Sky = Shader.PropertyToID("_BZB_AmbientSky"), Ground = Shader.PropertyToID("_BZB_AmbientGround"),
        FogColor = Shader.PropertyToID("_BZB_FogColor"), FogParams = Shader.PropertyToID("_BZB_FogParams"),
        TintId = Shader.PropertyToID("_BZB_Tint");

    // Game hook (not wired yet): community wellbeing 0..100 -> tint. Below 'healthy' the city slowly warms/reddens.
    public void SetWellbeing(float wellbeing, float healthy = 65f) => tint = Mathf.Clamp01((healthy - wellbeing) / healthy);

    private static Vector4 Linear(Color c, float scale = 1f)
    {
        var l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        return new Vector4(l.r * scale, l.g * scale, l.b * scale, 1f);
    }

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();
    private void LateUpdate() => Apply();

    public void Apply()
    {
        var direction = sun != null ? -sun.transform.forward : new Vector3(0.45f, 0.75f, 0.35f).normalized;
        Shader.SetGlobalVector(SunDir, direction);
        Shader.SetGlobalVector(SunColor, Linear(sunColor, sunIntensity));
        Shader.SetGlobalVector(Sky, Linear(ambientSky, ambientIntensity));
        Shader.SetGlobalVector(Ground, Linear(ambientGround, ambientIntensity));
        // Wellbeing tint multiplies all indirect light (baked or hemisphere).
        Shader.SetGlobalVector(TintId, Linear(Color.Lerp(Color.white, lowWellbeingTint, tint)));
        Shader.SetGlobalVector(FogColor, Linear(fogColor));
        Shader.SetGlobalVector(FogParams, new Vector4(fogStart, fogEnd, fog ? 1f : 0f, 0f));
        if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
    }
}
