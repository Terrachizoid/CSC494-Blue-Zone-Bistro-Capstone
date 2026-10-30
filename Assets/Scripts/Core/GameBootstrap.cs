// File responsibility: Composition root: creates one shared session service graph and connects the greybox UI.
// Editor setup assigns default assets; player builds use serialized references, never editor file paths.

using UnityEngine;

// The editor fills missing references from the standard imported asset locations.
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private FoodDatabase foods;
    [SerializeField] private StoreListingDatabase listings;
    [SerializeField] private RecipeDatabase recipes;
    [SerializeField] private CustomerDatabase customers;
    [SerializeField] private DialogueDatabase dialogues;
    [SerializeField] private EndingDatabase endings;
    [SerializeField] private EventDatabase events;
    [SerializeField] private BalanceConfig balance;
    [SerializeField] private BistroUITheme uiTheme;
    [SerializeField] private TutorialDatabase tutorials;
    [SerializeField] private GameSessionState state = new GameSessionState();

    public GameSessionState State => state;
    public BalanceConfig Balance => balance;
    public GameLoop Loop { get; private set; }
    public GameContentQueries Queries { get; private set; }
    public bool IsMainMenu { get; private set; } = true;
    public string SaveMessage { get; private set; }
    public bool SaveFailed { get; private set; }
    public bool HasCheckpoint => saves.Exists;
    private readonly CheckpointStore saves = new CheckpointStore();
    private string pendingCheckpoint;
    private EncounterScheduler scheduler;
    private TutorialController tutorialController;
    private void OnDestroy() { tutorialController?.Dispose(); }
    public string ResumeLabel
    {
        get { return saves.TryLoad(out var saved, out _) ? saved.gameCompleted ? "View completed run" : $"Resume — Day {saved.currentDay} start" : "Resume (no readable checkpoint)"; }
    }
    public void ReturnToMenu() { IsMainMenu = true; }
    public bool NewGame()
    {
        var fresh = new GameSessionState { money = balance.startingMoney };
        PrepareMorning(fresh);
        string snapshot = saves.Capture(fresh);
        if (!saves.Save(snapshot, out var error)) { SaveMessage = error; return false; }
        state = fresh; pendingCheckpoint = snapshot; SaveFailed = false; SaveMessage = "Day 1 checkpoint saved.";
        BuildSession(); IsMainMenu = false; return true;
    }
    public bool Resume()
    {
        if (!saves.TryLoad(out var restored, out var message)) { SaveMessage = message; return false; }
        if (restored.currentDay > balance.totalDays) { SaveMessage = "Saved day exceeds the current game configuration."; return false; }
        foreach (var entry in restored.inventory) if (foods.GetById(entry.foodId) == null)
        { SaveMessage = "Saved food is missing from the current content. Import all CSV content before resuming."; return false; }
        foreach (var customer in restored.customers) if (customers.GetById(customer.customerId) == null)
        { SaveMessage = "A saved customer is missing from the current content."; return false; }
        bool migrated=restored.eventPlanDay!=restored.currentDay || restored.communityStateVersion==0 || (!restored.gameCompleted && restored.attendancePlanDay!=restored.currentDay);
        if(restored.eventPlanDay==0 && restored.attendancePlanDay==restored.currentDay)restored.eventPlanDay=restored.currentDay;
        PrepareMorning(restored);
        if(restored.gameCompleted&&string.IsNullOrEmpty(restored.endingId)){new EndingService(restored,endings).Complete();migrated=true;}
        if(migrated && !saves.Save(saves.Capture(restored),out var migrationError))
        {SaveMessage="Could not save the migrated day plan: "+migrationError;return false;}
        state = restored; pendingCheckpoint = saves.Capture(state); SaveFailed = false; SaveMessage = message;
        BuildSession(); IsMainMenu = false; return true;
    }
    private void CaptureCheckpoint()
    {
        if (state.gameCompleted) state.completedDaySummary = Loop.Summary;
        pendingCheckpoint = saves.Capture(state); RetrySave();
    }
    public void RetrySave()
    {
        if (pendingCheckpoint == null) return;
        SaveFailed = !saves.Save(pendingCheckpoint, out var error);
        SaveMessage = SaveFailed ? error : state.gameCompleted ? "Completed run saved." : $"Day {state.currentDay} start saved.";
    }


    private void Awake()
    {
        if (foods == null || listings == null || recipes == null || customers == null || balance == null || dialogues == null)
        {
            Debug.LogError("GameBootstrap is missing content. Run Tools > Blue Zone Bistro > Import All CSV Content. " +
                "Defaults are connected automatically; inspect the Console for import errors if references remain missing.", this);
            enabled = false;
            return;
        }
        if(!CommunityProgress.Valid(balance))
        {
            Debug.LogError("Invalid popularity, craving or wellbeing settings in BalanceConfig. Check ranges and attendance thresholds.",this);
            enabled=false;return;
        }
        BuildSession();
        var cards = GetComponent<BistroUIScreen>();
        if (cards == null) cards = gameObject.AddComponent<BistroUIScreen>();
        cards.Initialize(this, uiTheme);
    }
    private void BuildSession()
    {
        tutorialController?.Dispose();
        foods.RebuildMinimumPrices(listings.Listings);
        scheduler = new EncounterScheduler(state, dialogues, customers, recipes);
        Loop = new GameLoop(new PurchaseService(state, listings, balance),
            new MealService(state, foods, recipes, customers, balance), new DayService(state, balance, customers),
            new CustomerVisitService(state, customers, balance, recipes:recipes, foods:foods, listings:listings), scheduler,
            new FoodSortingService(state, foods), state.gameCompleted,new EndingService(state,endings),new EventService(state,events,balance,listings),
            new PantryService(state, foods, balance), new FoodAidService(state, foods, recipes, balance));
        Loop.DayFinished += CaptureCheckpoint;
        tutorialController=new TutorialController(state,tutorials,new IntroMealPlan(state,foods,recipes,listings,balance));
        Loop.AttachTutorials(tutorialController);
        Queries = new GameContentQueries(state, foods, listings, recipes, customers,endings,events);
    }
    private void PrepareMorning(GameSessionState session)
    {
        CommunityProgress.Initialize(session,balance);
        new EventService(session,events,balance,listings).PrepareMorning();
        if(!session.gameCompleted)new CustomerVisitService(session,customers,balance,recipes:recipes,foods:foods,listings:listings).GetTodaysCustomers();
    }
}
