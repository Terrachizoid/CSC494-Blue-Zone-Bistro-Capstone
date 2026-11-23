// Disposable-project review only. Never install in the live Assets folder.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class CategoryColourCapture
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
            game.State.currentDay=4;
            var ui=go.AddComponent<BistroUIScreen>();ui.Initialize(game,theme);
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
                    foreach(string variant in new[]{"original","category"})
                    {
                        if(variant=="category")
                        {
                            string folder=System.Environment.GetEnvironmentVariable("BZB_COLOUR_PREVIEW_DIR");
                            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                                throw new System.InvalidOperationException("Set BZB_COLOUR_PREVIEW_DIR to the external colour-preview folder.");
                            foreach(var food in Asset<FoodDatabase>().Foods)
                            {
                                foreach(string face in new[]{"front","back"})
                                {
                                    string file=Path.Combine(folder,food.id+"_"+face+".png");if(!File.Exists(file))continue;
                                    var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);tex.LoadImage(File.ReadAllBytes(file));tex.filterMode=FilterMode.Bilinear;
                                    var sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));
                                    if(face=="front")food.icon=sprite;else food.cardBack=sprite;
                                }
                            }
                        }
                        foreach(var pair in new[]{new[]{"S001","PRODUCE"},new[]{"S002","BOTTOM_SHELF"},new[]{"S003","BULK_PRODUCE"}})
                        {
                            var shop=AssetDatabase.FindAssets("t:StoreData").Select(g=>AssetDatabase.LoadAssetAtPath<StoreData>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.id==pair[0]);
                            Set(ui,"store",shop);Set(ui,"shelf",pair[1]);Set(ui,"modal",null);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
                            foreach(var rail in go.GetComponentsInChildren<BistroShelfLayout>())rail.Apply();Canvas.ForceUpdateCanvases();
                            Save(camera,rt,"colour-"+variant+"-"+pair[0]+".png");
                        }
                    }
                    Debug.Log("CATEGORY COLOUR PASS: three store shelves, original/category variants; preview only.");EditorApplication.Exit(0);
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
