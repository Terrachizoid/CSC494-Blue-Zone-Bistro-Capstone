// Disposable-project review only. Never install in the live Assets folder.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class PanelArtCapture
{
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Asset<T>() where T:UnityEngine.Object => AssetDatabase.FindAssets("t:"+typeof(T).Name).Select(g=>AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).First();
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,flags).SetValue(o,v);
    static object Call(object o,string n)=>o.GetType().GetMethod(n,flags).Invoke(o,null);
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AllContentCsvImporter.ImportAll();BistroArtImport.ConfigurePanelBorders();GameBootstrapSetup.SetupLoadedScenes();
            foreach(var e in Asset<EventDatabase>().events)if(e.image==null||e.thumbnail==null)throw new Exception("Missing event art: "+e.id);
            var theme=UnityEngine.Object.Instantiate(Asset<BistroUITheme>());theme.reducedMotion=true;
            foreach(string name in new[]{"panelPaper","guideNotePanel","planPanel","suggestionFrame","soldOutStamp","newTag"})
            {
                var art=(Sprite)typeof(BistroUITheme).GetField(name).GetValue(theme);
                if(art==null)throw new Exception("Missing theme art: "+name);
                if(name!="soldOutStamp"&&art.border==Vector4.zero)throw new Exception("Missing slice borders: "+name);
            }
            var go=new GameObject("Preview only");var game=go.AddComponent<GameBootstrap>();
            Set(game,"foods",Asset<FoodDatabase>());Set(game,"listings",Asset<StoreListingDatabase>());Set(game,"recipes",Asset<RecipeDatabase>());
            Set(game,"customers",Asset<CustomerDatabase>());Set(game,"dialogues",Asset<DialogueDatabase>());Set(game,"balance",Asset<BalanceConfig>());
            Set(game,"endings",Asset<EndingDatabase>());Set(game,"events",Asset<EventDatabase>());
            game.State.currentDay=1;game.State.money=20;
            typeof(GameBootstrap).GetMethod("PrepareMorning",flags).Invoke(game,new object[]{game.State});Call(game,"BuildSession");
            typeof(GameBootstrap).GetProperty("IsMainMenu").SetValue(game,false);
            game.Planner.Add("R001");
            var ui=go.AddComponent<BistroUIScreen>();ui.Initialize(game,theme);
            Set(ui,"chatterText","Choose a dish, then tap a category in your plan to find an ingredient.");Set(ui,"chatterShownAt",Time.unscaledTime);Set(ui,"chatterUntil",Time.unscaledTime+300);
            Set(ui,"planHint",game.Queries.SuggestListing(FoodCategory.Grain,new System.Random(1),null));Set(ui,"planHintUntil",Time.unscaledTime+300);
            var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
            var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;
            var canvas=go.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            canvas.GetComponent<CanvasScaler>().enabled=false;canvas.scaleFactor=1;
            double until=EditorApplication.timeSinceStartup+2;
            EditorApplication.CallbackFunction finish=null;finish=()=>
            {
                if(EditorApplication.timeSinceStartup<until)return;EditorApplication.update-=finish;
                try
                {
                    Call(ui,"Refresh");Canvas.ForceUpdateCanvases();Save(camera,rt,"panel-plan-guide.png");
                    Set(ui,"modal","recipes");Call(ui,"Refresh");Canvas.ForceUpdateCanvases();Save(camera,rt,"panel-recipes.png");
                    var root=(RectTransform)typeof(BistroUIScreen).GetField("screen",flags).GetValue(ui);
                    var preview=BistroShelfStyle.PaperPanel(root,"Stamp review",theme,new Vector2(.3f,.3f),new Vector2(.7f,.7f));
                    BistroShelfStyle.Artwork(preview,"Stamp",theme.soldOutStamp,new Vector2(.1f,.40f),new Vector2(.9f,.75f),Color.white,true);
                    BistroShelfStyle.Text(preview,"Sold out",theme,new Vector2(.2f,.47f),new Vector2(.8f,.68f),28,new Color(.53f,.24f,.20f),true,TextAnchor.MiddleCenter).rectTransform.localRotation=Quaternion.Euler(0,0,5);
                    typeof(BistroUIScreen).GetMethod("NewBadge",flags).Invoke(ui,new object[]{preview,new Vector2(.35f,.13f),new Vector2(.65f,.30f)});
                    Canvas.ForceUpdateCanvases();Save(camera,rt,"panel-badges.png");
                    Debug.Log("PANEL ART PASS: six theme sprites, slice borders, all event sprites imported, plan/guide/recipe/badge renders.");EditorApplication.Exit(0);
                }
                catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
            };EditorApplication.update+=finish;
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Save(Camera camera,RenderTexture rt,string path)
    {
        RenderTexture.active=rt;camera.Render();var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
    }
}
