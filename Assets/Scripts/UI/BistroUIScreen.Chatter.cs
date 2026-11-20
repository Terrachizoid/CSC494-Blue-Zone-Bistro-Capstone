// Idle chatter bubble (2026-11-18): a small, non-blocking line from the guide under the Help button.
// Click lines: Help clicked with no lesson in progress. Idle lines: ~12 s without a click, key or scroll, once per screen
// visit, at most every 30 s, never over a lesson, a panel or a guest dialogue. IdleChatterService picks the line.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private string chatterText;private float chatterUntil,chatterShownAt,lastInputAt,lastIdleAt=-999f;
    private string chatterVisit;private bool idleShownThisVisit;
    private readonly System.Random chatterRandom=new System.Random();
    // Owen 2026-11-18: idle lines only after a long pause (35 s with no input), and rarely.
    private const float IdleAfterSeconds=35f,IdleCooldownSeconds=60f,ChatterFadeSeconds=.75f,ChatterFadeIn=.2f,ChatterCharsPerSecond=45f;
    private string ChatterPlace()
    {
        switch(game.Loop.Phase)
        {
            case GamePhase.Market:return store==null?"City":shelf==null?"Store":"Shelf";
            case GamePhase.FoodSorting:return "Sorting";
            case GamePhase.Service:return "Service";
            default:return "Results";
        }
    }
    // Picks and shows a line; false when none fits (the caller may fall back to something else).
    // Help can't be spammed: the next click line waits until the current one starts to fade.
    private bool ChatterBusy=>chatterText!=null&&Time.unscaledTime<chatterUntil-ChatterFadeSeconds;
    private bool ShowChatter(IdleKind kind)
    {
        if(game.Idle==null||game.IsMainMenu)return false;
        if(kind==IdleKind.Click&&ChatterBusy)return true; // still showing: the click is absorbed
        var report=game.Loop.Phase==GamePhase.Market||game.Loop.Phase==GamePhase.Service?PlanReport():null;
        var line=game.Idle.Pick(kind,ChatterPlace(),ChatterContext(report),chatterRandom);
        if(line==null)return false;
        string text=line.text.Replace("{money}","$"+game.State.money.ToString("0.00"));
        if(text.Contains("{food}")||text.Contains("{store}")||text.Contains("{aisle}")||text.Contains("{category}"))
        {
            // Navigation lines use the helper's suggestion for something the plan still needs.
            var need=report?.needs.Find(n=>n.Missing>0);var listing=need==null?null:game.Queries.SuggestListing(need.category,chatterRandom);
            if(listing==null)return false;
            string aisle=listing.store.shelves?.FirstOrDefaultShelf(listing.shelfId)??listing.shelfId;
            text=text.Replace("{category}",IntroMealPlan.CategoryName(need.category).ToLowerInvariant()).Replace("{food}",listing.food.displayName.ToLowerInvariant())
                .Replace("{store}",listing.store.displayName).Replace("{aisle}",aisle);
        }
        // Opaque time also covers the typing, so a long line is fully readable before it fades.
        chatterText=text;chatterShownAt=Time.unscaledTime;
        chatterUntil=chatterShownAt+text.Length/ChatterCharsPerSecond+line.DisplaySeconds+ChatterFadeSeconds;dirty=true;
        return true;
    }
    // Where the player is and what they are doing right now; the idle service's conditions read only this.
    private IdleContext ChatterContext(MealPlanReport report)
    {
        var phase=game.Loop.Phase;var c=new IdleContext{state=game.State,plan=report,totalDays=game.Balance.totalDays};
        if(phase==GamePhase.Market)
        {
            c.storeId=store?.id;c.shelfId=shelf;c.cartPackages=System.Linq.Enumerable.Sum(cart,e=>e.packageQuantity);
            var quote=cart.Count>0?game.Loop.QuoteCart(cart):null;c.cartTotal=quote!=null&&quote.success?quote.total:0;
        }
        if(phase==GamePhase.FoodSorting){c.sortTotal=System.Linq.Enumerable.Count(game.Loop.SortingQuestions);c.sortPlaced=answers.Count;c.lastSort=game.Loop.SortingResult;}
        if(phase==GamePhase.Service&&game.Loop.Service!=null)
        {
            var service=game.Loop.Service;c.customerId=service.CurrentCustomerId;c.mealRecipeId=mealRecipe?.id;
            if(c.customerId!=null)c.cravings=game.Queries.GetActiveCravings(c.customerId);
            c.canMakeAny=System.Linq.Enumerable.Any(game.Queries.GetUnlockedRecipes(),r=>game.Queries.CanMake(r));
            c.guestsLeft=service.encounters==null?0:System.Linq.Enumerable.Count(System.Linq.Enumerable.Skip(service.encounters,service.encounterIndex),e=>e.kind==EncounterKind.Customer);
            if(service.awaitingDishAcknowledgement)c.lastDish=service.lastDishResult;
        }
        return c;
    }
    private void TickChatter()
    {
        float now=Time.unscaledTime;
        bool input=(Pointer.current!=null&&Pointer.current.press.wasPressedThisFrame)||(Keyboard.current!=null&&Keyboard.current.anyKey.wasPressedThisFrame)||
            (Mouse.current!=null&&Mouse.current.scroll.ReadValue().sqrMagnitude>0);
        if(input||lastInputAt==0)lastInputAt=now;
        if(chatterText!=null&&now>chatterUntil){chatterText=null;dirty=true;}
        if(game.IsMainMenu||game.Loop.Phase==GamePhase.Completed)return;
        string visit=ChatterPlace()+"|"+store?.id+"|"+shelf;
        if(visit!=chatterVisit){chatterVisit=visit;idleShownThisVisit=false;}
        if(chatterText!=null||idleShownThisVisit||now-lastInputAt<IdleAfterSeconds||now-lastIdleAt<IdleCooldownSeconds)return;
        if(modal!=null||game.Loop.Tutorials?.Active!=null||game.Loop.Dialogue?.IsActive==true)return;
        if(ShowChatter(IdleKind.Idle)){idleShownThisVisit=true;lastIdleAt=now;}
    }
    private void ChatterBubble()
    {
        if(chatterText==null||dialogueRequests.Count>0)return;
        // A soft card in the game's cream, with a green accent strip and a small "Guide" label; no hard outline.
        var bubble=BistroShelfStyle.Panel(screen,"Guide chatter",new Color(.98f,.965f,.93f,.96f),new Vector2(.70f,.785f),new Vector2(.995f,.902f));
        BistroShelfStyle.ApplySurface(bubble,theme.guideNotePanel);
        bubble.gameObject.AddComponent<CanvasGroup>();bubble.gameObject.AddComponent<BistroFade>().Setup(chatterShownAt,ChatterFadeIn,chatterUntil,ChatterFadeSeconds);
        var shadow=bubble.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.16f);shadow.effectDistance=new Vector2(0,-3);
        if(theme.guideNotePanel==null)BistroShelfStyle.Panel(bubble,"Accent",new Color(BistroShelfStyle.Green.r,BistroShelfStyle.Green.g,BistroShelfStyle.Green.b,.85f),new Vector2(.015f,.14f),new Vector2(.03f,.86f));
        BistroShelfStyle.Text(bubble,"Guide",theme,new Vector2(.06f,.70f),new Vector2(.95f,.95f),14,BistroShelfStyle.Green,true);
        var line=BistroShelfStyle.Text(bubble,chatterText,theme,new Vector2(.06f,.06f),new Vector2(.95f,.72f),16,BistroShelfStyle.Ink);
        line.alignment=TextAnchor.UpperLeft;
        if(!theme.reducedMotion)BistroTextMotion.Typewriter(line,chatterText,chatterShownAt,ChatterCharsPerSecond,false);
        foreach(var graphic in bubble.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
    }
}

static class BistroShelfLookup
{
    public static string FirstOrDefaultShelf(this StoreShelfDefinition[] shelves,string id)
    {
        if(shelves==null)return null;
        foreach(var s in shelves)if(s!=null&&s.id==id)return s.displayName;
        return null;
    }
}
