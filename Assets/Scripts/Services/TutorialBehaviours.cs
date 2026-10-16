// Tutorial behaviours own step semantics; the controller owns scheduling and persistent outcomes.
using System.Collections.Generic;
public enum TutorialAction { Checkout, SelectRecipe, Serve, SkipCustomer, ContinueDish, BeginService, SubmitSorting, AdvanceDialogue, FinishDay }
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
    public bool CanActivate(GameLoop loop)=>true;
    public bool Feasible(IntroMealPlan plan,string step)=>true;
    public bool IsText(string step)=>true;
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)=>"Read the tutorial and choose Continue first.";
    public string Next(string step,TutorialAction action)=>step;
    public string Continue(string step)=>null;
}
public class ShoppingTutorialBehaviour : ITutorialBehaviour
{
    public string FirstStep=>"shop";
    public bool CanActivate(GameLoop loop)=>true;
    public bool Feasible(IntroMealPlan plan,string step)=>plan.ShoppingFeasible();
    public bool IsText(string step)=>false;
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)
        =>action==TutorialAction.Checkout&&!plan.CartReady(cart)?"Your first meal needs "+plan.Requirements+". Add enough ingredients before checkout.":null;
    public string Next(string step,TutorialAction action)=>action==TutorialAction.Checkout?null:step;
    public string Continue(string step)=>step;
}
public class FirstMealTutorialBehaviour : ITutorialBehaviour
{
    public string FirstStep=>"choose";
    public bool CanActivate(GameLoop loop)=>loop.Service.step==ServiceStep.MealSelection && loop.Dialogue?.IsActive!=true;
    public bool Feasible(IntroMealPlan plan,string step)=>step=="feedback" || plan.PantryReady();
    public bool IsText(string step)=>step=="feedback";
    public string Check(string step,TutorialAction action,IntroMealPlan plan,List<CartEntry> cart,string recipeId,ServeResult preview)
    {
        if(IsText(step))return "Read the meal explanation and choose Continue first.";
        if(action==TutorialAction.SkipCustomer||action==TutorialAction.ContinueDish||action==TutorialAction.FinishDay)return "Complete your first meal before moving on.";
        if(action==TutorialAction.SelectRecipe && recipeId!=IntroMealPlan.RecipeId)return "Choose "+plan.Recipe.displayName+" for this first lesson.";
        if(action==TutorialAction.Serve && step=="choose")return "Choose the introductory recipe before serving.";
        if(action==TutorialAction.Serve && (recipeId!=IntroMealPlan.RecipeId||preview==null||!preview.success||!preview.recipeBuilt))return "For this lesson, build the introductory recipe successfully before serving. Review its slot warnings.";
        return null;
    }
    public string Next(string step,TutorialAction action)=>action==TutorialAction.SelectRecipe?"build":action==TutorialAction.Serve?"feedback":step;
    public string Continue(string step)=>step=="feedback"?null:step;
}
