using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class ShelfCapture
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
   var store=AssetDatabase.FindAssets("t:StoreData").Select(g=>AssetDatabase.LoadAssetAtPath<StoreData>(AssetDatabase.GUIDToAssetPath(g))).First(s=>s.id=="S001");
   Set(ui,"store",store);Set(ui,"shelf","PRODUCE");Call(ui,"Refresh");
   var cards=go.GetComponentsInChildren<BistroFoodCard>();
   if(cards.Any(c=>c.GetComponentsInChildren<Image>().All(i=>i.sprite==null)))throw new Exception("A card has no artwork"); var first=cards.First();first.RestoreFace(true);if(!first.IsFlipped)throw new Exception("Flip failed");first.RestoreFace(false);
   var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=1,pointerDrag=first.gameObject,position=new Vector2(200,300)};
   first.OnBeginDrag(data);var target=go.GetComponentInChildren<BistroShelfCartTarget>();target.OnDrop(data);target.OnDrop(data);first.OnEndDrag(data);
   var cart=(System.Collections.Generic.List<CartEntry>)typeof(BistroUIScreen).GetField("cart",flags).GetValue(ui);if(cart.Count!=1||cart[0].packageQuantity!=1)throw new Exception("Duplicate drop or missing cart add");
   typeof(BistroUIScreen).GetMethod("AddToCart",flags).Invoke(ui,new object[]{cards[1].Key,"offer"});Call(ui,"Refresh");
   var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
   var rt=new RenderTexture(1672,940,24);camera.targetTexture=rt;
   var canvas=go.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
   var scaler=canvas.GetComponent<CanvasScaler>();scaler.enabled=false;canvas.scaleFactor=1672f/1440;
   double until=EditorApplication.timeSinceStartup+2;
   EditorApplication.CallbackFunction finish=null;finish=()=>{if(EditorApplication.timeSinceStartup<until)return;EditorApplication.update-=finish;try{
    Canvas.ForceUpdateCanvases();foreach(var l in go.GetComponentsInChildren<BistroShelfLayout>())l.Apply();Canvas.ForceUpdateCanvases();camera.Render();
    RenderTexture.active=rt;var tex=new Texture2D(1672,940,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("shelf-render.png",tex.EncodeToPNG());
    var visible=go.GetComponentsInChildren<BistroFoodCard>(); foreach(var card in visible.Take(4))card.RestoreFace(true); Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("card-backs.png",tex.EncodeToPNG());foreach(var card in visible)card.RestoreFace(false); Set(ui,"modal","cart");Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("cart-render.png",tex.EncodeToPNG()); Set(ui,"modal",null);cart.Clear();game.State.currentDay=4;
foreach(var pair in new[]{new[]{"S001","LUXURY"},new[]{"S002","EYE_LEVEL"},new[]{"S002","BARGAIN_BIN"},new[]{"S003","PALLETS"},new[]{"S001","WELLNESS"},new[]{"S002","BOTTOM_SHELF"},new[]{"S002","BACK_FREEZER"},new[]{"S003","BULK_SACKS"},new[]{"S003","BULK_PRODUCE"}}){
var shop=AssetDatabase.FindAssets("t:StoreData").Select(g=>AssetDatabase.LoadAssetAtPath<StoreData>(AssetDatabase.GUIDToAssetPath(g))).First(s=>s.id==pair[0]);Set(ui,"store",shop);Set(ui,"shelf",pair[1]);Call(ui,"Refresh");
foreach(var listing in game.Queries.GetListings(shop).Where(l=>l.shelfId==pair[1]))if(listing.food.icon==null)throw new Exception("Missing art "+listing.food.id);
Canvas.ForceUpdateCanvases();foreach(var layout in go.GetComponentsInChildren<BistroShelfLayout>())layout.Apply();Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes(pair[1]+".png",tex.EncodeToPNG());
if(pair[1]=="LUXURY"||pair[1]=="EYE_LEVEL"||pair[1]=="BARGAIN_BIN"||pair[1]=="PALLETS"){
foreach(var card in go.GetComponentsInChildren<BistroFoodCard>())card.RestoreFace(true);
Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes(pair[1]+"-backs.png",tex.EncodeToPNG());
foreach(var card in go.GetComponentsInChildren<BistroFoodCard>())card.RestoreFace(false);
if(pair[1]=="LUXURY"){
var scroll=go.GetComponentsInChildren<ScrollRect>().First(s=>s.content.GetComponentInChildren<BistroFoodCard>()!=null);scroll.verticalNormalizedPosition=0;
Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("LUXURY-bottom.png",tex.EncodeToPNG());}}
}
Set(ui,"store",null);Set(ui,"shelf",null);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("city-toolbar.png",tex.EncodeToPNG());
var cityLayout=go.GetComponentsInChildren<BistroWorldLayout>().First(l=>l.Cover);
if(((RectTransform)cityLayout.transform).rect.width<=cityLayout.Viewport.rect.width*1.25f||((RectTransform)cityLayout.transform).rect.height<=cityLayout.Viewport.rect.height*1.25f)throw new Exception("Map must pan meaningfully on both axes");


foreach(var shopId in new[]{"S001","S002","S003"}){
Set(ui,"store",null);Set(ui,"shelf",null);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
var cityButton=go.GetComponentsInChildren<BistroWorldButton>().First(b=>b.name=="Region "+shopId);
cityButton.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,dragging=true});
if(typeof(BistroUIScreen).GetField("store",flags).GetValue(ui)!=null)throw new Exception("Dragging opened store");
cityButton.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
if(((StoreData)typeof(BistroUIScreen).GetField("store",flags).GetValue(ui)).id!=shopId)throw new Exception("Wrong store route");
foreach(var layout in go.GetComponentsInChildren<BistroWorldLayout>())layout.Apply();Canvas.ForceUpdateCanvases();
camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes(shopId+"-overview.png",tex.EncodeToPNG());
var targets=BistroWorldArt.Get(shopId).regions;
foreach(var region in targets){
Set(ui,"shelf",null);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
var button=go.GetComponentsInChildren<BistroWorldButton>().First(b=>b.name=="Region "+region.id);
if(!button.interactable)throw new Exception("Expected unlocked shelf "+region.id);
button.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});Call(ui,"Refresh");
if((string)typeof(BistroUIScreen).GetField("shelf",flags).GetValue(ui)!=region.id||go.GetComponentsInChildren<BistroFoodCard>().Length==0)throw new Exception("Shelf routing failed "+region.id);
}}
Set(ui,"store",null);Set(ui,"shelf",null);game.State.currentDay=1;Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
if(go.GetComponentsInChildren<BistroWorldButton>().First(b=>b.name=="Region S003").interactable)throw new Exception("Locked store clickable");
// Tower route and event bubble use the same archive/detail UI.
game.State.currentDay=4;Set(ui,"modal",null);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
var tower=go.GetComponentsInChildren<BistroWorldButton>().First(b=>b.name=="Region NEWS");tower.onClick.Invoke();
if((string)typeof(BistroUIScreen).GetField("modal",flags).GetValue(ui)!="news")throw new Exception("Tower archive route failed");
Set(ui,"modal",null);var eventDef=Asset<EventDatabase>().events.First();game.State.events.Add(new EventRecord{eventId=eventDef.id,startDay=4,expiresOnDay=5});
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();Call(ui,"TickEventToast");Canvas.ForceUpdateCanvases();Call(ui,"PositionTowerToast");
var toast=(RectTransform)typeof(BistroUIScreen).GetField("eventToast",flags).GetValue(ui);
if(toast==null)throw new Exception("Missing tower bubble");
camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("tower-news.png",tex.EncodeToPNG());
toast.GetComponent<Button>().onClick.Invoke();if((string)typeof(BistroUIScreen).GetField("modal",flags).GetValue(ui)!="event-detail")throw new Exception("Bubble detail route failed");
Set(ui,"toastRecord",null);toast.gameObject.SetActive(false);
game.State.inventory.Add(new InventoryEntry{foodId="P004",servings=4,daysRemaining=3,purchaseCostPerServing=1});
game.State.inventory.Add(new InventoryEntry{foodId="P001",servings=8,daysRemaining=-1,purchaseCostPerServing=.5f});
Set(ui,"notice","");
foreach(var menu in new[]{"pantry","recipes","guests","news","main-menu"}){
Set(ui,"modal",menu);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("menu-"+menu+".png",tex.EncodeToPNG());}
Set(ui,"modal","recipes");Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
var recipeCards=go.GetComponentsInChildren<BistroRecipeCard>();
if(recipeCards.Length!=game.Queries.GetUnlockedRecipes().Count)throw new Exception("Recipe count mismatch");
foreach(var recipe in Asset<RecipeDatabase>().Recipes){if(recipe.icon==null)throw new Exception("Missing recipe art "+recipe.id);foreach(var slot in recipe.slots)if(Asset<FoodDatabase>().CategoryIcon(slot.category)==null)throw new Exception("Missing category art");}
foreach(var card in recipeCards.Take(3)){card.Flip();if(!card.IsFlipped)throw new Exception("Recipe flip failed");}
Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("recipe-backs.png",tex.EncodeToPNG());
Set(ui,"modal",null);
var priorReceipt=game.State.shoppingReceipt;
game.State.shoppingReceipt=new ShoppingReceipt{day=game.State.currentDay};
foreach(var entry in game.State.inventory){game.State.shoppingReceipt.batchIds.Add(entry.inventoryEntryId);game.State.shoppingReceipt.questions.Add(new SortingQuestion{foodId=entry.foodId});}
typeof(GameLoop).GetProperty("Phase").SetValue(game.Loop,GamePhase.FoodSorting);
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("sorting-polish.png",tex.EncodeToPNG());
if(go.GetComponentsInChildren<BistroShelfIcon>().Any(i=>i.mainTexture.width!=128))throw new Exception("HUD icon did not use antialiased cache");
game.State.shoppingReceipt=priorReceipt;typeof(GameLoop).GetProperty("Phase").SetValue(game.Loop,GamePhase.Market);
Set(game.Loop,"sorting",null);
if(!game.Loop.BeginService())throw new Exception("Could not start service preview");
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-dialogue.png",tex.EncodeToPNG());
for(int n=0;n<100&&game.Loop.Dialogue.IsActive;n++)game.Loop.AdvanceDialogue();
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-recipes.png",tex.EncodeToPNG());
var choice=go.GetComponentsInChildren<BistroRecipeCard>().First();choice.Selected();Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
if(typeof(BistroUIScreen).GetField("mealRecipe",flags).GetValue(ui)==null)throw new Exception("Recipe selection failed");
camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-cooking.png",tex.EncodeToPNG());
typeof(BistroUIScreen).GetMethod("AddServing",flags).Invoke(ui,new object[]{0,"P001"});Call(ui,"Refresh");Canvas.ForceUpdateCanvases();
if(go.GetComponentsInChildren<Image>().All(i=>i.name!="Seated card"||i.sprite==null))throw new Exception("Missing seated ingredient artwork");
camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-slotted.png",tex.EncodeToPNG());
// Verify review/result/tutorial all share the compact dialogue renderer.
Set(ui,"mealPreview",game.Loop.PreviewServe((ServeInput)typeof(BistroUIScreen).GetMethod("MealInput",flags).Invoke(ui,null)));
Set(ui,"modal","meal-preview");Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-review.png",tex.EncodeToPNG());
if(go.GetComponentsInChildren<RectTransform>().Count(t=>t.name=="Bottom dialogue")!=1)throw new Exception("Review must use compact dialogue");
Set(ui,"modal",null);game.Loop.Service.lastDishResult=new ServeResult{success=true,recipeBuilt=true,healthDelta=7,delightDelta=8,popularityDelta=24,basePayment=4,tip=.8f};game.Loop.Service.step=ServiceStep.DishResult;
Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-result.png",tex.EncodeToPNG());
game.Loop.Service.step=ServiceStep.MealSelection;
var lesson=new TutorialController(game.State,Asset<TutorialDatabase>(),new IntroMealPlan(game.State,Asset<FoodDatabase>(),Asset<RecipeDatabase>(),Asset<StoreListingDatabase>(),Asset<BalanceConfig>()));game.Loop.AttachTutorials(lesson);game.State.flags.SetFlag("okinawa_chef_arrived");Set(ui,"modal","tutorial-help");Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("service-guide.png",tex.EncodeToPNG());
if(go.GetComponentsInChildren<RectTransform>().All(t=>t.name!="Bottom dialogue"))throw new Exception("Missing compact guide");
var beforeStep=lesson.Record.stepId;
for(int pageIndex=0;pageIndex<20;pageIndex++){
    var next=go.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentInChildren<Text>()?.text=="Next  ›");
    if(next==null)break;
    next.onClick.Invoke();Call(ui,"Refresh");
    if(lesson.Record.stepId!=beforeStep)throw new Exception("Paging advanced tutorial state");
}
var finishLesson=go.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>()?.text=="Continue");finishLesson.onClick.Invoke();
if(lesson.Active?.id=="T_REQUESTS")throw new Exception("Final tutorial action did not complete");
Set(ui,"modal",null);typeof(GameLoop).GetProperty("Phase").SetValue(game.Loop,GamePhase.Results);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("day-summary.png",tex.EncodeToPNG());
Debug.Log("DIALOGUE PASS: review, result and guide use bottom panel.");
Debug.Log("SERVICE PASS: scene layers, dialogue advancement and illustrated recipe selection.");
Debug.Log("RECIPE PASS: unlocked catalogue, all artwork mappings and flip backs.");typeof(GameBootstrap).GetProperty("IsMainMenu").SetValue(game,true);Call(ui,"Refresh");Canvas.ForceUpdateCanvases();camera.Render();tex.ReadPixels(new Rect(0,0,1672,940),0,0);tex.Apply();File.WriteAllBytes("menu-start.png",tex.EncodeToPNG());if(go.GetComponentInChildren<BistroMenuBlur>().GetComponent<Image>().material.shader.name!="Bistro/UI/MenuBlur")throw new Exception("Menu blur material missing");Debug.Log("NEWS PASS: tower opens archive; notification opens event detail.");Debug.Log("WORLD PASS: three city routes, ten shelf routes, drag suppression and day-one store lock.");
Debug.Log("SHELF PASS: art lookup, flip, single-drop, cart refresh, offscreen render.");EditorApplication.Exit(0);
   }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}};EditorApplication.update+=finish;
  }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
 }
}













