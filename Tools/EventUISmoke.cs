// Isolated Unity Play Mode smoke test. Never calls NewGame/Save or touches player checkpoints.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class EventUISmoke
{
    const string Pending="Bistro.EventUI.pending";
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);Test();}};}
    static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    static object Get(object obj,string field)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static async Task Frames(){int start=Time.frameCount;var limit=DateTime.UtcNow.AddSeconds(15);while(Time.frameCount<start+4){if(DateTime.UtcNow>limit)throw new Exception("Frames stalled");await Task.Delay(20);}}
    static void Capture(Canvas canvas,string file)
    {
        var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());RenderTexture.active=previous;
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;camera.targetTexture=null;UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(texture);rt.Release();UnityEngine.Object.Destroy(rt);
    }
    static async void Test()
    {
        try
        {
            var go=new GameObject("Event UI test");go.SetActive(false);var boot=go.AddComponent<GameBootstrap>();
            Set(boot,"foods",AssetDatabase.LoadAssetAtPath<FoodDatabase>("Assets/GameData/Foods/FoodDatabase.asset"));
            Set(boot,"listings",AssetDatabase.LoadAssetAtPath<StoreListingDatabase>("Assets/GameData/Prices/StoreListingDatabase.asset"));
            Set(boot,"recipes",AssetDatabase.LoadAssetAtPath<RecipeDatabase>("Assets/GameData/Recipes/RecipeDatabase.asset"));
            Set(boot,"customers",AssetDatabase.LoadAssetAtPath<CustomerDatabase>("Assets/GameData/Customers/CustomerDatabase.asset"));
            Set(boot,"dialogues",AssetDatabase.LoadAssetAtPath<DialogueDatabase>("Assets/GameData/Dialogues/DialogueDatabase.asset"));
            Set(boot,"balance",ScriptableObject.CreateInstance<BalanceConfig>());
            var db=AssetDatabase.LoadAssetAtPath<EventDatabase>(EventCsvImporter.AssetPath);Set(boot,"events",db);
            var state=new GameSessionState{currentDay=2,eventPlanDay=2};
            foreach(var e in db.events.Where(e=>e.id=="EV_PUBLICITY"||e.id=="EV_OATS"))state.events.Add(new EventRecord{eventId=e.id,startDay=2,expiresOnDay=4,effects=e.effects.Select(x=>x.Copy()).ToList()});
            Set(boot,"state",state);go.SetActive(true);await Frames();
            Set(boot,"<IsMainMenu>k__BackingField",false);var ui=go.GetComponent<BistroUIScreen>();Call(ui,"Refresh");await Frames();
            var canvas=go.GetComponentInChildren<Canvas>();Check(canvas.transform.Find("Event notification")!=null,"No event toast");
            Check(boot.Loop.Phase==GamePhase.Market,"Notification changed phase");
            await Task.Delay(400);await Frames();Capture(canvas,"event-toast.png");
            var toast=(RectTransform)Get(ui,"eventToast");toast.GetComponent<Button>().onClick.Invoke();await Frames();
            Check((string)Get(ui,"modal")=="event-detail"&&!toast.gameObject.activeSelf,"Detail failed or toast covers modal");
            Capture(canvas,"event-detail.png");
            var codex=canvas.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Codex:"));codex.onClick.Invoke();await Frames();
            Check(Get(ui,"eventCodex")!=null,"Codex link did not open");
            Capture(canvas,"event-codex.png");
            Set(ui,"modal",null);Set(ui,"toastElapsed",6f);Call(ui,"Refresh");await Frames();
            Check(((System.Collections.Generic.HashSet<string>)Get(ui,"announcedEvents")).Count==2,"Notification queue did not advance");
            var closing=db.events.Find(e=>e.id=="EV_CLOSED");state.events.Add(new EventRecord{eventId=closing.id,startDay=2,expiresOnDay=3,effects=closing.effects.Select(e=>e.Copy()).ToList()});
            Call(ui,"Refresh");await Frames();Check(canvas.GetComponentsInChildren<Button>().Any(b=>b.name.Contains("CLOSED")&&!b.interactable),"Closed store not visibly disabled");
            Set(ui,"modal","news");Call(ui,"Refresh");await Frames();Check(canvas.GetComponentsInChildren<Button>().Any(b=>b.name.Contains(closing.headline)),"News archive missing event");
            Debug.Log("PASS: Play Mode notification queue, nonblocking phase, detail modal, codex link, closed store button, news archive; screenshots captured.");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
