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
    private readonly ShoppingTutorialBehaviour shopping=new ShoppingTutorialBehaviour();
    private readonly FirstMealTutorialBehaviour firstMeal=new FirstMealTutorialBehaviour();
    private readonly Dictionary<string,ITutorialBehaviour> behaviours;
    private MealPlanService planner;
    // The day's meal plan, used by the day-1 lessons (checkout needs everything planned; cook the planned dish).
    public MealPlanService Planner
    {
        get=>planner;
        set
        {
            planner=value;
            shopping.PlanMissing=planner==null?null:(System.Func<List<CartEntry>,string>)(cart=>{
                var report=planner.Report(cart,null);var missing=report.needs.Where(n=>n.Missing>0).ToList();
                return missing.Count==0?null:string.Join(", ",missing.Select(n=>$"{n.Missing} {IntroMealPlan.CategoryName(n.category)}"));});
            shopping.PlannedDishes=planner==null?null:(System.Func<int>)(()=>planner.Total);
        }
    }
    // A hard lesson (own behaviour class, not text) is running: the UI locks what its step does not allow.
    public bool HardActive=>Active!=null&&!(current is TextTutorialBehaviour);
    public string Step=>Record?.stepId;
    public bool PlanAffordable(string recipeId)=>plan.Affordable(recipeId);
    public TutorialDefinition Active {get;private set;}
    public TutorialRecord Record {get;private set;}
    public ServeResult LastMeal {get;private set;}
    public string LastError {get;private set;}
    private ITutorialBehaviour current;
    public bool IsText=>Active!=null && current.IsText(Record.stepId);
    public TutorialController(GameSessionState state,TutorialDatabase content,IntroMealPlan plan)
    {
        this.state=state;this.content=content;this.plan=plan;
        behaviours=new Dictionary<string,ITutorialBehaviour>{{"Shopping",shopping},{"FirstMeal",firstMeal},{"TextOnly",new TextTutorialBehaviour()}};
        if(state.tutorials==null)state.tutorials=new List<TutorialRecord>();
        state.flags.FlagSet+=FlagChanged;
        ObserveFlags();
    }
    public void Dispose()=>state.flags.FlagSet-=FlagChanged;
    private void FlagChanged(string id)
    {
        // One lesson at a time (Owen, 2026-11-18): while a hard lesson runs, a flag that would start a text lesson is
        // not kept. It is cleared, so that lesson shows the next time it happens instead of piling up behind this one.
        if(HardActive&&content!=null&&content.tutorials.Any(d=>d.behaviourId=="TextOnly"&&string.Equals(d.triggerFlag,id,StringComparison.OrdinalIgnoreCase)&&
            !state.tutorials.Any(r=>r.tutorialId==d.id))){state.flags.ClearFlag(id);return;}
        ObserveFlags();
    }
    // A game event a text lesson may react to (set by the loop or the UI through the loop).
    public void Note(string flag){if(!string.IsNullOrWhiteSpace(flag))state.flags.SetFlag(flag);}
    private void ObserveFlags()
    {
        if(content==null)return;
        foreach(var definition in content.tutorials)
            if(state.flags.HasFlag(definition.triggerFlag)&&!state.tutorials.Any(r=>r.tutorialId==definition.id))
                state.tutorials.Add(new TutorialRecord{tutorialId=definition.id,status="pending"});
    }
    // The current guest asks for Comfort today (the only request answered by appeal instead of the recipe).
    private bool WantsComfort(string customerId)
    {
        var guest=customerId==null?null:state.customers.FirstOrDefault(c=>string.Equals(c.customerId,customerId,StringComparison.OrdinalIgnoreCase));
        return guest!=null&&guest.cravingDay==state.currentDay&&state.currentDay>=plan.CravingsStartDay&&guest.activeCravings!=null&&guest.activeCravings.Contains(CravingTag.Comfort);
    }
    public void Sync(GameLoop loop)
    {
        if(state.gameCompleted)return;
        if(state.currentDay==1 && loop.Phase==GamePhase.Market&&!state.flags.HasFlag(TutorialFlags.Purchased))state.flags.SetFlag(TutorialFlags.ShoppingAvailable);
        if(state.currentDay==1 && loop.Phase==GamePhase.Market&&state.flags.HasFlag(TutorialFlags.Purchased))state.flags.SetFlag(TutorialFlags.BistroAvailable);
        if(loop.Phase==GamePhase.FoodSorting)state.flags.SetFlag(TutorialFlags.SortingAvailable);
        // State-derived flags let CSV lessons react to the economy without new behaviours.
        if(plan.RentPerDay>0&&state.currentDay==plan.RentStartDay-1)state.flags.SetFlag(TutorialFlags.RentUpcoming);
        if(state.money<0)state.flags.SetFlag(TutorialFlags.MoneyNegative);
        if(loop.Phase==GamePhase.Market&&new MarketEffects(state).AnyStoreClosed)state.flags.SetFlag(TutorialFlags.StoreClosedEvent);
        if(loop.Phase==GamePhase.Market&&state.currentDay>1&&plan.Candidates.Any(r=>r.unlockDay==state.currentDay))state.flags.SetFlag(TutorialFlags.NewRecipes);
        if(loop.Phase==GamePhase.Market&&state.inventory.Any(e=>MealAllocation.Usable(e)&&e.daysRemaining==1))state.flags.SetFlag(TutorialFlags.FoodExpiring);
        if(loop.Phase==GamePhase.Service&&loop.Service.step==ServiceStep.MealSelection&&WantsComfort(loop.Service.CurrentCustomerId))state.flags.SetFlag(TutorialFlags.ComfortRequest);
        // Money-low advice (sell pantry food) only makes sense at the market before today's checkout.
        bool checkedOutToday=state.shoppingReceipt!=null&&state.shoppingReceipt.day==state.currentDay;
        if(plan.RentPerDay>0&&state.currentDay>=plan.RentStartDay&&state.money<plan.RentPerDay&&loop.Phase==GamePhase.Market&&!checkedOutToday&&!loop.BistroOpened)state.flags.SetFlag(TutorialFlags.MoneyLow);
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
                .Replace("{comfort}",plan.ComfortMinimumAppeal.ToString("0.#")).Replace("{missedCraving}",plan.PopularityPerMissedCraving.ToString("0.#")).Replace("{maxDiscount}",plan.MaximumDiscount.ToString("P0")).Replace("{rent}",plan.RentPerDay.ToString("0.##")).Replace("{rentStart}",plan.RentStartDay.ToString())
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
    // Debug panel only: marks every lesson finished (with its completion flags) so play is never blocked.
    public void CompleteAll()
    {
        if(content==null)return;
        foreach(var definition in content.tutorials)
        {
            var record=state.tutorials.Find(r=>r.tutorialId==definition.id);
            if(record==null)state.tutorials.Add(record=new TutorialRecord{tutorialId=definition.id});
            if(record.status=="completed"||record.status=="skipped")continue;
            record.status="completed";
            foreach(string flag in definition.completionFlags??new string[0])state.flags.SetFlag(flag);
        }
        Active=null;Record=null;current=null;
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
