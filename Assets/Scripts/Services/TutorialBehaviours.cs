// Tutorial behaviours own step semantics; the controller owns scheduling and persistent outcomes.
using System.Collections.Generic;
public enum TutorialAction { Checkout, SelectRecipe, Serve, SkipCustomer, ContinueDish, BeginService, SubmitSorting, AdvanceDialogue, FinishDay, OpenRecipes, PlanDish, UnplanDish, CloseRecipes, EnterStore, OpenShelf }
public interface ITutorialBehaviour
{
    string FirstStep {get;}
    bool CanActivate(GameLoop loop);
    bool Feasible(IntroMealPlan plan,string step);
    bool IsText(string step);
    string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview);
    string Next(string step,TutorialAction action);
    string Continue(string step);
}
public class TextTutorialBehaviour : ITutorialBehaviour
{
    public string FirstStep=>"explain";
    // Safe moment: never over a guest's dialogue or a dish result (no double dialogues); the lesson waits.
    public bool CanActivate(GameLoop loop)=>loop.Dialogue?.IsActive!=true&&!(loop.Phase==GamePhase.Service&&loop.Service.awaitingDishAcknowledgement);
    public bool Feasible(IntroMealPlan plan,string step)=>true;
    public bool IsText(string step)=>true;
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)=>"Finish reading the guide first.";
    public string Next(string step,TutorialAction action)=>step;
    public string Continue(string step)=>null;
}
// Day-1 lesson 1 (2026-11-18): open Recipes, plan exactly one affordable dish, close the book, then buy everything the
// plan needs. Ends at the first successful checkout; the player is free until the bistro opens (lesson 2, FirstMeal).
// Steps advance on the player's action. The UI greys whatever the current step does not allow.
public class ShoppingTutorialBehaviour : ITutorialBehaviour
{
    // Set by the controller from the meal plan: what the plan still needs given this cart (null = nothing missing),
    // and how many dishes are planned.
    public System.Func<List<CartEntry>,string> PlanMissing;
    public System.Func<int> PlannedDishes;
    public string FirstStep=>"recipes";
    public bool CanActivate(GameLoop loop)=>true;
    // After planning: "shop" (go to a store) → "store" (inside: aisles) → "shelf" (add to cart, check out).
    public bool Feasible(IntroMealPlan plan,string step)=>step=="shop"||step=="store"||step=="shelf"||plan.ShoppingFeasible();
    public bool IsText(string step)=>false;
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)
    {
        if(action==TutorialAction.BeginService)return "Finish your first shopping trip first: buy everything your planned dish needs.";
        switch(step)
        {
            case "recipes":
                return action==TutorialAction.OpenRecipes?null:"Open your recipe book first (Recipes, in the top bar).";
            case "plan":
            case "close":
                // Inside the book the player may flip cards and plan/unplan freely; one dish, then Close.
                int planned=PlannedDishes?.Invoke()??0;
                if(action==TutorialAction.PlanDish)
                    return planned>0?"One dish is enough for your first day. Press − on it if you'd like to swap.":
                        plan.Affordable(recipeId)?null:"That dish costs more than you have today. Pick one that isn't greyed out.";
                if(action==TutorialAction.UnplanDish||action==TutorialAction.OpenRecipes)return null;
                if(action==TutorialAction.CloseRecipes)return planned>0?null:"Plan one dish first: press + on its card. Then close the book.";
                return planned>0?"Close the recipe book to go shopping.":"Plan one dish first: press + on its card.";
            default: // shop, store, shelf
                if(action==TutorialAction.PlanDish||action==TutorialAction.UnplanDish)return "Your plan is set for this first trip. You can change it after your first purchase.";
                if(action==TutorialAction.Checkout)
                {
                    string missing=PlanMissing?.Invoke(cart);
                    if(missing!=null)return "For your first checkout, let's buy everything your dish needs. Still missing: "+missing+
                        ". Tip: click a food type in the plan panel to see where to find it.";
                    if(PlanMissing==null&&!plan.CartReady(cart))return "Your cart and pantry can't make any recipe on today's menu yet.";
                }
                return null;
        }
    }
    public string Next(string step,TutorialAction action)
    {
        if(step=="recipes"&&action==TutorialAction.OpenRecipes)return "plan";
        if(step=="plan"&&action==TutorialAction.PlanDish)return "close";
        if(step=="close"&&action==TutorialAction.UnplanDish&&(PlannedDishes?.Invoke()??0)==0)return "plan";
        if(step=="close"&&action==TutorialAction.CloseRecipes)return "shop";
        if(step=="shop"&&action==TutorialAction.EnterStore)return "store";
        if(step=="store"&&action==TutorialAction.OpenShelf)return "shelf";
        if((step=="shop"||step=="store"||step=="shelf")&&action==TutorialAction.Checkout)return null;
        return step;
    }
    public string Continue(string step)=>step;
}
public class FirstMealTutorialBehaviour : ITutorialBehaviour
{
    // Any complete dish passes the first-meal lesson, not only the planned one (Owen, 2026-11-19).
    public string FirstStep=>"choose";
    public bool CanActivate(GameLoop loop)=>loop.Service.step==ServiceStep.MealSelection && loop.Dialogue?.IsActive!=true;
    public bool Feasible(IntroMealPlan plan,string step)=>step=="feedback" || plan.PantryReady();
    public bool IsText(string step)=>step=="feedback";
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)
    {
        if(IsText(step))return "Finish reading the guide first.";
        if(action==TutorialAction.SkipCustomer||action==TutorialAction.ContinueDish||action==TutorialAction.FinishDay)return "Complete your first meal before moving on.";
        // Any recipe on today's menu is accepted; the lesson only requires a successful build.
        if(action==TutorialAction.SelectRecipe && !plan.IsCandidate(recipeId))return "Choose a recipe from today's menu for this first lesson.";
        // Faded cards can't be completed with the pantry; the first guest should get a full dish (Owen, 2026-11-19).
        if(action==TutorialAction.SelectRecipe && !plan.CanMakeNow(recipeId))return "You don't have everything for that dish yet. For your first guest, pick a dish you can fully make: one of the cards that isn't faded.";
        if(action==TutorialAction.Serve && step=="choose")return "Choose a recipe before serving.";
        if(action==TutorialAction.Serve && (!plan.IsCandidate(recipeId)||preview==null||!preview.success||!preview.recipeBuilt))return "For your first dish, fill every slot exactly. Slots with a red outline still need something.";
        return null;
    }
    public string Next(string step,TutorialAction action)=>action==TutorialAction.SelectRecipe?"build":action==TutorialAction.Serve?"feedback":step;
    public string Continue(string step)=>step=="feedback"?null:step;
}
