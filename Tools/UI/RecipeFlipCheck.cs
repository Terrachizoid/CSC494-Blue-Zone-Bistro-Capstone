// Copy into the disposable project's Assets/Editor; do not install in the live game.
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RecipeFlipCheck
{
    private const string Key="Bistro.RecipeFlipCheck";
    private static BistroRecipeCard card;
    private static double started;
    private static bool narrowed;
    static RecipeFlipCheck(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false)||state!=PlayModeStateChange.EnteredPlayMode)return;
        try
        {
            var root=new GameObject("Flip test",typeof(Canvas));
            var theme=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BistroUITheme>("Assets/GameData/BistroUITheme.asset"));
            theme.reducedMotion=false;theme.cardFlipSeconds=.4f;
            var recipe=AssetDatabase.LoadAssetAtPath<RecipeData>("Assets/GameData/Recipes/R001.asset");
            var foods=AssetDatabase.LoadAssetAtPath<FoodDatabase>("Assets/GameData/Foods/FoodDatabase.asset");
            card=BistroRecipeCard.Create(root.transform,recipe,theme,foods);
            Application.runInBackground=true;Time.timeScale=0;card.Flip();card.Flip(); // Duplicate click must not start another flip.
            started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
        }
        catch(Exception ex){Fail(ex);}
    }
    private static void Tick()
    {
        try
        {
            narrowed|=card.transform.localScale.x<.95f;
            if(EditorApplication.timeSinceStartup-started<3)return;
            EditorApplication.update-=Tick;
            if(!narrowed||!card.IsFlipped||card.transform.localScale!=Vector3.one)throw new Exception("Flip failed: narrowed="+narrowed+" flipped="+card.IsFlipped+" scale="+card.transform.localScale+" frames="+Time.frameCount);
            card.Flip();card.gameObject.SetActive(false);
            if(card.transform.localScale!=Vector3.one)throw new Exception("Interrupted flip did not restore scale");
            var theme=ScriptableObject.CreateInstance<BistroUITheme>();theme.reducedMotion=true;
            var instant=BistroRecipeCard.Create(card.transform.parent,AssetDatabase.LoadAssetAtPath<RecipeData>("Assets/GameData/Recipes/R001.asset"),theme,AssetDatabase.LoadAssetAtPath<FoodDatabase>("Assets/GameData/Foods/FoodDatabase.asset"));
            instant.Flip();if(!instant.IsFlipped||instant.transform.localScale!=Vector3.one)throw new Exception("Reduced motion must flip instantly");
            Time.timeScale=1;SessionState.SetBool(Key,false);Debug.Log("FLIP PLAYMODE PASS: animation, paused time, duplicate click, interruption and reduced motion.");EditorApplication.Exit(0);
        }
        catch(Exception ex){Fail(ex);}
    }
    private static void Fail(Exception ex){EditorApplication.update-=Tick;SessionState.SetBool(Key,false);Debug.LogException(ex);EditorApplication.Exit(1);}
}
