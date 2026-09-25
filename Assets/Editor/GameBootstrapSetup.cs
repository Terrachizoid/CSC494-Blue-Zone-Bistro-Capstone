// File responsibility: Editor-only default asset wiring and dialogue freshness checks for scenes/play/builds.
// Preserves explicit Inspector overrides; serialized references carry content into player builds.

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only path lookup. Serialized references carry the assets into player builds.
[InitializeOnLoad]
public class GameBootstrapSetup : IProcessSceneWithReport, IPreprocessBuildWithReport
{
    private const string BalancePath = "Assets/GameData/BalanceConfig.asset";
    private static bool queued;
    public int callbackOrder => 0;

    static GameBootstrapSetup()
    {
        QueueSetup();
        EditorApplication.hierarchyChanged += QueueSetup;
        EditorApplication.projectChanged += QueueSetup;
        EditorSceneManager.sceneOpened += (scene, mode) => QueueSetup();
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                if (!DialogueCsvImporter.EnsureCurrent())
                {
                    Debug.LogError("Dialogue import did not complete. Fix CSV errors and run Tools > Blue Zone Bistro > Import Dialogue CSV.");
                    EditorApplication.isPlaying = false;
                    return;
                }
                SetupLoadedScenes();
            }
        };
    }

    private static void QueueSetup()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) SetupLoadedScenes();
        };
    }

    public static void SetupLoadedScenes()
    {
        if (EditorApplication.isPlaying) return;
        EnsureBalance();
        DialogueCsvImporter.EnsureCurrent();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded) SetupScene(scene, false);
        }
    }

    private static void EnsureBalance()
    {
        if (AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
            AssetDatabase.CreateFolder("Assets", "GameData");
        // Do not overwrite an asset of another type at the expected location.
        if (AssetDatabase.LoadMainAssetAtPath(BalancePath) != null)
        {
            Debug.LogError("Cannot create default BalanceConfig: another asset occupies " + BalancePath);
            return;
        }
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BalanceConfig>(), BalancePath);
        AssetDatabase.SaveAssets();
    }

    private static void SetupScene(Scene scene, bool building)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var bootstrap in root.GetComponentsInChildren<GameBootstrap>(true))
        {
            var serialized = new SerializedObject(bootstrap);
            bool changed = false;
            changed |= Fill<FoodDatabase>(serialized, "foods", "Assets/GameData/Foods/FoodDatabase.asset");
            changed |= Fill<StoreListingDatabase>(serialized, "listings", "Assets/GameData/Prices/StoreListingDatabase.asset");
            changed |= Fill<RecipeDatabase>(serialized, "recipes", "Assets/GameData/Recipes/RecipeDatabase.asset");
            changed |= Fill<CustomerDatabase>(serialized, "customers", "Assets/GameData/Customers/CustomerDatabase.asset");
            changed |= Fill<DialogueDatabase>(serialized, "dialogues", "Assets/GameData/Dialogues/DialogueDatabase.asset");
            changed |= Fill<BalanceConfig>(serialized, "balance", BalancePath);
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!building) EditorSceneManager.MarkSceneDirty(scene);
            }
            if (building)
                foreach (string field in new[] { "foods", "listings", "recipes", "customers", "dialogues", "balance" })
                    if (serialized.FindProperty(field).objectReferenceValue == null)
                        throw new BuildFailedException("GameBootstrap in scene '" + scene.name +
                            "' is missing " + field + ". Run Tools > Blue Zone Bistro > Import All CSV Content before building.");
        }
    }

    private static bool Fill<T>(SerializedObject target, string field, string path) where T : Object
    {
        var property = target.FindProperty(field);
        if (property.objectReferenceValue != null) return false; // Preserve explicit overrides.
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) return false;
        property.objectReferenceValue = asset;
        return true;
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Also resolves unopened build scenes, so setup does not depend on saving an open scene.
        SetupScene(scene, report != null);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureBalance();
        if (!DialogueCsvImporter.EnsureCurrent()) throw new BuildFailedException("Dialogue CSVs are not imported successfully. Fix dialogue import errors before building.");
        if (!BistroContentValidator.Validate()) throw new BuildFailedException("Imported Bistro content has structural errors. See the Console and run Tools > Blue Zone Bistro > Validate Imported Content.");
    }
}
