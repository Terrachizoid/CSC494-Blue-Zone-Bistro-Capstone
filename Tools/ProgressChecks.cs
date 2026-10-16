// Tests presentation state independently of health and one-time dialogue completion tutorial flags.
using System;
using System.Collections.Generic;
public static class ProgressChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static string Run()
    {
        var state=new GameSessionState();var balance=new BalanceConfig();
        var customer=new CustomerData{id="U",displayName="June",startingHealth=50};
        var customers=new CustomerDatabase();customers.SetItems(new List<CustomerData>{customer});
        var progress=CustomerState.Create(customer,balance);state.customers.Add(progress);
        int rolls=0;var visits=new CustomerVisitService(state,customers,balance,()=>{rolls++;return .99;});
        Check(visits.GetTodaysCustomers().Count==1 && rolls==1,"Empty day did not force customer");
        Check(visits.GetTodaysCustomers().Count==1 && rolls==1,"Forced attendance rerolled");
        progress.lastServedDay=1;Check(visits.GetTodaysCustomers().Count==0,"Forced customer attended twice");
        RushCrashProgress.Served(progress,1,true);
        Check(progress.visualScore==10 && progress.health==50 && progress.finance==20,"Rush changed actual stats");
        new DayService(state,balance,customers).FinishDay();
        Check(state.currentDay==2 && progress.visualScore==-15 && progress.crashDialoguePending && progress.health==50 && progress.finance==20,"Crash changed health or finance");
        new DayService(state,balance,customers).FinishDay();
        Check(progress.visualScore==0 && progress.crashDialoguePending,"Absent customer's pending dialogue lost");
        var database=new DialogueDatabase();
        var scheduler=new EncounterScheduler(state,database,customers,new RecipeDatabase());
        var trigger=scheduler.CustomerTrigger(DialogueTrigger.CustomerArrival,"U",null);
        Check(trigger==DialogueTrigger.CrashArrival,"Crash trigger not selected");
        Check(scheduler.Dialogue.FindNext(trigger,scheduler.Context("U"))==null && progress.crashDialoguePending,
            "Missing CSV dialogue generated a fallback or cleared pending crash");
        var sequence=new DialogueSequenceData{id="AUTHORED_CRASH",customer=customer,trigger=DialogueTrigger.CrashArrival,
            mode=DialogueMode.Mandatory,lines=new[]{new DialogueLineData{speakerId="U",textTemplate="Authored crash line."}}};
        database.SetItems(new List<DialogueSequenceData>{sequence});
        Check(scheduler.Dialogue.FindNext(trigger,scheduler.Context("U"))==sequence && !state.flags.HasFlag(TutorialFlags.CrashExplained),"Authored crash selection failed");
        Check(scheduler.Dialogue.FindNext(trigger,scheduler.Context("OTHER"))==null,"Borrowed another customer's special dialogue");
        scheduler.OnDialogueCompleted(sequence,"U");
        Check(!progress.crashDialoguePending && state.flags.HasFlag(TutorialFlags.CrashExplained),"Crash completion not recorded");
        Check(scheduler.CustomerTrigger(DialogueTrigger.CustomerArrival,"U",null)==DialogueTrigger.CustomerArrival,"Crash greeting repeated");
        var failed=new ServeResult{rushCrashTriggered=true,recipeBuilt=false};
        Check(scheduler.CustomerTrigger(DialogueTrigger.AfterService,"U",failed)==DialogueTrigger.RushAfterService,"Failed red meal lost rush trigger");
        Check(scheduler.Dialogue.FindNext(DialogueTrigger.RushAfterService,scheduler.Context("U",null,failed))==null,"Missing rush dialogue fabricated text");
        var completed=new GameSessionState{currentDay=balance.totalDays};
        Check(new DayService(completed,balance).FinishDay().gameCompleted && completed.gameCompleted,"Completion not persisted");
        return "PASS: cached forced attendance, rush/crash without health/finance mutation, delayed greeting and completion-only flags.";
    }
}

