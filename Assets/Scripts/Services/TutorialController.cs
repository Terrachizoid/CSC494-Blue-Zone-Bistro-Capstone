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
        if(state.currentDay==1 && loop.Phase==GamePhase.Market&&!state.flags.HasFlag(TutorialFlags.Purchased))state.flags.SetFlag(TutorialFlags.ShoppingAvailable);
        if(loop.Phase==GamePhase.FoodSorting)state.flags.SetFlag(TutorialFlags.SortingAvailable);
        // State-derived flags let CSV lessons react to the economy without new behaviours.
        if(plan.RentPerDay>0&&state.currentDay==plan.RentStartDay-1)state.flags.SetFlag(TutorialFlags.RentUpcoming);
        if(state.money<0)state.flags.SetFlag(TutorialFlags.MoneyNegative);
        if(plan.RentPerDay>0&&state.currentDay>=plan.RentStartDay&&state.money<plan.RentPerDay)state.flags.SetFlag(TutorialFlags.MoneyLow);
        if(loop.Phase==GamePhase.Service&&loop.Service.step==ServiceStep.MealSelection&&!state.flags.HasFlag(TutorialFlags.Served))state.flags.SetFlag(TutorialFlags.CookingAvailable);
        ObserveFlags();
        if(Active!=null)
        {
            if(!current.Feasible(plan,Record.stepId))Skip("Tutorial requirements are no longer available or affordable.");
            // A lesson the player asked for (top-bar click) steps in front of an action lesson that is
            // waiting for the player to do something. The paused lesson keeps its step and resumes next.
            else if(!IsText&&ClickedLessonWaiting(loop)){Record.status="pending";Active=null;Record=null;current=null;}
            else return;
        }
        if(content==null)return;
        foreach(var definition in content.tutorials.OrderByDescending(d=>d.priority).ThenBy(d=>d.id,StringComparer.Ordinal))
        {
            var record=state.tutorials.Find(r=>r.tutorialId==definition.id);
            if(record==null || record.status=="completed"||record.status=="skipped")continue;
            // "Any" lessons (e.g. top-bar explanations) may open on whichever screen the player is on.
            if(definition.activationPhase!="Any"&&definition.activationPhase!=loop.Phase.ToString())continue;
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
            return FormatInstruction(text);
        }
    }
    private string FormatInstruction(string text)
    {
        return (text??"").Replace("{recipe}",plan.Recipe?.displayName??"recipe").Replace("{requirements}",plan.Requirements).Replace("{examples}",plan.Examples)
                .Replace("{comfort}",plan.ComfortMinimumAppeal.ToString("0.#")).Replace("{maxDiscount}",plan.MaximumDiscount.ToString("P0")).Replace("{rent}",plan.RentPerDay.ToString("0.##")).Replace("{rentStart}",plan.RentStartDay.ToString())
                .Replace("{totalDays}",plan.TotalDays.ToString()).Replace("{cravingsStart}",plan.CravingsStartDay.ToString()).Replace("{maxGuests}",plan.MaxGuests.ToString())
                .Replace("{guestThresholds}",plan.GuestThresholds).Replace("{walkInThreshold}",plan.WalkInThreshold.ToString("0")).Replace("{walkInPer100}",plan.WalkInPerHundred.ToString("0.00")).Replace("{sellRate}",plan.PantrySellRate.ToString("P0"))
                .Replace("{healthDelta}",LastMeal?.healthDelta.ToString()??"0")
                .Replace("{dailyDozen}",IntroMealPlan.ReadableList(LastMeal?.dailyDozenCovered??new DailyDozenCategory[0]))
                .Replace("{ingredients}",LastMeal==null?"":string.Join(", ",LastMeal.consumedIngredients
                    .GroupBy(i=>i.foodId).Select(g=>$"{plan.Foods.GetById(g.Key)?.displayName??g.Key} ×{g.Sum(i=>i.servingsUsed)}")));
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
    // The player clicked a top-bar item (day, popularity, wallet). Sets a flag a CSV lesson can trigger on;
    // each lesson still runs only once, so later clicks change nothing.
    public void NoteToolbarClick(string item)
    {
        if(string.IsNullOrWhiteSpace(item)||!item.All(char.IsLetter))return;
        state.flags.SetFlag(TutorialFlags.ToolbarPrefix+item.ToLowerInvariant());
    }
    private bool ClickedLessonWaiting(GameLoop loop)=>content!=null&&content.tutorials.Any(d=>
        d.triggerFlag!=null&&d.triggerFlag.StartsWith(TutorialFlags.ToolbarPrefix)&&
        (d.activationPhase=="Any"||d.activationPhase==loop.Phase.ToString())&&
        state.tutorials.Any(r=>r.tutorialId==d.id&&r.status=="pending"));
    // Action-step instructions open once by themselves; Help can always show them again.
    public bool InstructionSeen=>Record!=null&&Record.seenStepId==Record.stepId;
    public void MarkInstructionSeen(){if(Record!=null)Record.seenStepId=Record.stepId;}
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
