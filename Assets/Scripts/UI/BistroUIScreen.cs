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
    private StoreData store;
    private string shelf, notice="", modal;
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
    private void PhaseChanged(GamePhase phase){ResetMeal();store=null;shelf=null;modal=null;answers.Clear();notice="";dirty=true;}
    private void LateUpdate(){TickEventToast();if(!game.IsMainMenu)SyncVoice();if(dirty){dirty=false;Refresh();}}
    private void OnDestroy(){if(subscribedLoop!=null)subscribedLoop.PhaseChanged-=PhaseChanged;if(ownsTheme)Destroy(theme);}
    private void Change(Action action){action();dirty=true;}
    private void Refresh()
    {
        canvas.gameObject.SetActive(true);
        RememberScroll();
        RememberCardFaces();
        if(screen!=null){screen.gameObject.SetActive(false);Destroy(screen.gameObject);}
        screen=BistroWidgets.Box(canvas.transform,"Current screen",theme.background,Vector2.zero,Vector2.one);
        if(game.IsMainMenu){MainMenu();AnimateNavigation();return;}
        if(game.Loop.Phase==GamePhase.Completed){EndingScreen();AnimateNavigation();return;}
        game.Loop.RefreshTutorials();
        BistroWidgets.Label(screen,$"BLUE ZONE BISTRO / DAY {game.State.currentDay} / ${game.State.money:0.00}\nPopularity {game.State.popularity:0} {PublicityLabel()}",theme,new Vector2(.03f,.91f),new Vector2(.51f,.99f),23);
        BistroWidgets.Button(screen,"Menu",theme,new Vector2(.65f,.925f),new Vector2(.78f,.98f),()=>Change(()=>modal="main-menu"));
        BistroWidgets.Button(screen,"Pantry",theme,new Vector2(.80f,.925f),new Vector2(.96f,.98f),()=>Change(()=>modal="pantry"));
        BistroWidgets.Label(screen,game.SaveFailed?game.SaveMessage:notice,theme,new Vector2(.03f,0),new Vector2(.97f,.065f),18);
        if(game.Loop.Phase==GamePhase.Market)Market();else if(game.Loop.Phase==GamePhase.FoodSorting)Sorting();else if(game.Loop.Phase==GamePhase.Service)ServiceScreen();else ResultsScreen();
        if(game.SaveFailed)BistroWidgets.Button(screen,"Retry checkpoint save",theme,new Vector2(.72f,.005f),new Vector2(.98f,.06f),()=>Change(()=>game.RetrySave()));
        BistroWidgets.Button(screen,"News",theme,new Vector2(.52f,.925f),new Vector2(.63f,.98f),()=>Change(()=>modal="news"));
        PresentTutorial();
        if(modal=="news"||modal=="event-detail")EventPanel();else if(modal=="main-menu")ReturnMenuPanel();else if(modal!=null && modal!="tutorial-help")Modal();
        RestoreScroll();
        RestoreCardFaces();
        AnimateNavigation();
    }
    private void Market()
    {
        if(store==null)
        {
            CityMap();
        }
        else
        {
            BistroWidgets.Button(screen,shelf==null?"← City":"← Store shelves",theme,new Vector2(.03f,.82f),new Vector2(.23f,.89f),()=>Change(()=>{if(shelf!=null)shelf=null;else store=null;}));
            BistroWidgets.Label(screen,store.displayName+(shelf==null?"":" / "+shelf),theme,new Vector2(.26f,.82f),new Vector2(.96f,.89f),25);
            var listings=game.Queries.GetListings(store);
            if(shelf==null)
            {
                var interior=BistroWidgets.Box(screen,"Store interior",theme.panel,new Vector2(.03f,.20f),new Vector2(.97f,.79f));
                if(store.shopBackground!=null){interior.GetComponent<Image>().sprite=store.shopBackground;interior.GetComponent<Image>().color=Color.white;}
                var shelves=listings.Select(l=>string.IsNullOrEmpty(l.shelfId)?"All products":l.shelfId).Distinct().ToList();
                for(int i=0;i<shelves.Count;i++)
                {string id=shelves[i];var definition=store.shelves.FirstOrDefault(s=>s!=null&&s.id==id);float x=.04f+(i%3)*.32f,y=.64f-(i/3)*.25f;
                    BistroWidgets.Button(interior,definition?.displayName??id,theme,new Vector2(x,y),new Vector2(x+.28f,y+.19f),()=>Change(()=>shelf=id));}
            }
            else
            {
                var grid=BistroWidgets.Grid(screen,theme,new Vector2(.03f,.20f),new Vector2(.73f,.79f));
                foreach(var listing in listings.Where(l=>(string.IsNullOrEmpty(l.shelfId)?"All products":l.shelfId)==shelf))
                    BistroFoodCard.Create(grid,listing.food,listing.id,"offer",$"{game.Queries.GetPrice(listing).Display} · {listing.servingsPerPackage} servings\n{Expiry(game.Balance.GetShelfLife(listing.food.storageType))}",theme);
                BistroWidgets.EmptyShelf(grid,"No products are available on this shelf.",theme);
                var quote=game.Loop.QuoteCart(cart);
                BistroDropSlot.Create(screen,$"CART · ${quote.total:0.00}",CartText(),theme,new Vector2(.76f,.28f),new Vector2(.97f,.77f),quote.success&&quote.total>game.State.money,AddToCart,()=>Change(()=>modal="cart"),cart.Select(c=>$"{game.Queries.GetListing(c.listingId)?.food.displayName} ×{c.packageQuantity}").ToArray());
            }
        }
        BistroWidgets.Button(screen,$"Inspect cart · {cart.Sum(c=>c.packageQuantity)} packages",theme,new Vector2(.03f,.085f),new Vector2(.40f,.16f),()=>Change(()=>modal="cart"));
        BistroWidgets.Button(screen,$"Today's guests · {game.State.dailyCustomerIds.Count}",theme,new Vector2(.42f,.085f),new Vector2(.62f,.16f),()=>Change(()=>modal="guests"));
        BistroWidgets.Button(screen,"Review / checkout",theme,new Vector2(.64f,.085f),new Vector2(.97f,.16f),()=>Change(()=>modal="cart"));
    }
    private string CartText(){var quote=game.Loop.QuoteCart(cart);return $"{cart.Sum(c=>c.packageQuantity)} packages\n"+(quote.success?$"${quote.total:0.00}":quote.error)+"\n\n"+string.Join("\n",cart.Take(4).Select(c=>$"{game.Queries.GetListing(c.listingId)?.food.displayName} ×{c.packageQuantity}"));}
    private void AddToCart(string id,string kind)
    {
        if(kind!="offer")return;
        Change(()=>{var entry=cart.Find(c=>c.listingId==id);if(entry==null)cart.Add(new CartEntry{listingId=id});else if(entry.packageQuantity<10000)entry.packageQuantity++;notice="Package added to your cart.";});
    }
    private void Sorting()
    {
        var result=game.Loop.SortingResult;
        if(result!=null)
        {
            BistroWidgets.Label(screen,$"{result.correctCount}/{result.questionCount} correct · refund ${result.refundAmount:0.00}",theme,new Vector2(.04f,.79f),new Vector2(.96f,.89f),30);
            var feedback=string.Join("\n\n",result.feedback.Select(f=>$"{game.Queries.Foods.GetById(f.foodId)?.displayName}: {(f.isCorrect?"Correct":"Needs another look")}\n{f.explanation}"));
            BistroWidgets.ReadingPanel(screen,feedback,theme,new Vector2(.08f,.23f),new Vector2(.92f,.77f));
            BistroWidgets.Button(screen,"Continue to service",theme,new Vector2(.60f,.09f),new Vector2(.95f,.18f),()=>Change(()=>game.Loop.BeginService()));return;
        }
        BistroWidgets.Label(screen,"Sort today's foods · click to flip · drag to place",theme,new Vector2(.03f,.82f),new Vector2(.97f,.90f),25);
        var grid=BistroWidgets.Grid(screen,theme,new Vector2(.03f,.29f),new Vector2(.61f,.80f));
        foreach(var q in game.Loop.SortingQuestions.Where(q=>!answers.ContainsKey(q.foodId)))
        {
            var batches=game.State.inventory.Where(e=>game.State.shoppingReceipt.batchIds.Contains(e.inventoryEntryId)&&e.foodId==q.foodId).ToList();
            int earliest=batches.Where(e=>e.daysRemaining>=0).Select(e=>e.daysRemaining).DefaultIfEmpty(-1).Min();
            BistroFoodCard.Create(grid,game.Queries.Foods.GetById(q.foodId),q.foodId,"sort",$"{batches.Sum(e=>e.servings)} purchased servings\n{Expiry(earliest)}",theme);
        }
        var colors=new[]{TrafficLight.Green,TrafficLight.Yellow,TrafficLight.Red};
        BistroWidgets.EmptyShelf(grid,"All foods placed.\nOpen a slot to move a card, or submit your answers.",theme);
        for(int i=0;i<3;i++)
        {
            var answer=colors[i];float y=.57f-i*.23f;
            var text=string.Join("\n",answers.Where(p=>p.Value==answer).Select(p=>game.Queries.Foods.GetById(p.Key)?.displayName));
            BistroDropSlot.Create(screen,answer.ToString().ToUpperInvariant(),text,theme,new Vector2(.65f,y),new Vector2(.97f,y+.21f),false,
                (id,kind)=>{if(kind=="sort")Change(()=>answers[id]=answer);},()=>Change(()=>modal="sort:"+answer),answers.Where(p=>p.Value==answer).Select(p=>game.Queries.Foods.GetById(p.Key)?.displayName).ToArray());
        }
        BistroWidgets.Button(screen,"Submit answers",theme,new Vector2(.06f,.12f),new Vector2(.55f,.22f),()=>Change(()=>{
            var resultSubmit=game.Loop.SubmitSorting(answers.Select(p=>new SortingChoice{foodId=p.Key,answer=p.Value}).ToList());notice=resultSubmit.success?"":resultSubmit.error;}));
    }
    private void Modal()
    {
        var shade=BistroWidgets.Box(screen,"Modal blocker",new Color(0,0,0,.70f),Vector2.zero,Vector2.one);
        var panel=BistroWidgets.Box(shade,"Slot contents "+modal,theme.background,new Vector2(.05f,.05f),new Vector2(.95f,.95f));
        BistroWidgets.Label(panel,modal=="guests"?"TODAY'S EXPECTED GUESTS":modal=="pantry"?"PANTRY · individual purchase batches":modal=="cart"?"SHOPPING CART":modal=="meal-preview"?"REVIEW DISH":modal.Substring(5)+" SLOT",theme,new Vector2(.03f,.88f),new Vector2(.75f,.98f),26);
        BistroWidgets.Button(panel,"Close",theme,new Vector2(.81f,.90f),new Vector2(.97f,.97f),()=>Change(()=>modal=null));
        BistroWidgets.Label(panel,notice,theme,new Vector2(.04f,.845f),new Vector2(.96f,.89f),16);
        if(modal=="guests")
        {
            string guests=string.Join("\n\n",game.Queries.GetExpectedCustomers().Select(c=>{
                var cravings=game.Queries.GetActiveCravings(c.id);
                return c.displayName+"\n"+(cravings.Length==0?"No specific request today.":"Craving: "+string.Join(", ",cravings));}));
            BistroWidgets.ReadingPanel(panel,$"Morning popularity: {game.State.morningPopularity:0} · Up to {game.State.dailyCustomerCapacity} customers\nThese guests and requests stay fixed for today.\n\n"+guests,theme,new Vector2(.05f,.12f),new Vector2(.95f,.83f));return;
        }
        if(modal.StartsWith("meal")){MealModal(panel);return;}
        var grid=BistroWidgets.Grid(panel,theme,new Vector2(.03f,.23f),new Vector2(.97f,.84f));
        if(modal=="pantry")
        {
            foreach(var e in game.State.inventory)
                BistroFoodCard.Create(grid,game.Queries.Foods.GetById(e.foodId),e.inventoryEntryId,"batch",$"{e.servings} servings · {Expiry(e.daysRemaining)}\nOffer: {e.listingId}\nBatch: {e.inventoryEntryId.Substring(0,Math.Min(8,e.inventoryEntryId.Length))}",theme,false);
            BistroWidgets.EmptyShelf(grid,"Your pantry is empty.\nPurchased food appears here as individual batches.",theme);
        }
        else
        {
            bool isCart=modal=="cart";
            if(isCart)foreach(var c in cart)
            {var listing=game.Queries.GetListing(c.listingId);BistroFoodCard.Create(grid,listing.food,c.listingId,"remove-cart",$"{c.packageQuantity} packages · ${game.Queries.GetPrice(listing).effectivePrice*c.packageQuantity:0.00}\nDrag out to remove one",theme);}
            else foreach(var p in answers.Where(p=>"sort:"+p.Value==modal))
                BistroFoodCard.Create(grid,game.Queries.Foods.GetById(p.Key),p.Key,"remove-sort","Drag out to return",theme);
            BistroWidgets.EmptyShelf(grid,isCart?"Your cart is empty.\nOpen a store shelf and drag a food card into CART.":"This slot is empty.\nClose this panel to place a food card.",theme);
            BistroDropSlot.Create(panel,"RETURN / REMOVE",isCart?"Drop a card here to remove one package":"Drop a card here to return it to your hand",theme,new Vector2(.03f,.025f),new Vector2(.55f,.21f),false,
                (id,kind)=>Change(()=>{if(kind=="remove-cart"){var item=cart.Find(c=>c.listingId==id);if(item!=null&&--item.packageQuantity==0)cart.Remove(item);}else if(kind=="remove-sort")answers.Remove(id);}),null);
            if(isCart)
            {
                var quote=game.Loop.QuoteCart(cart);BistroWidgets.Label(panel,quote.success?$"Total ${quote.total:0.00} / Budget ${game.State.money:0.00}":quote.error,theme,new Vector2(.58f,.13f),new Vector2(.97f,.22f),20);
                BistroWidgets.Button(panel,"Confirm checkout",theme,new Vector2(.59f,.035f),new Vector2(.97f,.12f),()=>Change(()=>{
                    var outcome=game.Loop.Checkout(cart);if(outcome.success){cart.Clear();modal=null;}notice=outcome.success?"Checkout complete":outcome.error;}));
            }
        }
    }
    private static string Expiry(int days)=>days<0?"no expiry":$"{days} days left";
}
