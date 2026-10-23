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
    public SortingResult SortingResult { get; private set; }
    public List<SortingQuestion> SortingQuestions => sorting?.GetQuestions() ?? new List<SortingQuestion>();
    public DialoguePlaybackController Dialogue { get; }
    public ServiceSessionState Service { get; private set; } = new ServiceSessionState();
    public DaySummary Summary { get; private set; } = new DaySummary();
    public GamePhase Phase { get; private set; } = GamePhase.Market;
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
    public bool ContinueTutorial() { bool done=Tutorials?.ContinueText()==true; RefreshTutorials(); return done; }

    public GameLoop(IPurchaseService purchases, IMealService meals, IDayService days,
        ICustomerVisitService visits, EncounterScheduler encounters = null, FoodSortingService sorting = null, bool completed = false, EndingService endings = null, EventService events = null)
    {
        this.purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
        this.meals = meals ?? throw new ArgumentNullException(nameof(meals));
        this.days = days ?? throw new ArgumentNullException(nameof(days));
        this.visits = visits ?? throw new ArgumentNullException(nameof(visits));
        this.endings=endings;this.events=events;
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

    public CartResult QuoteCart(List<CartEntry> cart) => purchases is ICartPurchaseService carts
        ? carts.Quote(cart) : new CartResult { error = "Cart service unavailable." };

    public CartResult Checkout(List<CartEntry> cart)
    {
        if (Phase != GamePhase.Market || sorting == null || !(purchases is ICartPurchaseService carts))
            return new CartResult { error = "Checkout requires Market phase and sorting services." };
        if(!TutorialAllows(TutorialAction.Checkout,cart))return new CartResult{error=Tutorials.LastError};
        var result = carts.Checkout(cart);
        if (!result.success) return result;
        Tutorials?.Succeeded(TutorialAction.Checkout);
        Summary.moneySpent += result.total;
        SortingResult = null;
        if (sorting.IsComplete) BeginService();
        else SetPhase(GamePhase.FoodSorting);
        return result;
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
        if(!TutorialAllows(TutorialAction.BeginService))return false;
        if (Phase != GamePhase.Market && Phase != GamePhase.FoodSorting) return false;
        if (sorting != null && !sorting.IsComplete) return false;
        var customers = visits.GetTodaysCustomers();
        Service = new ServiceSessionState();
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
    private void StartBeforeDialogue()
    {
        Service.step = ServiceStep.BeforeDialogue;
        if (!StartCustomerDialogue(DialogueTrigger.BeforeService)) Service.step = ServiceStep.MealSelection;
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
            case ServiceStep.BeforeDialogue: Service.step = ServiceStep.MealSelection; break;
            case ServiceStep.AfterDialogue: AdvanceEncounter(); break;
        }
        RefreshTutorials();
        return true;
    }

    public DayResult FinishDay()
    {
        if(!TutorialAllows(TutorialAction.FinishDay))return new DayResult{error=Tutorials.LastError};
        if (Phase != GamePhase.Results) return new DayResult { error = "Finish the day from Results." };
        var result = days.FinishDay();
        if (result.success)
        {
            if (!result.gameCompleted)
            {
                Summary = new DaySummary { rentPaid = result.rentCharged };
                Service = new ServiceSessionState();
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
