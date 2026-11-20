// "Today's plan": pinned recipes (+/- on recipe cards) and a small live panel, top left, showing what the plan
// still needs. MealPlanService does the counting; this file only presents its report. Rebuilt with the screen,
// so cart, pantry and serving changes show immediately.
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private bool planCollapsed;
    // ---- Day-1 lesson 1 (Shopping behaviour) presentation: greyed locks and "why not" dialogues. ----
    private string lessonReason,lessonReasonStep;
    private void ShowLessonNote(string text){lessonReason=text;lessonReasonStep=game.Loop.Tutorials?.Step;}
    private bool ShopLesson(string step=null)=>game.Loop.Tutorials?.Active?.behaviourId=="Shopping"&&(step==null||game.Loop.Tutorials.Step==step);
    // Greyed and unresponsive (supervisor: grey / half transparent means "not clickable").
    private static void LockIf(bool locked,Transform target)
    {
        if(!locked||target==null)return;
        var group=target.GetComponent<CanvasGroup>();if(group==null)group=target.gameObject.AddComponent<CanvasGroup>();group.alpha=.4f;group.interactable=false;
    }
    // A hard lesson refused the action: explain why in a dialogue (other errors stay in the notice line).
    private void Refused(string error)
    {
        var lessons=game.Loop.Tutorials;
        if(lessons!=null&&lessons.HardActive&&!string.IsNullOrEmpty(error)&&error==lessons.LastError)ShowLessonNote(error);else notice=error??"";
    }
    // Before shopping starts in lesson 1, clicks elsewhere still respond, with the Guide saying what to do first.
    private bool LessonHolds()=>ShopLesson("recipes")||ShopLesson("plan")||ShopLesson("close");
    private void LessonRefuse()=>ShowLessonNote(ShopLesson("recipes")?"Let's plan first: open your recipe book (Recipes, in the top bar).":
        ShopLesson("plan")?"Plan one dish first: press + on its card.":"Close the recipe book to go shopping.");
    private System.Action Gate(System.Action action)=>()=>{if(LessonHolds())LessonRefuse();else action();};
    // Lesson 1 follows where the player is: entering a store, then opening a shelf (any route, including the plan hint).
    private void TrackShopLesson()
    {
        if(game.Loop.Phase!=GamePhase.Market)return;
        if(store!=null&&ShopLesson("shop"))game.Loop.TellTutorial(TutorialAction.EnterStore);
        if(store!=null&&shelf!=null&&ShopLesson("store"))game.Loop.TellTutorial(TutorialAction.OpenShelf);
    }
    // Small round +/− buttons: every drag action also has a click (supervisor, 2026-11-18).
    private Button RoundButton(Transform parent,string label,Vector2 min,Vector2 max,bool add,System.Action action)
    {
        var button=BistroShelfStyle.Button(parent,label,theme,min,max,add?BistroShelfStyle.Green:new Color(.84f,.83f,.77f),add?BistroShelfStyle.Cream:BistroShelfStyle.Ink,()=>Change(action),true);
        button.transform.SetAsLastSibling();return button;
    }
    // A blocked click always says why: in a hard lesson the Guide explains, otherwise the notice line does.
    private void Explain(string reason)
    {
        if(string.IsNullOrEmpty(reason))return;
        if(game.Loop.Tutorials?.HardActive==true)ShowLessonNote(reason);else notice=reason;
    }
    private void LessonOverlays()
    {
        // Once the dish is planned, the book closing by any route (Close, Help, menu) counts as closing it.
        if(ShopLesson("close")&&modal!="recipes"&&game.Loop.AskTutorial(TutorialAction.CloseRecipes))game.Loop.TellTutorial(TutorialAction.CloseRecipes);
        // A note belongs to the step it was shown in; once the lesson moves on it is stale and dropped.
        if(lessonReason!=null&&(game.Loop.Tutorials?.HardActive!=true||game.Loop.Tutorials.Step!=lessonReasonStep))lessonReason=null;
        if(lessonReason!=null)DialoguePanel("Guide",lessonReason,"Got it",()=>lessonReason=null);
    }
    // Navigation helper: clicking a category tile suggests one food and where to find it, for a few seconds.
    private StoreListingData planHint;private float planHintUntil;private readonly System.Random planRandom=new System.Random();
    private const float PlanHintSeconds=3f;
    private void TickPlanHint(){if(planHint!=null&&Time.unscaledTime>planHintUntil){planHint=null;dirty=true;}}
    private void ShowPlanHint(FoodCategory category)
    {
        var listing=game.Queries.SuggestListing(category,planRandom,planHint?.id);
        if(listing==null){notice="No open store sells "+IntroMealPlan.CategoryName(category)+" today.";return;}
        planHint=listing;planHintUntil=Time.unscaledTime+PlanHintSeconds;
    }
    private void GoToPlanHint(StoreListingData listing)
    {
        planHint=null;
        if(game.Loop.Phase!=GamePhase.Market||listing?.store==null||game.Queries.Market.ClosureReason(listing.store.id)!=null)return;
        modal=null;store=listing.store;shelf=listing.shelfId;
    }
    private MealPlanReport PlanReport()=>game.Planner.Report(game.Loop.Phase==GamePhase.Market?cart:null,game.Loop.Summary.dishes.Select(d=>d.recipeId));
    // Recipe book header and the +/- control on one card.
    private void PlanHeader(RectTransform panel)
    {
        int total=game.Planner.Total,limit=game.Planner.Limit;
        string text=limit==0?"Plan today's dishes once your guests are known":$"Today's plan: {total} of {limit} dish{(limit==1?"":"es")} (one per guest)";
        if(ShopLesson())text=total==0?"First day: plan one dish to cook":"First day: your dish is planned. Close the book to go shopping (− to swap).";
        BistroShelfStyle.Text(panel,text,theme,new Vector2(.04f,.825f),new Vector2(.62f,.86f),17,BistroShelfStyle.Ink);
        if(total>0&&!ShopLesson())BistroShelfStyle.Button(panel,"Clear plan",theme,new Vector2(.64f,.826f),new Vector2(.80f,.858f),new Color(.84f,.83f,.77f),BistroShelfStyle.Ink,()=>Change(()=>game.Planner.Clear()));
    }
    private void PlanControl(BistroRecipeCard card,RecipeData recipe)
    {
        int count=game.Planner.Planned(recipe.id);
        // Lesson 1: inside the book the player may flip and plan/unplan freely (one dish; the lesson explains refusals).
        // Dishes they can't afford yet are greyed. After the first purchase the plan is fixed until the lesson ends.
        bool lesson=ShopLesson(),choosing=ShopLesson("plan")||ShopLesson("close"),affordable=!lesson||game.Loop.Tutorials.PlanAffordable(recipe.id);
        if(choosing&&!affordable){var faded=card.gameObject.GetComponent<CanvasGroup>();if(faded==null)faded=card.gameObject.AddComponent<CanvasGroup>();faded.alpha=.45f;}
        var pill=BistroShelfStyle.Panel(card.FrontLayer,"Plan control",count>0?new Color(.77f,.86f,.70f,.96f):new Color(.95f,.93f,.87f,.94f),new Vector2(.60f,.895f),new Vector2(.975f,.985f));
        var minus=BistroShelfStyle.Button(pill,"−",theme,new Vector2(.03f,.1f),new Vector2(.30f,.9f),new Color(.84f,.83f,.77f),BistroShelfStyle.Ink,()=>Change(()=>PlanRemove(recipe.id)));
        minus.interactable=count>0&&(!lesson||choosing);
        BistroShelfStyle.Text(pill,count==0?"Plan":"×"+count,theme,new Vector2(.30f,0),new Vector2(.70f,1),17,BistroShelfStyle.Ink,true,TextAnchor.MiddleCenter);
        var plus=BistroShelfStyle.Button(pill,"+",theme,new Vector2(.70f,.1f),new Vector2(.97f,.9f),BistroShelfStyle.Green,BistroShelfStyle.Cream,()=>Change(()=>PlanAdd(recipe.id)));
        plus.interactable=game.Planner.Total<game.Planner.Limit&&(!lesson||choosing&&affordable);
        pill.SetAsLastSibling();
        if(NewRecipe(recipe))NewBadge(card.FrontLayer,new Vector2(.04f,.90f),new Vector2(.22f,.975f));
    }
    // "New" markers (supervisor): foods never bought and dishes never served.
    private bool NewFood(string foodId)=>!(game.State.boughtFoodIds?.Contains(foodId)??false)&&!game.State.correctlySortedFoodIds.Contains(foodId)&&
        !game.State.inventory.Any(e=>e.foodId==foodId);
    private bool NewRecipe(RecipeData recipe)=>!game.State.recipeServes.Any(r=>r!=null&&string.Equals(r.recipeId,recipe.id,System.StringComparison.OrdinalIgnoreCase));
    private void NewBadge(Transform parent,Vector2 min,Vector2 max)
    {
        var badge=BistroShelfStyle.Panel(parent,"New marker",new Color(.86f,.36f,.29f,.96f),min,max);
        BistroShelfStyle.ApplySurface(badge,theme.newTag);
        BistroShelfStyle.Text(badge,"New",theme,Vector2.zero,Vector2.one,14,new Color(.99f,.97f,.93f),true,TextAnchor.MiddleCenter);
        foreach(var g in badge.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
        badge.SetAsLastSibling();
    }
    private void PlanRemove(string recipeId)
    {
        if(!ShopLesson()){game.Planner.Remove(recipeId);return;}
        if(!game.Loop.AskTutorial(TutorialAction.UnplanDish,recipeId)){Refused(game.Loop.Tutorials.LastError);return;}
        if(game.Planner.Remove(recipeId))game.Loop.TellTutorial(TutorialAction.UnplanDish);
    }
    private void PlanAdd(string recipeId)
    {
        if(!ShopLesson()){game.Planner.Add(recipeId);return;}
        if(!game.Loop.AskTutorial(TutorialAction.PlanDish,recipeId)){Refused(game.Loop.Tutorials.LastError);return;}
        if(game.Planner.Add(recipeId))game.Loop.TellTutorial(TutorialAction.PlanDish);
    }
    // The floating panel: drag it anywhere (it stays where it was left), click the header to collapse it.
    // Required servings only, as category tiles like the back of a recipe card: icon, name, have/need.
    private Vector2 planOffset;
    private void MealPlanPanel()
    {
        if(game.Loop.Phase!=GamePhase.Market&&game.Loop.Phase!=GamePhase.Service)return;
        var report=PlanReport();if(report.Empty)return;
        var ink=BistroShelfStyle.Ink;var muted=new Color(.30f,.36f,.33f);
        var needs=report.needs.Where(n=>n.required>0).ToList();
        const int perRow=4;const float head=.042f,tileRow=.10f,line=.028f,pad=.01f,top=.905f,left=.005f,right=.24f;
        int tileRows=(needs.Count+perRow-1)/perRow;
        float body=planCollapsed?0:pad+tileRows*tileRow+report.dishes.Count*line+pad+(report.Remaining==0?line:0);
        float bottom=top-head-body;
        var panel=BistroShelfStyle.Panel(screen,"Today's plan",new Color(.97f,.95f,.90f,.96f),new Vector2(left,bottom),new Vector2(right,top));
        BistroShelfStyle.ApplySurface(panel,planCollapsed?theme.panelPaper:theme.planPanel);
        var shadow=panel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.18f);shadow.effectDistance=new Vector2(1,-2);
        var drag=panel.gameObject.AddComponent<BistroDragWindow>();drag.Moved=offset=>planOffset=offset;drag.topLimit=.91f; // never under the top bar, where it could not be grabbed again
        float h=top-bottom;
        string status=report.Remaining==0?"all served":report.Ready?"ready":$"{report.Missing} missing";
        var header=BistroShelfStyle.Button(panel,"",theme,new Vector2(0,1-head/h),Vector2.one,Color.clear,ink,()=>Change(()=>planCollapsed=!planCollapsed));
        BistroShelfStyle.Text(header.transform,"Today's plan · "+status+(planCollapsed?"  (show)":"  (hide)"),theme,new Vector2(.05f,0),new Vector2(.97f,1),17,report.Ready?BistroShelfStyle.Green:ink,true);
        if(!planCollapsed)
        {
            float y=head+pad;
            for(int i=0;i<needs.Count;i++)
            {
                var need=needs[i];int col=i%perRow,row=i/perRow;float w=.92f/perRow;
                float y0=y+row*tileRow;
                var tile=BistroShelfStyle.Panel(panel,"Need tile",need.Missing>0?new Color(.98f,.88f,.84f):new Color(.90f,.95f,.86f),
                    new Vector2(.04f+col*w+.006f,1-(y0+tileRow-.006f)/h),new Vector2(.04f+(col+1)*w-.006f,1-(y0+.004f)/h));
                var icon=BistroWidgets.Box(tile,"Category icon",Color.white,new Vector2(.12f,.44f),new Vector2(.88f,.97f));
                var image=icon.GetComponent<Image>();image.sprite=game.Queries.Foods.CategoryIcon(need.category);image.preserveAspect=true;image.raycastTarget=false;
                if(image.sprite==null)icon.gameObject.SetActive(false);
                var category=need.category;var tileButton=tile.gameObject.AddComponent<Button>();tileButton.targetGraphic=tile.GetComponent<Image>();
                tileButton.onClick.AddListener(()=>Change(()=>ShowPlanHint(category)));
                BistroShelfStyle.Text(tile,IntroMealPlan.CategoryName(need.category),theme,new Vector2(.02f,.24f),new Vector2(.98f,.44f),12,muted,false,TextAnchor.MiddleCenter);
                BistroShelfStyle.Text(tile,$"{System.Math.Min(need.have,need.required)}/{need.required}",theme,new Vector2(.02f,.01f),new Vector2(.98f,.25f),16,need.Missing>0?theme.warning:BistroShelfStyle.Green,true,TextAnchor.MiddleCenter);
            }
            y+=tileRows*tileRow+pad;
            foreach(var dish in report.dishes)
            {
                string text=$"{dish.recipe.displayName} ×{dish.planned}"+(dish.served==0?"":dish.Remaining==0?"  · all served":$"  · {dish.served} served");
                BistroShelfStyle.Text(panel,text,theme,new Vector2(.05f,1-(y+line)/h),new Vector2(.95f,1-y/h),14,dish.Remaining==0?muted:ink);y+=line;
            }
            if(report.Remaining==0)BistroShelfStyle.Text(panel,"Every planned dish is served.",theme,new Vector2(.05f,1-(y+line)/h),new Vector2(.95f,1-y/h),14,muted);
        }
        foreach(var text in panel.GetComponentsInChildren<Text>())text.raycastTarget=false;
        foreach(var tileImage in panel.GetComponentsInChildren<Image>())if(tileImage.transform!=panel&&tileImage.transform!=header.transform&&tileImage.name!="Need tile")tileImage.raycastTarget=false;
        drag.Place(planOffset);
        if(!planCollapsed&&planHint!=null)PlanHintCard(panel);
    }
    // The suggestion: a look-only food card with store and aisle. Clicking it (at the market) goes there.
    private void PlanHintCard(RectTransform panel)
    {
        var listing=planHint;var price=game.Queries.GetPrice(listing);
        // Beside the panel, on whichever side has room.
        bool right=panel.TransformPoint(panel.rect.max).x<screen.TransformPoint(screen.rect.center+new Vector2(screen.rect.width*.25f,0)).x;
        var box=BistroShelfStyle.Panel(panel,"Plan hint",new Color(.99f,.97f,.92f,.98f),Vector2.zero,Vector2.zero);
        BistroShelfStyle.ApplySurface(box,theme.panelPaper);
        box.gameObject.AddComponent<CanvasGroup>();box.gameObject.AddComponent<BistroFade>().Setup(float.NegativeInfinity,0,planHintUntil,.75f); // full for ~2.25 s, then fades out
        box.anchorMin=box.anchorMax=new Vector2(right?1:0,1);box.pivot=new Vector2(right?0:1,1);
        box.sizeDelta=new Vector2(210,350);box.anchoredPosition=new Vector2(right?8:-8,0);
        var shadow=box.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.22f);shadow.effectDistance=new Vector2(1,-2);
        var ink=BistroShelfStyle.Ink;
        BistroShelfStyle.Text(box,"Try this",theme,new Vector2(.06f,.91f),new Vector2(.94f,.99f),15,new Color(.30f,.36f,.33f),false,TextAnchor.MiddleCenter);
        var holder=BistroWidgets.Box(box,"Hint card",Color.clear,new Vector2(.08f,.30f),new Vector2(.92f,.91f));
        var frame=BistroShelfStyle.Artwork(box,"Suggestion frame",theme.suggestionFrame,new Vector2(.055f,.285f),new Vector2(.945f,.925f),Color.white);
        BistroShelfStyle.ApplySurface(frame,theme.suggestionFrame);
        var card=BistroFoodCard.Create(holder,listing.food,listing.id,"hint","",theme,false);
        card.ApplyShelfArtwork(listing.food,"$"+price.effectivePrice.ToString("0.00"),listing.servingsPerPackage,listing.store.logo);
        var cardRect=(RectTransform)card.transform;cardRect.anchorMin=Vector2.zero;cardRect.anchorMax=Vector2.one;cardRect.offsetMin=cardRect.offsetMax=Vector2.zero;
        foreach(var layout in card.GetComponents<LayoutElement>())layout.ignoreLayout=true;
        foreach(var graphic in card.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        card.enabled=false; // look only: no flip, no drag
        string aisle=listing.store.shelves?.FirstOrDefault(s=>s!=null&&s.id==listing.shelfId)?.displayName??listing.shelfId;
        BistroShelfStyle.Text(box,listing.store.displayName,theme,new Vector2(.06f,.18f),new Vector2(.94f,.29f),16,ink,true,TextAnchor.MiddleCenter);
        BistroShelfStyle.Text(box,aisle,theme,new Vector2(.06f,.09f),new Vector2(.94f,.18f),14,ink,false,TextAnchor.MiddleCenter);
        bool canGo=game.Loop.Phase==GamePhase.Market;
        BistroShelfStyle.Text(box,canGo?"Click to go there":"Find it at the market",theme,new Vector2(.06f,.01f),new Vector2(.94f,.09f),13,new Color(.30f,.36f,.33f),false,TextAnchor.MiddleCenter);
        foreach(var text in box.GetComponentsInChildren<Text>())text.raycastTarget=false;
        if(canGo){var go=box.gameObject.AddComponent<Button>();go.targetGraphic=box.GetComponent<Image>();go.onClick.AddListener(()=>Change(()=>GoToPlanHint(listing)));}
        else box.GetComponent<Image>().raycastTarget=false;
    }
}
