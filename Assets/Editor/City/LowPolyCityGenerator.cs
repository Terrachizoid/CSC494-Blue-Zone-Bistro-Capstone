// File responsibility: Assembles the low-poly city prefab from the generator's output. The geometry itself is made by
// Tools/City3D/citygen.py (plan once into Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv, then build), which
// writes Assets/GameData/City/Generated/*.bzbmesh (imported by BzbMeshImporter), city_manifest.csv and
// city_landmarks.csv. This file only reads those, so Unity never needs Python unless you change the city.
// Menus (Tools > Blue Zone Bistro):
//   Generate Low-Poly City          build the prefab from the generated meshes (no Python needed)
//   Rebuild City Meshes (Python)    run citygen.py build (after editing the lots/layout tables), then generate
//   Reroll City Plan (Python)       replace the lots table with a fresh random plan (asks first), then build
// Output: prefab Assets/GameData/City/LowPolyCity.prefab with one renderer per group per tile (tight bounds, so the
// camera skips tiles off screen), a Sun light, CityLighting and CityLandmark click boxes.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public static class LowPolyCityGenerator
{
    public const string LayoutPath = "Assets/GameData/City/Blue_Zone_Bistro_CityLayout.csv";
    public const string LotsPath = "Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv";
    private const string GeneratedFolder = "Assets/GameData/City/Generated";
    private const string ManifestPath = GeneratedFolder + "/city_manifest.csv";
    private const string ShelvesPath = GeneratedFolder + "/city_shelves.csv";
    private const string LandmarksPath = GeneratedFolder + "/city_landmarks.csv";
    private const string PrefabPath = "Assets/GameData/City/LowPolyCity.prefab";
    private const string MaterialPath = "Assets/GameData/City/LowPolyCity.mat";
    private const string WaterMaterialPath = "Assets/GameData/City/LowPolyWater.mat";
    private const string LightingSettingsPath = "Assets/GameData/City/CityBake.lighting";
    private const string GeneratorScript = "Tools/City3D/citygen.py";

    [MenuItem("Tools/Blue Zone Bistro/Generate Low-Poly City")]
    public static void GenerateMenu()
    {
        var prefab = Generate();
        if (prefab == null) return;
        var existing = GameObject.Find("Low-Poly City");
        if (existing == null) existing = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // Off to the side, so the 2D game camera (looking at the origin) never sees it. Press F in the scene view to frame it.
        // This scene copy is the one the game shows (BistroCity3D uses it), and the one Unity bakes lighting for.
        existing.transform.SetPositionAndRotation(new Vector3(1000f, 0f, 1000f), Quaternion.identity);
        Selection.activeGameObject = existing;
        EnsureBakeSettings();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(existing.scene);
    }

    [MenuItem("Tools/Blue Zone Bistro/Rebuild City Meshes (Python)")]
    public static void RebuildMenu() { if (RunPython("build")) GenerateMenu(); }

    [MenuItem("Tools/Blue Zone Bistro/Reroll City Plan (Python)")]
    public static void RerollMenu()
    {
        if (!EditorUtility.DisplayDialog("Reroll city plan",
            "This replaces " + LotsPath + " with a new random plan. Every hand edit in that table is lost.", "Reroll", "Cancel")) return;
        if (RunPython("plan --reroll") && RunPython("build")) GenerateMenu();
    }

    // Runs citygen.py from the project root. Tries "python", then "py" (Windows launcher), then "python3".
    private static bool RunPython(string args)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        foreach (var exe in new[] { "python", "py", "python3" })
        {
            try
            {
                var info = new ProcessStartInfo(exe, $"\"{GeneratorScript}\" {args} .")
                { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using (var process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0) { Debug.LogError($"citygen.py {args} failed:\n{output}\n{error}"); return false; }
                    Debug.Log($"citygen.py {args}:\n{output}");
                    AssetDatabase.Refresh();
                    return true;
                }
            }
            catch (System.ComponentModel.Win32Exception) { /* not installed under this name; try the next */ }
        }
        Debug.LogError("Python 3 was not found (tried python, py, python3). Install it from python.org, or run " + GeneratorScript + " by hand.");
        return false;
    }

    private static float F(string s) => string.IsNullOrWhiteSpace(s) ? 0f : float.Parse(s, CultureInfo.InvariantCulture);

    public static GameObject Generate()
    {
        if (!CsvImportUtility.TryRead(ManifestPath, "City meshes", new[] { "mesh", "group", "shadows" }, out var manifest, out var mcol))
        { Debug.LogError("No generated city found at " + ManifestPath + ". Run Tools > Blue Zone Bistro > Rebuild City Meshes (Python)."); return null; }
        bool hasLandmarks = CsvImportUtility.TryRead(LandmarksPath, "City landmarks", new[] { "id", "cx", "cy", "cz", "sx", "sy", "sz" }, out var landmarks, out var lcol);
        bool hasShelves = CsvImportUtility.TryRead(ShelvesPath, "Store shelves", new[] { "store", "shelf", "cx", "cy", "cz", "sx", "sy", "sz" }, out var shelfRows, out var scol);

        RemoveOldMeshAssets();
        int rendererIndex = City3DRenderingSetup.Ensure();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("BlueZone/LowPolyBlinnPhong") ?? Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "LowPolyCity", doubleSidedGI = true };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (water == null)
        {
            var shader = Shader.Find("BlueZone/LowPolyWater") ?? material.shader;
            water = new Material(shader) { name = "LowPolyWater", doubleSidedGI = true };
            AssetDatabase.CreateAsset(water, WaterMaterialPath);
        }

        var root = new GameObject("Low-Poly City");
        try
        {
            int triangles = 0, missing = 0;
            var groups = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var row in manifest.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r)))
            {
                string name = CsvImportUtility.Get(row, mcol, "mesh").Trim(), group = CsvImportUtility.Get(row, mcol, "group").Trim();
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedFolder + "/" + name + ".bzbmesh");
                if (mesh == null) { missing++; continue; }
                if (!groups.TryGetValue(group, out var parent))
                {
                    parent = new GameObject(group).transform; parent.SetParent(root.transform, false); groups[group] = parent;
                }
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = group == "water" ? water : material;
                renderer.shadowCastingMode = CsvImportUtility.Get(row, mcol, "shadows").Trim() == "0" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                // Static for baking. Water is not lightmapped (it is lit live and reflects). Small pieces (trees, props,
                // the detail layer: window frames, chairs, rails) still bounce light but take their own indirect light
                // from the probes instead of the lightmap: their facets are smaller than a lightmap texel, so lightmap
                // islands for them only blur into each other.
                var flags = (StaticEditorFlags)~0;
                if (group == "water") flags &= ~StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                bool small = group == "trees" || group == "props" || group.EndsWith("_detail");
                bool interior = group.Contains("_interior");
                if (small || interior) renderer.receiveGI = ReceiveGI.LightProbes;
                if (interior) GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.ContributeGI);   // not in the city bake
                var so = new SerializedObject(renderer);
                var scale = so.FindProperty("m_ScaleInLightmap");
                if (scale != null) { scale.floatValue = group.EndsWith("_detail") ? 0.25f : group == "trees" ? 0.25f : group == "props" ? 0.3f : group == "roads" ? 0.5f : 1f; so.ApplyModifiedPropertiesWithoutUndo(); }
                triangles += (int)(mesh.GetIndexCount(0) / 3);
            }
            if (missing > 0) Debug.LogWarning($"{missing} city meshes listed in the manifest were not found (still importing? try again).");

            // Stores: each store's shell and interior groups (store_<id>_shell / _interior, + _detail) stay direct
            // children of the city (the detail toggle looks there); a CityStore object ties them together. The
            // interior starts hidden and gets one box collider per shelf zone (StoreShelf, from city_shelves.csv).
            var storeRoot = new GameObject("Stores"); storeRoot.transform.SetParent(root.transform, false);
            var storeIds = groups.Keys.Where(g => g.StartsWith("store_")).Select(g => g.Split('_')[1]).Distinct().OrderBy(x => x);
            foreach (var sid in storeIds)
            {
                var store = new GameObject("Store " + sid, typeof(CityStore)).GetComponent<CityStore>();
                store.transform.SetParent(storeRoot.transform, false);
                store.storeId = sid;
                store.shell = groups.Where(kv => kv.Key.StartsWith("store_" + sid + "_shell")).Select(kv => kv.Value.gameObject).ToArray();
                store.interior = groups.Where(kv => kv.Key.StartsWith("store_" + sid + "_interior")).Select(kv => kv.Value.gameObject).ToArray();
                if (hasShelves && groups.TryGetValue("store_" + sid + "_interior", out var inside))
                    foreach (var row in shelfRows.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r) && CsvImportUtility.Get(r, scol, "store").Trim() == sid))
                    {
                        string shelfId = CsvImportUtility.Get(row, scol, "shelf").Trim();
                        var shelf = new GameObject("Shelf " + sid + " " + shelfId, typeof(BoxCollider), typeof(StoreShelf));
                        shelf.transform.SetParent(inside, false);
                        shelf.transform.localPosition = new Vector3(F(CsvImportUtility.Get(row, scol, "cx")), F(CsvImportUtility.Get(row, scol, "cy")), F(CsvImportUtility.Get(row, scol, "cz")));
                        shelf.GetComponent<BoxCollider>().size = new Vector3(F(CsvImportUtility.Get(row, scol, "sx")), F(CsvImportUtility.Get(row, scol, "sy")), F(CsvImportUtility.Get(row, scol, "sz")));
                        var marker = shelf.GetComponent<StoreShelf>(); marker.storeId = sid; marker.shelfId = shelfId;
                    }
                foreach (var go in store.interior) go.SetActive(false);
            }

            var landmarkRoot = new GameObject("Landmarks"); landmarkRoot.transform.SetParent(root.transform, false);
            int count = 0;
            if (hasLandmarks)
                foreach (var row in landmarks.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r)))
                {
                    string id = CsvImportUtility.Get(row, lcol, "id").Trim();
                    var go = new GameObject("Landmark " + id, typeof(BoxCollider), typeof(CityLandmark));
                    go.transform.SetParent(landmarkRoot.transform, false);
                    go.transform.localPosition = new Vector3(F(CsvImportUtility.Get(row, lcol, "cx")), F(CsvImportUtility.Get(row, lcol, "cy")), F(CsvImportUtility.Get(row, lcol, "cz")));
                    go.GetComponent<BoxCollider>().size = new Vector3(F(CsvImportUtility.Get(row, lcol, "sx")), F(CsvImportUtility.Get(row, lcol, "sy")), F(CsvImportUtility.Get(row, lcol, "sz")));
                    go.GetComponent<CityLandmark>().landmarkId = id;
                    count++;
                }

            // Lighting: one directional Sun (rotate it to move the light) + CityLighting with the shader's values.
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.transform.localRotation = Quaternion.Euler(50f, -35f, 0f);
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 1f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;   // bounced light baked, direct light and shadows live
            var lighting = root.AddComponent<CityLighting>();
            lighting.sun = sun; lighting.rendererIndex = rendererIndex < 0 ? 0 : rendererIndex;
            KeepLighting(lighting);
            lighting.rendererData = City3DRenderingSetup.RendererData();

            // One baked reflection probe over the whole city (box projection keeps reflections roughly in place).
            var bounds = new Bounds(); bool first = true;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name.StartsWith("water")) continue;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            var probe = new GameObject("Reflection Probe", typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
            probe.transform.SetParent(root.transform, false);
            probe.transform.localPosition = new Vector3(bounds.center.x, 25f, bounds.center.z);
            probe.mode = ReflectionProbeMode.Baked; probe.boxProjection = true; probe.resolution = 256;
            probe.size = new Vector3(bounds.size.x + 40f, 120f, bounds.size.z + 40f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Low-poly city: {triangles} triangles in {root.GetComponentsInChildren<MeshRenderer>().Length} meshes, {count} landmarks.");
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Keep lighting values tuned on the previous prefab (sun angle, colours, fog) when rebuilding it.
    private static void KeepLighting(CityLighting fresh)
    {
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var previous = old != null ? old.GetComponent<CityLighting>() : null;
        if (previous == null) return;
        int index = fresh.rendererIndex; var sun = fresh.sun;
        EditorUtility.CopySerialized(previous, fresh);
        fresh.sun = sun; fresh.rendererIndex = index;
        if (previous.sun != null) { sun.color = previous.sun.color; sun.intensity = previous.sun.intensity; }
        if (previous.sun != null) sun.transform.localRotation = previous.sun.transform.localRotation;
    }

    // Scene bake settings for the city: baked bounced light ("Baked Indirect": the sun's direct light and shadows stay
    // live), ambient occlusion in the bake, 1 lightmap texel per metre (raise for sharper AO, at the cost of bake time
    // and download size). Only creates settings if the scene has none; otherwise just makes sure baked GI is on.
    [MenuItem("Tools/Blue Zone Bistro/Set Up City Baking")]
    public static void EnsureBakeSettings()
    {
        if (!Lightmapping.TryGetLightingSettings(out var settings) || settings == null)
        {
            settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "CityBake" };
                settings.realtimeGI = false;
                settings.lightmapResolution = 1f;
                settings.lightmapMaxSize = 2048;
                settings.lightmapPadding = 2;
                settings.maxBounces = 3;
                settings.directSampleCount = 32;
                settings.indirectSampleCount = 256;
                settings.environmentSampleCount = 256;
                settings.ao = true;
                settings.aoMaxDistance = 3f;
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }
            Lightmapping.lightingSettings = settings;
        }
        settings.bakedGI = true;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        EditorUtility.SetDirty(settings);
        // Sky light for the bake: blue from above, neutral at the horizon, warm from the ground. With a warm sun this
        // gives the classic warm-light / cool-shadow look instead of everything beige. Saved with the scene.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.64f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.67f, 0.70f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.36f);
        Debug.Log("City baking ready: Window > Rendering > Lighting > Generate Lighting.");
    }

    // The first generator saved City_*.asset meshes in the same folder; the prefab no longer uses them.
    private static void RemoveOldMeshAssets()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { GeneratedFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path).StartsWith("City_") && path.EndsWith(".asset")) AssetDatabase.DeleteAsset(path);
        }
    }
}
