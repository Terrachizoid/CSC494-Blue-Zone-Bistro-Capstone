// File responsibility: One-time URP setup for the 3D city. The project's default renderer is the 2D renderer, which
// cannot draw 3D lighting or shadow maps. This adds a Universal (3D) renderer to the URP asset's renderer list
// (the 2D renderer stays the default for the UI) and raises the shadow distance so the whole city gets shadows.
// The city camera (BistroCity3D) then picks the 3D renderer by index. Called by the city generator; also a menu item.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class City3DRenderingSetup
{
    private const string RendererPath = "Assets/Settings/City3DRenderer.asset";
    public const float CityShadowDistance = 700f;

    [MenuItem("Tools/Blue Zone Bistro/Set Up 3D Rendering")]
    public static void Menu() => Debug.Log("3D renderer index: " + Ensure());

    // Returns the index of a Universal renderer in the active URP asset's list (adding one if needed), or -1.
    public static int Ensure()
    {
        // This project assigns URP per quality level (Graphics settings is empty), so ask for the active one.
        var pipeline = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (pipeline == null) { Debug.LogWarning("No URP asset is active; 3D city setup skipped."); return -1; }
        var so = new SerializedObject(pipeline);
        var list = so.FindProperty("m_RendererDataList");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData found) { DepthAfterOpaques(found); PostFeature(found); Shadows(pipeline); return i; }

        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
        }
        DepthAfterOpaques(data);
        PostFeature(data);
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = data;
        so.ApplyModifiedProperties();
        Shadows(pipeline);
        AssetDatabase.SaveAssets();
        Debug.Log($"Added 3D renderer '{RendererPath}' at index {index} (2D renderer stays the default).");
        return index;
    }

    private static void Shadows(UniversalRenderPipelineAsset pipeline)
    {
        if (pipeline.shadowDistance < CityShadowDistance) { pipeline.shadowDistance = CityShadowDistance; EditorUtility.SetDirty(pipeline); }
        AssetDatabase.SaveAssets();
    }

    // The water (drawn after the opaque city) reads the depth texture for SSR, so depth must be copied right after
    // the opaques, not after transparents.
    private static void DepthAfterOpaques(UniversalRendererData data)
    {
        if (data.copyDepthMode != CopyDepthMode.AfterOpaques) { data.copyDepthMode = CopyDepthMode.AfterOpaques; EditorUtility.SetDirty(data); }
    }

    private const string PostMaterialPath = "Assets/GameData/City/CityPost.mat";

    // Adds one "Full Screen Pass" renderer feature running BlueZone/CityPost (outlines, tilt-shift, grading) on the
    // 3D renderer, if it is not there yet. The material holds the tuning values; the graphics panel switches parts.
    private static void PostFeature(UniversalRendererData data)
    {
        foreach (var f in data.rendererFeatures) if (f is FullScreenPassRendererFeature) return;
        var shader = Shader.Find("BlueZone/CityPost");
        if (shader == null) { Debug.LogWarning("BlueZone/CityPost shader not found; city post-processing not added."); return; }
        var material = AssetDatabase.LoadAssetAtPath<Material>(PostMaterialPath);
        if (material == null) { material = new Material(shader) { name = "CityPost" }; AssetDatabase.CreateAsset(material, PostMaterialPath); }
        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = "City Post (outlines, tilt-shift, grading)";
        feature.passMaterial = material;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
        feature.fetchColorBuffer = true;
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
        var so = new SerializedObject(data);
        var map = so.FindProperty("m_RendererFeatureMap");
        if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
        {
            map.InsertArrayElementAtIndex(map.arraySize);
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        data.SetDirty();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("Added the city post-processing pass (outlines, tilt-shift, grading) to " + AssetDatabase.GetAssetPath(data));
    }

    public static UniversalRendererData RendererData() => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
}
