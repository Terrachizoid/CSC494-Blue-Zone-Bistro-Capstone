// File responsibility: Coordinates Market, encounters, dish acknowledgement, and Results in response to UI actions.
// Owns flow only; services calculate outcomes and mutate session data. Unity supplies the event loop.

using System;
using System.Collections.Generic;

// Phase orchestration is separate from operations and presentation.
public class GameLoop
{
    private readonly IPurchaseService purchases;
    private readonly IMealService meals;
    private readonly IDayService days;
    private readonly ICustomerVisitService visits;
    private readonly EndingService endings;
    private readonly EventService events;
    private readonly EncounterScheduler encounters;
    private readonly FoodSortingService sorting;
    private readonly PantryService pantry;
    private readonly FoodAidService foodAid;
    public SortingResult SortingResult { get; private set; }
    public List<SortingQuestion> SortingQuestions => sorting?.GetQuestions() ?? new List<SortingQuestion>();
    public DialoguePlaybackController Dialogue { get; }
    public ServiceSessionState Service { get; private set; } = new ServiceSessionState();
    public DaySummary Summary { get; private set; } = new DaySummary();
    // Phase is where the player is (city/market, sorting, bistro, results). Service progress lives in Service and
    // survives a trip back to the city (easy mode, BalanceConfig.cityDuringService; 2026-11-13).
    public GamePhase Phase { get; private set; } = GamePhase.Market;
    public bool BistroOpened { get; private set; } // Service has started today; the queue continues on return.
    public event Action<GamePhase> PhaseChanged;
    public event Action DayFinished;
    public TutorialController Tutorials { get; private set; }
    public void AttachTutorials(TutorialController controller) { Tutorials=controller; RefreshTutorials(); }
    public void RefreshTutorials() => Tutorials?.Sync(this);
    private bool TutorialAllows(TutorialAction action, List<CartEntry> cart=null, string recipeId=null, ServeResult preview=null)
    { RefreshTutorials(); return Tutorials==null || Tutorials.Allow(action,cart,recipeId,preview); }
    public OperationResult SelectRecipe(string recipeId)
    {
        if(Phase!=GamePhase.Service || Service.step!=ServiceStep.MealSelection)
            return new OperationResult {error="Choose a recipe during meal selection."};
        if(!TutorialAllows(TutorialAction.SelectRecipe,recipeId:recipeId))return new OperationResult{error=Tutorials.LastError};
        Tutorials?.Succeeded(TutorialAction.SelectRecipe);
        return new OperationResult{success=true};
    }
    // UI-only steps of a hard lesson (open/close the recipe book, plan a dish): ask first, report after.
    public bool AskTutorial(TutorialAction action, string recipeId=null) => TutorialAllows(action, recipeId:recipeId);
    public void TellTutorial(TutorialAction action) { Tutorials?.Succeeded(action); RefreshTutorials(); }
    // First-time screens the UI reports (store, shelf, pantry, guests) and map clicks on unavailable stores.
    public void NoteSeen(string flag) { if (flag != null && flag.StartsWith("tutorial.")) Tutorials?.Note(flag); RefreshTutorials(); }
    public bool ContinueTutorial() { bool done=Tutorials?.ContinueText()==true; RefreshTutorials(); return done; }
    public void MarkTutorialSeen() => Tutorials?.MarkInstructionSeen();
    public void InspectToolbar(string item) { Tutorials?.NoteToolbarClick(item); RefreshTutorials(); }

    public GameLoop(IPurchaseService purchases, IMealService meals, IDayService days,
        ICustomerVisitService visits, EncounterScheduler encounters = null, FoodSortingService sorting = null, bool completed = false, EndingService endings = null, EventService events = null, PantryService pantry = null, FoodAidService foodAid = null)
    {
        this.purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
        this.meals = meals ?? throw new ArgumentNullException(nameof(meals));
        this.days = days ?? throw new ArgumentNullException(nameof(days));
        this.visits = visits ?? throw new ArgumentNullException(nameof(visits));
        this.endings=endings;this.events=events;this.pantry=pantry;this.foodAid=foodAid;
        if(!completed)events?.PrepareMorning();
        if(completed)endings?.Complete();
        this.encounters = encounters;
        this.sorting = sorting;
        if (completed) Phase = GamePhase.Completed;
        else visits.GetTodaysCustomers(); // Prepare requests before market, and before the checkpoint.
        if (encounters != null) Dialogue = new DialoguePlaybackController(encounters.Dialogue);
    }

    public PurchaseResult Buy(string listingId)
    {
        if (sorting != null) return new PurchaseResult { error = "Add items to the cart and confirm checkout." };
        if (Phase != GamePhase.Market) return new PurchaseResult { error = "Buying requires Market phase." };
        var result = purchases.Buy(listingId);
        if (result.success) Summary.moneySpent += result.moneySpent;
        return result;
    }

    // Pantry sales happen at the market, before opening the bistro, so a broke player can raise cash to shop.
    public bool CanSellPantry => pantry != null && Phase == GamePhase.Market;
    public float PantryValuePerServing(string inventoryEntryId) => pantry == null ? -1 : pantry.ValuePerServing(inventoryEntryId);
    public PantrySaleResult QuotePantrySale(List<PantrySaleItem> items) => pantry == null ? new PantrySaleResult { error = "Selling is unavailable." } : pantry.Quote(items);
    public PantrySaleResult SellPantry(List<PantrySaleItem> items)
    {
        if (!CanSellPantry) return new PantrySaleResult { error = "Pantry food can be sold at the market." };
        var result = pantry.Sell(items);
        if (result.success) { Summary.pantrySales += result.amount; RefreshTutorials(); }
        return result;
    }

    public CartResult QuoteCart(List<CartEntry> cart) => purchases is ICartPurchaseService carts
        ? carts.Quote(cart) : new CartResult { error = "Cart service unavailable." };

    public CartResult Checkout(List<CartEntry> cart)
    {
        if (Phase != GamePhase.Market || sorting == null || !(purchases is ICartPurchaseService carts))
            return new CartResult { error = "Checkout requires Market phase and sorting services." };
        if(!TutorialAllows(TutorialAction.Checkout,cart))return new CartResult{error=Tutorials.LastError};
        bool shoppedBefore = sorting.HasCheckout;
        var result = carts.Checkout(cart);
        if (!result.success) return result;
        if (shoppedBefore) Tutorials?.Note(TutorialFlags.SecondCheckout);
        Tutorials?.Succeeded(TutorialAction.Checkout);
        Summary.moneySpent += result.total;
        SortingResult = null;
        // Each purchase is sorted right away (if it has new foods), then the player returns to the market
        // (ReturnToMarket) and may check out again. Only EnterBistro ends shopping (2026-11-13).
        if (sorting.IsComplete) RefreshTutorials();
        else SetPhase(GamePhase.FoodSorting);
        return result;
    }

    // After a purchase's sorting round is submitted, go back to the market to keep shopping.
    public bool ReturnToMarket()
    {
        if (Phase != GamePhase.FoodSorting || sorting == null || !sorting.IsComplete) return false;
        if (!TutorialAllows(TutorialAction.BeginService)) return false;
        SetPhase(GamePhase.Market);
        return true;
    }

    // The only way out of the market (the Bistro on the city map): start service, or continue it after a trip to the city.
    public bool EnterBistro()
    {
        if (Phase != GamePhase.Market) return false;
        if (!TutorialAllows(TutorialAction.BeginService)) return false;
        Tutorials?.Succeeded(TutorialAction.BeginService);
        return BistroOpened ? ResumeService() : StartService();
    }

    // Leaving is allowed at any service step and is never blocked by a lesson (Owen, 2026-11-13); the UI shows
    // the button only when the switch is on. An active lesson simply continues when the player returns.
    public bool CanLeaveBistro => Phase == GamePhase.Service && Service.CurrentEncounter != null;
    public bool LeaveBistro()
    {
        if (!CanLeaveBistro) return false;
        // A served guest (dish result or closing dialogue) does not wait: the next encounter starts on return.
        if (Service.step == ServiceStep.DishResult || Service.step == ServiceStep.AfterDialogue)
        { Service.encounterIndex++; Service.lastDishResult = null; Service.lastRecipeId = null; Service.step = ServiceStep.ArrivalDialogue; }
        // An unfinished dialogue is dropped without its effects; that encounter starts again on return.
        Dialogue?.Cancel();
        if (Service.step != ServiceStep.MealSelection) Service.step = ServiceStep.ArrivalDialogue;
        Tutorials?.Note(TutorialFlags.CityDuringService);
        SetPhase(GamePhase.Market);
        return true;
    }
    private bool ResumeService()
    {
        Phase = GamePhase.Service;
        // Meal selection resumes directly (the farmer check runs again, since the pantry may have changed);
        // anything else restarts the current encounter. An empty queue goes straight to Results.
        if (Service.step == ServiceStep.MealSelection && Service.CurrentEncounter?.kind == EncounterKind.Customer) EnterMealSelection();
        else StartEncounter();
        RefreshTutorials();
        if (Phase == GamePhase.Service) PhaseChanged?.Invoke(Phase);
        return true;
    }

    public SortingResult SubmitSorting(List<SortingChoice> choices)
    {
        if (Phase != GamePhase.FoodSorting || sorting == null)
            return new SortingResult { error = "Sorting is not active." };
        if(!TutorialAllows(TutorialAction.SubmitSorting))return new SortingResult{error=Tutorials.LastError};
        var result = sorting.Submit(choices);
        if (result.success)
        {
            SortingResult = result;
            Summary.moneySpent -= result.refundAmount;
            if (result.correctCount < result.questionCount) Tutorials?.Note(TutorialFlags.SortMistake);
        }
        return result;
    }

    public ServeResult PreviewServe(ServeInput input) => EvaluateServe(input, false);
    public ServeResult Serve(ServeInput input) => EvaluateServe(input, true);

    private ServeResult EvaluateServe(ServeInput input, bool commit)
    {
        if (Phase != GamePhase.Service || Service.step != ServiceStep.MealSelection || Service.awaitingDishAcknowledgement ||
            input == null || Service.CurrentCustomerId == null ||
            !string.Equals(input.customerId, Service.CurrentCustomerId, StringComparison.OrdinalIgnoreCase))
            return new ServeResult { error = "Serve the current customer, then continue after the dish result." };
        if(commit && !TutorialAllows(TutorialAction.Serve,recipeId:input.recipeId,preview:meals.Preview(input)))
            return new ServeResult{error=Tutorials.LastError};
        var result = commit ? meals.Serve(input) : meals.Preview(input);
        if (commit && result.success)
        {
            Service.lastDishResult = result;
            Service.step = ServiceStep.DishResult;
            if (!result.recipeBuilt) Tutorials?.Note(TutorialFlags.IncompleteDish);
            else if (result.popularityDelta < 0) Tutorials?.Note(TutorialFlags.PopularityDrop);
            Service.lastRecipeId = input.recipeId;
            Summary.moneyEarned += result.moneyEarned;
            Summary.dishes.Add(new DishRecord { customerId = input.customerId,
                recipeId = input.recipeId, result = result });
            Tutorials?.Succeeded(TutorialAction.Serve,result);
        }
        return result;
    }

    public bool BeginService()
    {
        if (Phase == GamePhase.Market) return EnterBistro();
        if (Phase == GamePhase.FoodSorting) return ReturnToMarket();
        if(!TutorialAllows(TutorialAction.BeginService))return false;
        return StartService();
    }
    // Starts service; callers ask the tutorial first (EnterBistro).
    private bool StartService()
    {
        if (Phase != GamePhase.Market) return false;
        if (sorting != null && sorting.HasCheckout && !sorting.IsComplete) return false;
        var customers = visits.GetTodaysCustomers();
        Service = new ServiceSessionState();
        BistroOpened = true;
        if (encounters != null) Service.encounters = encounters.Build(customers);
        else foreach (string id in customers) Service.encounters.Add(new EncounterEntry { kind = EncounterKind.Customer, customerId = id });
        // Finish preparing the encounter before notifying observers. An empty queue
        // transitions straight to Results and must not emit a stale Service event.
        Phase = GamePhase.Service;
        StartEncounter();
        RefreshTutorials();
        if (Phase == GamePhase.Service) PhaseChanged?.Invoke(Phase);
        return true;
    }

    public bool ShowResults()
    {
        if (Phase != GamePhase.Service || Service.CurrentEncounter != null || (Dialogue != null && Dialogue.IsActive)) return false;
        float walkIns = days.CloseService(); // Once per day: the phase guard prevents a second call.
        Summary.walkInIncome = walkIns;
        Summary.moneyEarned += walkIns;
        SetPhase(GamePhase.Results);
        return true;
    }

    public bool SkipCustomer()
    {
        if(!TutorialAllows(TutorialAction.SkipCustomer))return false;
        if (Phase != GamePhase.Service || Service.CurrentCustomerId == null ||
            (Service.step != ServiceStep.MealSelection && Service.step != ServiceStep.DishResult)) return false;
        if (Service.awaitingDishAcknowledgement) return ContinueAfterDish();
        Summary.skippedCustomerIds.Add(Service.CurrentCustomerId);
        Tutorials?.Note(TutorialFlags.SkippedCustomer);
        AdvanceEncounter();
        return true;
    }

    public bool ContinueAfterDish()
    {
        if(!TutorialAllows(TutorialAction.ContinueDish))return false;
        if (Phase != GamePhase.Service || Service.step != ServiceStep.DishResult || !Service.awaitingDishAcknowledgement) return false;
        Service.step = ServiceStep.AfterDialogue;
        if (!StartCustomerDialogue(DialogueTrigger.AfterService)) AdvanceEncounter();
        return true;
    }

    private void AdvanceEncounter()
    {
        Service.encounterIndex++;
        Service.lastDishResult = null;
        Service.lastRecipeId = null;
        StartEncounter();
    }

    private void StartEncounter()
    {
        while (Service.CurrentEncounter != null)
        {
            var entry = Service.CurrentEncounter;
            if (entry.kind == EncounterKind.Story)
            {
                Service.step = ServiceStep.StoryDialogue;
                var context = encounters.Context(entry.dialogue.customer?.id);
                if (encounters.Dialogue.IsEligible(entry.dialogue, DialogueTrigger.ServiceEncounter, context) &&
                    Dialogue.Start(entry.dialogue, context)) return;
                Service.encounterIndex++; // Earlier completion may have invalidated this queued event.
                continue;
            }
            Service.step = ServiceStep.ArrivalDialogue;
            if (!StartCustomerDialogue(DialogueTrigger.CustomerArrival)) StartBeforeDialogue();
            return;
        }
        Service.step = ServiceStep.Finished;
        ShowResults();
    }
    private bool StartCustomerDialogue(DialogueTrigger trigger)
    {
        if (encounters == null) return false;
        var context = encounters.Context(Service.CurrentCustomerId, Service.lastRecipeId, Service.lastDishResult);
        var selectedTrigger = encounters.CustomerTrigger(trigger, Service.CurrentCustomerId, Service.lastDishResult);
        return Dialogue.Start(encounters.Dialogue.FindNext(selectedTrigger, context), context);
    }
    // Every path into meal selection goes through here so the farmer can step in when nothing can be cooked.
    private void EnterMealSelection()
    {
        Service.step = ServiceStep.MealSelection;
        var aid = foodAid?.TryGive();
        if (aid != null && aid.given) { Service.foodAidMessage = aid.message; Summary.foodAidMessage = aid.message; RefreshTutorials(); }
    }
    private void StartBeforeDialogue()
    {
        Service.step = ServiceStep.BeforeDialogue;
        if (!StartCustomerDialogue(DialogueTrigger.BeforeService)) EnterMealSelection();
    }
    public bool AdvanceDialogue()
    {
        if(!TutorialAllows(TutorialAction.AdvanceDialogue))return false;
        if (Phase != GamePhase.Service || Dialogue == null || !Dialogue.IsActive) return false;
        var completedSequence = Dialogue.Sequence;
        if (!Dialogue.Next()) return true;
        encounters.OnDialogueCompleted(completedSequence, Service.CurrentCustomerId);
        switch (Service.step)
        {
            case ServiceStep.StoryDialogue: AdvanceEncounter(); break;
            case ServiceStep.ArrivalDialogue: StartBeforeDialogue(); break;
            case ServiceStep.BeforeDialogue: EnterMealSelection(); break;
            case ServiceStep.AfterDialogue: AdvanceEncounter(); break;
        }
        RefreshTutorials();
        return true;
    }

    public DayResult FinishDay()
    {
        if(!TutorialAllows(TutorialAction.FinishDay))return new DayResult{error=Tutorials.LastError};
        if (Phase != GamePhase.Results) return new DayResult { error = "Finish the day from Results." };
        return EndDay();
    }

    // Debug panel only (development builds): ends today from any screen, without serving the remaining guests and
    // without asking the tutorial. Rent, spoilage, events, guests and the day-start save then run as normal.
    public DayResult DebugEndDay()
    {
        if (Phase == GamePhase.Completed) return new DayResult { error = "The run is already complete." };
        if (Dialogue != null && Dialogue.IsActive) Dialogue.Cancel();
        Service = new ServiceSessionState();
        Phase = GamePhase.Results;
        return EndDay();
    }

    private DayResult EndDay()
    {
        var result = days.FinishDay();
        if (result.success)
        {
            if (!result.gameCompleted)
            {
                Summary = new DaySummary { rentPaid = result.rentCharged };
                Service = new ServiceSessionState();
                BistroOpened = false;
                SortingResult = null;
                events?.PrepareMorning();
                visits.GetTodaysCustomers();
            }
            if(result.gameCompleted)endings?.Complete();
            SetPhase(result.gameCompleted ? GamePhase.Completed : GamePhase.Market);
            DayFinished?.Invoke();
        }
        return result;
    }

    private void SetPhase(GamePhase phase)
    {
        Phase = phase;
        RefreshTutorials();
        PhaseChanged?.Invoke(phase);
    }
}
