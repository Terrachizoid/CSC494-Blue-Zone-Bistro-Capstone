// Canvas navigation for market, sorting, meal building, dialogue and results.
// Owns temporary UI selections only. All purchases/refunds go through GameLoop.
// Service-specific presentation is in BistroUIScreen.Service.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public partial class BistroUIScreen : MonoBehaviour
{
    [SerializeField] private BistroUITheme theme;
    private GameBootstrap game;
    private Canvas canvas;
    private RectTransform screen;
    private readonly List<CartEntry> cart=new List<CartEntry>();
    private readonly Dictionary<string,TrafficLight> answers=new Dictionary<string,TrafficLight>();
    // Servings waiting in the pantry SELL slot, by batch. Nothing is sold until "Sell" is confirmed.
    private readonly Dictionary<string,int> sellSelections=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private StoreData store;
    private string shelf, notice="", modal;
    // Where the player checked out, so finishing that purchase's sorting returns them to the same store/shelf.
    private StoreData returnStore; private string returnShelf;
    private bool dirty;
    private string motionContext;
    private bool ownsTheme;
    public void Initialize(GameBootstrap bootstrap,BistroUITheme configuredTheme=null)
    {
        game=bootstrap;
        if(configuredTheme!=null)theme=configuredTheme;
        if(theme==null){theme=ScriptableObject.CreateInstance<BistroUITheme>();ownsTheme=true;}
        var root=new GameObject("Bistro Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
        var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=.5f;
        if(EventSystem.current==null){var events=new GameObject("Bistro input",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);}
        else if(EventSystem.current.GetComponent<BaseInputModule>()==null)EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
        subscribedLoop=game.Loop;subscribedLoop.PhaseChanged+=PhaseChanged;Refresh();
    }
    private void PhaseChanged(GamePhase phase)
    {
        ResetMeal();sellSelections.Clear();store=null;shelf=null;modal=null;answers.Clear();notice="";dirty=true;
        if(phase==GamePhase.Market&&returnStore!=null){store=returnStore;shelf=returnShelf;notice=PurchaseDone;}
        else if(phase==GamePhase.Market&&game.Loop.BistroOpened)notice="Your guests will wait. Shop, then click your Bistro on the map to go back.";
        if(phase!=GamePhase.FoodSorting){returnStore=null;returnShelf=null;}
    }
    private const string PurchaseDone="Purchase complete. Shop more, or click your Bistro on the map when you're ready to cook.";
    private void LateUpdate(){TickEventToast();TickPlanHint();if(!game.IsMainMenu)TickChatter();if(!game.IsMainMenu)SyncVoice();if(dirty){dirty=false;Refresh();}}
    private void OnDestroy(){if(subscribedLoop!=null)subscribedLoop.PhaseChanged-=PhaseChanged;if(ownsTheme)Destroy(theme);}
    private void Change(Action action){action();dirty=true;}
    // Redraw after something outside the UI changed the session (debug panel).
    public void MarkDirty(){dirty=true;}
    private void Refresh()
    {
        canvas.gameObject.SetActive(true);
        RememberScroll();
        RememberCardFaces();
        RetireScreen();
        screen=BistroWidgets.Box(canvas.transform,"Current screen",theme.background,Vector2.zero,Vector2.one);
        dialogueRequests.Clear();
        if(game.IsMainMenu){MainMenu();StyleMenu(screen);FlushDialogue();AnimateNavigation();return;}
        if(game.Loop.Phase==GamePhase.Completed){EndingScreen();FlushDialogue();AnimateNavigation();return;}
        game.Loop.RefreshTutorials();
        BistroWidgets.Label(screen,game.SaveFailed?game.SaveMessage:notice,theme,new Vector2(.03f,0),new Vector2(.97f,.065f),18);
        TrackShopLesson();
        if(game.Loop.Phase==GamePhase.Market)Market();else if(game.Loop.Phase==GamePhase.FoodSorting)Sorting();else if(game.Loop.Phase==GamePhase.Service)ServiceScreen();else ResultsScreen();
        MealPlanPanel();
        if(game.SaveFailed)BistroWidgets.Button(screen,"Retry checkpoint save",theme,new Vector2(.72f,.005f),new Vector2(.98f,.06f),()=>Change(()=>game.RetrySave()));
        GlobalToolbar();
        if(modal=="news"||modal=="event-detail")EventPanel();else if(modal=="main-menu")ReturnMenuPanel();else if(modal!=null && modal!="tutorial-help")Modal();
        // Guide dialogues come after every panel so they are always on top (they are layered where requested).
        LessonOverlays();
        PresentTutorial();
        ChatterBubble();
        foreach(Transform child in screen)if(child.name=="Modal blocker"||child.name=="Return shade"||child.name=="Tutorial")StyleMenu(child);
        FlushDialogue();
        RestoreScroll();
        RestoreCardFaces();
        AnimateNavigation();
    }
    private void Market()
    {
        if(store==null){CityMap();return;}
        // Every listing's shelf is checked on import (StoreListingCsvImporter), so a chosen shelf always exists.
        if(shelf!=null){IllustratedShelf();return;}
        if(StoreOverview())return;
        // Fallback for a store without a background or world.json click regions: plain shelf buttons.
        BistroWidgets.Label(screen,store.displayName,theme,new Vector2(.03f,.82f),new Vector2(.96f,.89f),25);
        var interior=BistroWidgets.Box(screen,"Store interior",theme.panel,new Vector2(.03f,.20f),new Vector2(.97f,.79f));
        if(store.shopBackground!=null){interior.GetComponent<Image>().sprite=store.shopBackground;interior.GetComponent<Image>().color=Color.white;}
        var shelves=game.Queries.GetListings(store).Select(l=>l.shelfId).Distinct().ToList();
        for(int i=0;i<shelves.Count;i++)
        {string id=shelves[i];var definition=store.shelves.FirstOrDefault(s=>s!=null&&s.id==id);float x=.04f+(i%3)*.32f,y=.64f-(i/3)*.25f;
            BistroWidgets.Button(interior,definition?.displayName??id,theme,new Vector2(x,y),new Vector2(x+.28f,y+.19f),()=>Change(()=>shelf=id));}
        BistroWidgets.EmptyShelf(interior,"Nothing is for sale here today.",theme);
    }
    private void AddToCart(string id,string kind)
    {
        if(kind!="offer")return;
        Change(()=>AddToCartNow(id));
    }
    private void AddToCartNow(string id){var entry=cart.Find(c=>c.listingId==id);if(entry==null)cart.Add(new CartEntry{listingId=id});else if(entry.packageQuantity<10000)entry.packageQuantity++;notice="Package added to your cart.";}
    // Sorting uses the same illustrated kitchen as cooking: room behind, cream workspace, art cards,
    // and three bins with Traffic Light shapes (circle GO, triangle SLOW, octagon STOP) so colour is never the only cue.
    private static readonly TrafficLight[] SortBins={TrafficLight.Green,TrafficLight.Yellow,TrafficLight.Red};
    private static string LightSymbol(TrafficLight light)=>light==TrafficLight.Green?"LightGo":light==TrafficLight.Yellow?"LightSlow":"LightStop";
    private static Color LightColour(TrafficLight light)=>light==TrafficLight.Green?new Color(.33f,.55f,.36f):light==TrafficLight.Yellow?new Color(.84f,.64f,.22f):new Color(.72f,.30f,.27f);
    private RectTransform KitchenWorkspace(string name)
    {
        var room=BistroWidgets.Box(screen,"Bistro interior",theme.panel,Vector2.zero,new Vector2(1,.91f));
        var background=room.GetComponent<Image>();background.raycastTarget=false;
        if(theme.serviceBackground!=null){background.sprite=theme.serviceBackground;background.color=Color.white;}
        BistroWidgets.Box(room,"Kitchen dim",new Color(0,0,0,.24f),Vector2.zero,Vector2.one).GetComponent<Image>().raycastTarget=false;
        return BistroShelfStyle.PaperPanel(room,name,theme,new Vector2(.025f,.025f),new Vector2(.975f,.97f));
    }
    private void Sorting()
    {
        var result=game.Loop.SortingResult;
        var desk=KitchenWorkspace("Sorting workspace");
        BistroSurface.Dress(desk,BistroShelfStyle.Green,theme.cardSprig);
        if(result!=null){SortingResults(desk,result);return;}
        var ink=BistroShelfStyle.Ink;
        int total=game.Loop.SortingQuestions.Count(),placed=answers.Count;
        BistroShelfStyle.Text(desk,"Sort your new food",theme,new Vector2(.03f,.905f),new Vector2(.62f,.99f),30,ink,true);
        BistroShelfStyle.Text(desk,$"Drag each new food into a bin, or press one of the shapes on its card. Click a card to read its label. Each correct answer gives back {game.Balance.maximumSortingDiscount*100:0}% of what you paid for that food.",theme,new Vector2(.03f,.845f),new Vector2(.64f,.905f),17,ink);
        var grid=BistroWidgets.Grid(desk,theme,new Vector2(.015f,.13f),new Vector2(.64f,.84f));
        ((RectTransform)grid.parent).GetComponent<Image>().color=Color.clear;
        grid.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(176,250));
        foreach(var q in game.Loop.SortingQuestions.Where(q=>!answers.ContainsKey(q.foodId)))
        {
            var food=game.Queries.Foods.GetById(q.foodId);
            var batches=game.State.inventory.Where(e=>game.State.shoppingReceipt.batchIds.Contains(e.inventoryEntryId)&&e.foodId==q.foodId).ToList();
            int earliest=batches.Where(e=>e.daysRemaining>=0).Select(e=>e.daysRemaining).DefaultIfEmpty(-1).Min();
            int servings=batches.Sum(e=>e.servings);
            var card=BistroFoodCard.Create(grid,food,q.foodId,"sort",$"{servings} purchased servings\n{Expiry(earliest)}",theme);
            card.ApplyShelfArtwork(food,"",servings);
            // Click alternative to dragging (2026-11-18): one small button per bin, with that bin's shape and colour.
            string foodId=q.foodId;
            for(int b=0;b<SortBins.Length;b++)
            {
                var answer=SortBins[b];float y=.70f-b*.12f;
                var pick=BistroShelfStyle.Button(card.FrontLayer,"",theme,new Vector2(.80f,y),new Vector2(.96f,y+.105f),Color.Lerp(Color.white,LightColour(answer),.35f),ink,()=>Change(()=>answers[foodId]=answer));
                BistroShelfIcon.Add(pick.transform,LightSymbol(answer),new Vector2(.15f,.15f),new Vector2(.85f,.85f),LightColour(answer));
                pick.transform.SetAsLastSibling();
            }
        }
        BistroWidgets.EmptyShelf(grid,"All foods placed.\nDrag a food between bins, or out of a bin to take it back.",theme);
        for(int i=0;i<SortBins.Length;i++)
        {
            var answer=SortBins[i];float top=.97f-i*.285f;
            var inBin=answers.Where(p=>p.Value==answer).Select(p=>p.Key).ToList();
            var bin=BistroDropSlot.Create(desk,answer.ToString().ToUpperInvariant(),inBin.Count==0?"Drop food here":"",theme,new Vector2(.665f,top-.265f),new Vector2(.975f,top),false,
                (id,kind)=>{if(kind=="sort")Change(()=>answers[id]=answer);},null);
            bin.ApplyIllustratedContents(null);
            // Rows keep kind "sort": dropping one on another bin moves it there; dropping it anywhere else takes it back.
            bin.ShowRows(inBin.Select(id=>new BistroDropSlot.Row{key=id,label=game.Queries.Foods.GetById(id)?.displayName??id,art=game.Queries.Foods.GetById(id)?.icon}).ToList(),
                "sort",id=>Change(()=>answers.Remove(id)),theme);
            bin.GetComponent<Image>().color=Color.Lerp(new Color(.94f,.92f,.86f),LightColour(answer),.22f);
            BistroSurface.Dress((RectTransform)bin.transform,LightColour(answer),theme.cardSprig);
            BistroShelfIcon.Add(bin.transform,LightSymbol(answer),new Vector2(.035f,.75f),new Vector2(.15f,.97f),LightColour(answer));
            var title=bin.GetComponentInChildren<Text>();title.rectTransform.anchorMin=new Vector2(.17f,.74f);title.alignment=TextAnchor.MiddleLeft;
        }
        BistroShelfStyle.Text(desk,string.IsNullOrEmpty(notice)?$"{placed} of {total} placed":notice,theme,new Vector2(.03f,.03f),new Vector2(.25f,.11f),20,ink);
        BistroActionButton.Create(desk,"Submit answers",theme,new Vector2(.27f,.025f),new Vector2(.64f,.115f),BistroActionRole.Primary,()=>Change(()=>{
            var resultSubmit=game.Loop.SubmitSorting(answers.Select(p=>new SortingChoice{foodId=p.Key,answer=p.Value}).ToList());notice=resultSubmit.success?"":resultSubmit.error;}),true);
    }
    // One row per sorted food: picture, verdict with the correct Traffic Light shape, and the label explanation.
    private void SortingResults(RectTransform desk,SortingResult result)
    {
        var ink=BistroShelfStyle.Ink;
        BistroShelfStyle.Text(desk,$"{result.correctCount} of {result.questionCount} sorted correctly",theme,new Vector2(.03f,.905f),new Vector2(.70f,.99f),30,ink,true);
        BistroShelfStyle.Text(desk,result.refundAmount>0?$"Refund ${result.refundAmount:0.00} added to your wallet":$"No refund this time: each correct answer gives back {game.Balance.maximumSortingDiscount*100:0}% of that food's price",theme,new Vector2(.03f,.845f),new Vector2(.70f,.905f),18,ink);
        var list=BistroWidgets.Grid(desk,theme,new Vector2(.015f,.13f),new Vector2(.985f,.84f));
        ((RectTransform)list.parent).GetComponent<Image>().color=Color.clear;
        list.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(620,170));
        foreach(var f in result.feedback)
        {
            var food=game.Queries.Foods.GetById(f.foodId);
            var row=BistroShelfStyle.Panel(list,"Sorting result",new Color(.98f,.96f,.91f),Vector2.zero,Vector2.one);
            var thumb=BistroShelfStyle.Panel(row,"Food thumbnail",Color.white,new Vector2(.015f,.06f),new Vector2(.2f,.94f));
            thumb.GetComponent<Image>().sprite=food?.icon;thumb.GetComponent<Image>().preserveAspect=true;thumb.GetComponent<Image>().raycastTarget=false;
            if(food?.icon==null)thumb.GetComponent<Image>().color=new Color(.84f,.87f,.79f);
            BistroShelfStyle.Text(row,food?.displayName??f.foodId,theme,new Vector2(.23f,.70f),new Vector2(.80f,.96f),21,ink,true);
            BistroShelfIcon.Add(row,LightSymbol(f.correct),new Vector2(.83f,.70f),new Vector2(.89f,.95f),LightColour(f.correct));
            BistroShelfStyle.Text(row,f.isCorrect?(f.refund>0?$"Correct · ${f.refund:0.00} back":"Correct"):"Wrong bin · no refund",theme,new Vector2(.23f,.52f),new Vector2(.97f,.71f),16,f.isCorrect?BistroShelfStyle.Green:theme.warning);
            var why=BistroShelfStyle.Text(row,f.explanation,theme,new Vector2(.23f,.05f),new Vector2(.97f,.52f),15,ink);why.alignment=TextAnchor.UpperLeft;
        }
        BistroActionButton.Create(desk,"Back to the market",theme,new Vector2(.64f,.025f),new Vector2(.975f,.115f),BistroActionRole.Primary,()=>Change(()=>{if(!game.Loop.ReturnToMarket())notice=game.Loop.Tutorials?.LastError??"";}),true);
    }
    // Opening a bin shows its cards in the same style as the cart review; drag one out to take it back.
    private void Modal()
    {
        if(modal=="recipes"){RecipeBook();return;}
        if(modal=="meal-preview"){
            bool warning=!mealPreview.recipeBuilt||mealPreview.activeCravings.Length>mealPreview.cravingsResolved.Length;
            DialoguePanel("Review your dish",ReviewText(mealPreview),warning?"Serve anyway":"Serve",()=>{
                var result=game.Loop.Serve(MealInput());modal=null;if(result.success)ResetMeal();else Explain(result.error);
            },()=>modal=null,true,null,"Keep cooking");return;
        }
        if(modal=="guests"){game.Loop.NoteSeen(TutorialFlags.SeenGuests);
            var text=$"Morning popularity: {game.State.morningPopularity:0} · Up to {game.State.dailyCustomerCapacity} guests\n\n"+string.Join("\n\n",game.Queries.GetExpectedCustomers().Select(c=>{
                var requests=game.Queries.GetActiveCravings(c.id);return c.displayName+" · "+(requests.Length==0?"No specific request today":string.Join(", ",requests));}));
            DialoguePanel("Today's guests",text,"Back",()=>modal=null);return;
        }
        if(modal=="cart"){IllustratedCartReview();return;}
        if(modal=="checkout-confirm"){CheckoutConfirmation();return;}
        if(modal=="bistro-confirm"){BistroConfirmation();return;}
        if(modal!="pantry"){modal=null;return;} // the only remaining generic panel
        var shade=BistroWidgets.Box(screen,"Modal blocker",new Color(0,0,0,.70f),Vector2.zero,new Vector2(1,.91f));
        var panel=BistroWidgets.Box(shade,"Pantry panel",theme.background,new Vector2(.05f,.05f),new Vector2(.95f,.95f));
        BistroWidgets.Label(panel,"YOUR PANTRY",theme,new Vector2(.03f,.88f),new Vector2(.75f,.98f),26);
        BistroWidgets.Button(panel,"Close",theme,new Vector2(.81f,.90f),new Vector2(.97f,.97f),()=>Change(()=>modal=null));
        BistroWidgets.Label(panel,notice,theme,new Vector2(.04f,.845f),new Vector2(.96f,.89f),16);
        var grid=BistroWidgets.Grid(panel,theme,new Vector2(.03f,.23f),new Vector2(.97f,.84f));
        if(modal=="pantry"){game.Loop.NoteSeen(TutorialFlags.SeenPantry);PantryModal(panel,grid);}
    }
    // Pantry batches; at the market, drag one serving at a time into SELL, drag a row out of SELL to keep one, then confirm.
    private void PantryModal(RectTransform panel,RectTransform grid)
    {
        bool selling=game.Loop.CanSellPantry;
        if(!selling)sellSelections.Clear();
        // Drop selections whose batch was used, spoiled or shrank.
        foreach(var id in sellSelections.Keys.ToList())
        {
            var batch=game.State.inventory.Find(e=>string.Equals(e.inventoryEntryId,id,StringComparison.OrdinalIgnoreCase));
            if(batch==null||!MealAllocation.Usable(batch))sellSelections.Remove(id);else if(sellSelections[id]>batch.servings)sellSelections[id]=batch.servings;
        }
        var items=sellSelections.Select(p=>new PantrySaleItem{inventoryEntryId=p.Key,servings=p.Value}).ToList();
        var quote=items.Count==0?null:game.Loop.QuotePantrySale(items);
        foreach(var e in game.State.inventory)
        {
            sellSelections.TryGetValue(e.inventoryEntryId,out int pending);
            int left=e.servings-pending;
            float each=selling?game.Loop.PantryValuePerServing(e.inventoryEntryId):-1;
            string price=!selling?"":each>=0?$"\nSells for ${each:0.00} each":"\nCannot be sold";
            var pantryCard=BistroFoodCard.Create(grid,game.Queries.Foods.GetById(e.foodId),e.inventoryEntryId,"sell-batch",$"{left} servings · {Expiry(e.daysRemaining)}"+price,theme,selling&&each>=0&&left>0);
            pantryCard.ApplyShelfArtwork(game.Queries.Foods.GetById(e.foodId),Expiry(e.daysRemaining),left);
            string batchId=e.inventoryEntryId;
            if(selling&&each>=0&&left>0)RoundButton(pantryCard.FrontLayer,"+",new Vector2(.76f,.60f),new Vector2(.94f,.72f),true,()=>AddSellServing(batchId));
        }
        BistroWidgets.EmptyShelf(grid,"Your pantry is empty.\nPurchased food appears here as individual batches.",theme);
        if(!selling){BistroWidgets.Label(panel,"Pantry food can be sold at the market.",theme,new Vector2(.03f,.05f),new Vector2(.97f,.18f),18);return;}
        var sellSlot=BistroDropSlot.Create(panel,"SELL",items.Count==0?"Drag one serving at a time here":$"{items.Sum(i=>i.servings)} servings · ${(quote!=null&&quote.success?quote.amount:0):0.00}",theme,
            new Vector2(.03f,.025f),new Vector2(.55f,.21f),quote!=null&&!quote.success,(id,kind)=>{if(kind=="sell-batch")Change(()=>AddSellServing(id));},null);
        // Each row is one batch waiting to be sold; drag it out of the slot to keep one serving.
        sellSlot.ShowRows(sellSelections.Select(p=>new BistroDropSlot.Row{key=p.Key,label=$"{FoodName(p.Key)} ×{p.Value}",art=game.Queries.Foods.GetById(BatchFood(p.Key))?.icon}).ToList(),
            "unsell",id=>Change(()=>{if(sellSelections.TryGetValue(id,out int n)){if(n<=1)sellSelections.Remove(id);else sellSelections[id]=n-1;}}),theme);
        BistroWidgets.Button(panel,quote!=null&&quote.success?$"Sell for ${quote.amount:0.00}":"Sell",theme,new Vector2(.59f,.035f),new Vector2(.97f,.12f),()=>Change(()=>{
            var sale=game.Loop.SellPantry(items);
            if(sale.success){sellSelections.Clear();notice=$"Sold {sale.servingsSold} servings for ${sale.amount:0.00}.";}else notice=sale.error;}));
        if(quote!=null&&!quote.success)BistroWidgets.Label(panel,quote.error,theme,new Vector2(.59f,.13f),new Vector2(.97f,.21f),16);
    }
    private void AddSellServing(string entryId)
    {
        var batch=game.State.inventory.Find(e=>string.Equals(e.inventoryEntryId,entryId,StringComparison.OrdinalIgnoreCase));
        sellSelections.TryGetValue(entryId,out int n);
        if(batch==null||n>=batch.servings){notice="No more servings left in that batch.";return;}
        sellSelections[entryId]=n+1;notice="One serving added to SELL. Nothing is sold until you press Sell.";
    }
    private string BatchFood(string entryId)=>game.State.inventory.Find(e=>string.Equals(e.inventoryEntryId,entryId,StringComparison.OrdinalIgnoreCase))?.foodId;
    private string FoodName(string entryId){var id=BatchFood(entryId);return game.Queries.Foods.GetById(id)?.displayName??id;}
    private static string Expiry(int days)=>days<0?"no expiry":$"{days} days left";
}
