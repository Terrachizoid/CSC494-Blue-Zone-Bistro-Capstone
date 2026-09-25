// State-machine regressions with explicit story/customer fixtures and deterministic service doubles.
// Covers shared flags/history, event timing, legal commands, and dialogue completion ordering.
using System;
using System.Collections.Generic;

public static class DialogueChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private class Visits : ICustomerVisitService
    {
        public List<string> ids = new List<string> { "A", "B" };
        public List<string> GetTodaysCustomers() => new List<string>(ids);
    }
    private class Meals : IMealService
    {
        public int commits;
        public ServeResult Preview(ServeInput input) => new ServeResult { success = true, recipeBuilt = false, delightDelta = -30 };
        public ServeResult Serve(ServeInput input) { commits++; return Preview(input); }
    }
    private class Purchases : IPurchaseService { public PurchaseResult Buy(string id) => new PurchaseResult { success = true }; }
    private static DialogueSequenceData Sequence(string id, DialogueTrigger trigger, CustomerData customer = null)
    {
        return new DialogueSequenceData { id = id, trigger = trigger, customer = customer,
            mode = DialogueMode.Mandatory, repeatRule = DialogueRepeatRule.Once,
            lines = new[] { new DialogueLineData { lineOrder = 2, textTemplate = "Last {currentDay}" },
                new DialogueLineData { lineOrder = 1, textTemplate = "First" } } };
    }
    public static string Run()
    {
        var state = new GameSessionState { currentDay = 4 };
        var customers = new CustomerDatabase(); var a = new CustomerData { id = "A", displayName = "Ada" };
        customers.SetItems(new List<CustomerData> { a, new CustomerData { id = "B" } });
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData> { new RecipeData { id = "R" } });
        var chef = Sequence("chef", DialogueTrigger.ServiceEncounter); chef.priority = 1000;
        chef.setFlags = new[] { "chef_introduced" }; chef.minDay = 4;
        var story = Sequence("story", DialogueTrigger.ServiceEncounter); story.priority = 10;
        var arrival = Sequence("arrival", DialogueTrigger.CustomerArrival, a);
        var before = Sequence("before", DialogueTrigger.BeforeService, a);
        before.requiredFlags = new[] { "arrival_complete" }; arrival.setFlags = before.requiredFlags;
        var failed = Sequence("failed", DialogueTrigger.AfterService, a);
        failed.recipeBuildCondition = RecipeBuildCondition.Failed;
        var success = Sequence("success", DialogueTrigger.AfterService, a);
        success.recipeBuildCondition = RecipeBuildCondition.Built; success.priority = 999;
        var empty = Sequence("empty", DialogueTrigger.ServiceEncounter); empty.lines = new DialogueLineData[0]; empty.priority = 9999;
        var db = new DialogueDatabase(); db.SetItems(new List<DialogueSequenceData> { story, chef, arrival, before, failed, success, empty });
        var scheduler = new EncounterScheduler(state, db, customers, recipes);
        var meals = new Meals(); var visits = new Visits();
        var loop = new GameLoop(new Purchases(), meals, new DayService(state, new BalanceConfig()), visits, scheduler);
        int serviceEvents = 0;
        loop.PhaseChanged += phase =>
        {
            if (phase != GamePhase.Service) return;
            serviceEvents++;
            Check(loop.Service.step == ServiceStep.StoryDialogue && loop.Dialogue.IsActive,
                "Phase observer saw an unprepared service encounter");
        };
        Check(loop.BeginService() && loop.Dialogue.Sequence == chef, "Chef not first");
        Check(serviceEvents == 1, "Service event missing or duplicated");
        Check(loop.Dialogue.CurrentText == "First" && loop.Dialogue.CurrentLine.voiceOver == null, "Line sorting or silent voice failed");
        Check(!loop.SkipCustomer() && !loop.FinishDay().success && !loop.ShowResults(), "Story bypass allowed");
        Check(!loop.Serve(new ServeInput { customerId = "A" }).success && meals.commits == 0, "Serving during dialogue allowed");
        loop.AdvanceDialogue();
        Check(!state.flags.HasFlag("chef_introduced") && loop.Dialogue.CurrentText == "Last 4", "Effects applied early or token failed");
        loop.AdvanceDialogue();
        Check(state.flags.HasFlag("chef_introduced") && !state.dialogueHistory.CanPlay(chef, 5) && loop.Dialogue.Sequence == story,
            "Shared effects/history or next story failed");
        loop.AdvanceDialogue(); loop.AdvanceDialogue();
        Check(loop.Dialogue.Sequence == arrival, "Customer arrival missing");
        loop.AdvanceDialogue(); loop.AdvanceDialogue();
        Check(loop.Dialogue.Sequence == before, "BeforeService did not read arrival flag");
        loop.AdvanceDialogue(); loop.AdvanceDialogue();
        Check(loop.Service.step == ServiceStep.MealSelection && !loop.Dialogue.IsActive && !loop.AdvanceDialogue(), "Opening completion failed");
        loop.PreviewServe(new ServeInput { customerId = "A", recipeId = "R" });
        Check(!loop.Dialogue.IsActive && meals.commits == 0, "Preview started reaction");
        loop.Serve(new ServeInput { customerId = "A", recipeId = "R" });
        Check(!loop.Dialogue.IsActive && loop.Service.step == ServiceStep.DishResult, "Dish result skipped");
        loop.ContinueAfterDish();
        Check(loop.Dialogue.Sequence == failed && !loop.SkipCustomer(), "Failed dish selected success or could bypass reaction");
        loop.AdvanceDialogue(); loop.AdvanceDialogue();
        Check(loop.Service.CurrentCustomerId == "B" && loop.Service.step == ServiceStep.MealSelection, "No-dialogue customer got stuck");
        loop.SkipCustomer();
        Check(loop.Phase == GamePhase.Results && loop.Summary.skippedCustomerIds.Count == 1 && loop.Summary.dishes.Count == 1,
            "Skip produced a dish or did not reach Results");
        loop.FinishDay(); visits.ids.Clear();
        loop.BeginService();
        Check(loop.Phase == GamePhase.Results, "Once-only stories replayed");
        Check(serviceEvents == 1, "Empty queue emitted a stale Service event");

        var fresh = new GameSessionState { currentDay = 4 };
        var onlyChef = new DialogueDatabase(); onlyChef.SetItems(new List<DialogueSequenceData> { chef });
        loop = new GameLoop(new Purchases(), meals, new DayService(fresh, new BalanceConfig()), visits,
            new EncounterScheduler(fresh, onlyChef, customers, recipes));
        loop.BeginService();
        Check(loop.Dialogue.IsActive, "No regular customers suppressed chef encounter");
        loop.AdvanceDialogue(); loop.AdvanceDialogue();
        Check(loop.Phase == GamePhase.Results && fresh.flags.HasFlag("chef_introduced"), "Story-only day did not finish");
        var chefCustomer = new CustomerData { id = "Chef", customerType = CustomerType.VisitingChef };
        customers.SetItems(new List<CustomerData> { a, chefCustomer });
        var attendance = new CustomerVisitService(new GameSessionState(), customers, new BalanceConfig(), () => 0);
        Check(attendance.GetTodaysCustomers().Count == 1 && attendance.GetTodaysCustomers()[0] == "A",
            "Visiting chef incorrectly rolled as a regular meal customer");
        return "PASS: chef priority, shared flags/history, ordered lines, dialogue guards, before/after reactions, skips, empty queues, silent audio, and one-time completion.";
    }
}
