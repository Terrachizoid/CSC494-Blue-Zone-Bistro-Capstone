using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class EventPaperCapture
{
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Asset<T>() where T:UnityEngine.Object {return AssetDatabase.FindAssets("t:"+typeof(T).Name).Select(g=>AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).First();}
 static void Set(object o,string name,object v){o.GetType().GetField(name,flags).SetValue(o,v);}
 static object Call(object o,string name){return o.GetType().GetMethod(name,flags).Invoke(o,null);}
 public static void Run()
 {
  try {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); AllContentCsvImporter.ImportAll(); Debug.Log("ART sprite="+Asset<FoodDatabase>().GetById("P004")?.icon);
   if(Asset<FoodDatabase>().GetById("P004")?.icon==null)throw new Exception("Art lookup failed: Foods.csv icon_path did not import");

   var go=new GameObject("Preview only");var game=go.AddComponent<GameBootstrap>();
   Set(game,"foods",Asset<FoodDatabase>());Set(game,"listings",Asset<StoreListingDatabase>());Set(game,"recipes",Asset<RecipeDatabase>());Set(game,"customers",Asset<CustomerDatabase>());Set(game,"dialogues",Asset<DialogueDatabase>());Set(game,"balance",Asset<BalanceConfig>());Set(game,"endings",Asset<EndingDatabase>());Set(game,"events",Asset<EventDatabase>());
   game.State.currentDay=1;game.State.money=20;
   typeof(GameBootstrap).GetMethod("PrepareMorning",flags).Invoke(game,new object[]{game.State});Call(game,"BuildSession");
   typeof(GameBootstrap).GetProperty("IsMainMenu").SetValue(game,false);
   var ui=go.AddComponent<BistroUIScreen>();var theme=UnityEngine.Object.Instantiate(Asset<BistroUITheme>());theme.reducedMotion=true;ui.Initialize(game,theme);
   var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
   var rt=new RenderTexture(1672,940,24);camera.targetTexture=rt;
   var canvas=go.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
   var scaler=canvas.GetComponent<CanvasScaler>();scaler.enabled=false;canvas.scaleFactor=1672f/1440;
   double until=EditorApplication.timeSinceStartup+2;
   EditorApplication.CallbackFunction finish=null;finish=()=>{if(EditorApplication.timeSinceStartup<until)return;EditorApplication.update-=finish;try{
RenderTexture.active=rt;var tex=new Texture2D(1672,940,TextureFormat.RGB24,false);
game.State.currentDay=4;
foreach(var news in Asset<EventDatabase>().events){
 if(news.image==null||news.thumbnail==null)throw new Exception("Missing event illustration "+news.id);
 var record=new EventRecord{eventId=news.id,startDay=4,expiresOnDay=4+news.durationDays,effects=news.effects.Select(e=>e.Copy()).ToList()};
 typeof(BistroUIScreen).GetMethod("OpenEvent",flags).Invoke(ui,new object[]{record});Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
 if(go.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Bottom dialogue"))throw new Exception("Event still renders as dialogue");
 camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("news-"+news.id+".png",tex.EncodeToPNG());
 var codexButton=go.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentInChildren<Text>()?.text=="Codex · Read more");
 if(codexButton!=null){codexButton.onClick.Invoke();Call(ui,"Refresh");if(typeof(BistroUIScreen).GetField("eventCodex",flags).GetValue(ui)==null)throw new Exception("Codex link failed");}
}
Set(ui,"eventCodex",null);Set(ui,"modal",null);

Set(ui,"modal",null);game.State.currentDay=4;game.State.events.Clear();
var firstNews=Asset<EventDatabase>().events.First();game.State.events.Add(new EventRecord{eventId=firstNews.id,startDay=4,expiresOnDay=6});
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();Call(ui,"TickEventToast");Canvas.ForceUpdateCanvases();Call(ui,"PositionTowerToast");
camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("news-collapsed.png",tex.EncodeToPNG());
var toast=(RectTransform)typeof(BistroUIScreen).GetField("eventToast",flags).GetValue(ui);toast.GetComponent<Button>().onClick.Invoke();Call(ui,"Refresh");
if((string)typeof(BistroUIScreen).GetField("modal",flags).GetValue(ui)!="event-detail")throw new Exception("Toast navigation failed");
var close=go.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>()?.text=="Close");close.onClick.Invoke();Call(ui,"Refresh");
if(typeof(BistroUIScreen).GetField("modal",flags).GetValue(ui)!=null)throw new Exception("Close failed");
Debug.Log("EVENT PAPER PASS: five illustrations, articles without dialogue, Codex, tower click and close.");EditorApplication.Exit(0);
}catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}};EditorApplication.update+=finish;
}catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
}
}
