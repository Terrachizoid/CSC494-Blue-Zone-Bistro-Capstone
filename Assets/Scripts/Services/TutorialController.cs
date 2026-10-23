// Flag-driven scheduler. Steps/locks live in behaviours; definitions/text are imported content.
// Persistent completion/skips live in the shared session. UI references never enter this layer.
using System;
using System.Collections.Generic;
using System.Linq;
public class TutorialController : IDisposable
{
    private readonly GameSessionState state;
    private readonly TutorialDatabase content;
    private readonly IntroMealPlan plan;
    private readonly Dictionary<string,ITutorialBehaviour> behaviours=new Dictionary<string,ITutorialBehaviour>{
        {"Shopping",new ShoppingTutorialBehaviour()},{"FirstMeal",new FirstMealTutorialBehaviour()},{"TextOnly",new TextTutorialBehaviour()}};
    public TutorialDefinition Active {get;private set;}
    public TutorialRecord Record {get;private set;}
    public ServeResult LastMeal {get;private set;}
    public string LastError {get;private set;}
    private ITutorialBehaviour current;
    public bool IsText=>Active!=null && current.IsText(Record.stepId);
    public TutorialController(GameSessionState state,TutorialDatabase content,IntroMealPlan plan)
    {
        this.state=state;this.content=content;this.plan=plan;
        if(state.tutorials==null)state.tutorials=new List<TutorialRecord>();
        state.flags.FlagSet+=FlagChanged;
        ObserveFlags();
    }
    public void Dispose()=>state.flags.FlagSet-=FlagChanged;
    private void FlagChanged(string id)=>ObserveFlags();
    private void ObserveFlags()
    {
        if(content==null)return;
        foreach(var definition in content.tutorials)
            if(state.flags.HasFlag(definition.triggerFlag)&&!state.tutorials.Any(r=>r.tutorialId==definition.id))
                state.tutorials.Add(new TutorialRecord{tutorialId=definition.id,status="pending"});
    }
    public void Sync(GameLoop loop)
    {
        if(state.gameCompleted)return;
        if(state.currentDay==1 && loop.Phase==GamePhase.Market&&!state.flags.HasFlag(TutorialFlags.Purchased))state.flags.SetFlag("tutorial.shopping_available");
        if(loop.Phase==GamePhase.FoodSorting)state.flags.SetFlag("tutorial.sorting_available");
        if(loop.Phase==GamePhase.Service&&loop.Service.step==ServiceStep.MealSelection&&!state.flags.HasFlag(TutorialFlags.Served))state.flags.SetFlag("tutorial.cooking_available");
        ObserveFlags();
        if(Active!=null)
        {
            if(!current.Feasible(plan,Record.stepId))Skip("Tutorial requirements are no longer available or affordable.");
            else return;
        }
        if(content==null)return;
        foreach(var definition in content.tutorials.OrderByDescending(d=>d.priority).ThenBy(d=>d.id,StringComparer.Ordinal))
        {
            var record=state.tutorials.Find(r=>r.tutorialId==definition.id);
            if(record==null || record.status=="completed"||record.status=="skipped")continue;
            if(definition.activationPhase!=loop.Phase.ToString())continue;
            if(behaviours.TryGetValue(definition.behaviourId,out var candidate) && !candidate.CanActivate(loop))continue;
            Active=definition;Record=record;
            if(!behaviours.TryGetValue(definition.behaviourId,out current)){Skip("Unknown tutorial behaviour.");continue;}
            Record.status="active";Record.stepId=string.IsNullOrEmpty(Record.stepId)?current.FirstStep:Record.stepId;
            if(!current.Feasible(plan,Record.stepId)){Skip("Tutorial requirements are unavailable or unaffordable.");continue;}
            if(!HasText()){Skip("Missing tutorial instruction text.");continue;}
            return;
        }
    }
    private bool HasText()=>Active.steps.Any(s=>s.stepId==Record.stepId&&!string.IsNullOrWhiteSpace(s.text));
    public string Instruction
    {
        get
        {
            if(Active==null)return "";
            var text=Active.steps.Find(s=>s.stepId==Record.stepId)?.text??"";
            return text.Replace("{recipe}",plan.Recipe?.displayName??"recipe").Replace("{requirements}",plan.Requirements)
                .Replace("{maxDiscount}",plan.MaximumDiscount.ToString("P0")).Replace("{rent}",plan.RentPerDay.ToString("0.##"))
                .Replace("{healthDelta}",LastMeal?.healthDelta.ToString()??"0")
                .Replace("{dailyDozen}",LastMeal==null||LastMeal.dailyDozenCovered.Length==0?"none":string.Join(", ",LastMeal.dailyDozenCovered))
                .Replace("{ingredients}",LastMeal==null?"":string.Join(", ",LastMeal.consumedIngredients
                    .GroupBy(i=>i.foodId).Select(g=>$"{plan.Foods.GetById(g.Key)?.displayName??g.Key} ×{g.Sum(i=>i.servingsUsed)}")));
        }
    }
    public bool Allow(TutorialAction action,List<CartEntry> cart=null,string recipeId=null,ServeResult preview=null)
    {
        LastError=Active==null?null:current.Check(Record.stepId,action,plan,cart,recipeId,preview);
        return LastError==null;
    }
    public void Succeeded(TutorialAction action,ServeResult meal=null)
    {
        if(meal!=null)LastMeal=meal;
        if(Active==null)return;
        Move(current.Next(Record.stepId,action));
    }
    public bool ContinueText()
    {
        if(!IsText)return false;Move(current.Continue(Record.stepId));return true;
    }
    private void Move(string next)
    {
        if(next==null)
        {
            var flags=Active.completionFlags;Record.status="completed";Active=null;Record=null;current=null;
            foreach(string flag in flags)state.flags.SetFlag(flag);
        }
        else {Record.stepId=next;if(!HasText())Skip("Missing tutorial step text.");}
    }
    private void Skip(string reason)
    {
        Record.status="skipped";Record.skipReason=reason;
        UnityEngine.Debug.LogWarning($"Tutorial {Active.id} skipped: {reason}");
        Active=null;Record=null;current=null;
    }
}
