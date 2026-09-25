// File responsibility: Coordinates Market, encounters, dish acknowledgement, and Results in response to UI actions.
// Owns flow only; services calculate outcomes and mutate session data. Unity supplies the event loop.

using System;

// Phase orchestration is separate from operations and presentation.
public class GameLoop
{
    private readonly IPurchaseService purchases;
    private readonly IMealService meals;
    private readonly IDayService days;
    private readonly ICustomerVisitService visits;
    private readonly EncounterScheduler encounters;
    public DialoguePlaybackController Dialogue { get; }
    public ServiceSessionState Service { get; private set; } = new ServiceSessionState();
    public DaySummary Summary { get; private set; } = new DaySummary();
    public GamePhase Phase { get; private set; } = GamePhase.Market;
    public event Action<GamePhase> PhaseChanged;

    public GameLoop(IPurchaseService purchases, IMealService meals, IDayService days,
        ICustomerVisitService visits, EncounterScheduler encounters = null)
    {
        this.purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
        this.meals = meals ?? throw new ArgumentNullException(nameof(meals));
        this.days = days ?? throw new ArgumentNullException(nameof(days));
        this.visits = visits ?? throw new ArgumentNullException(nameof(visits));
        this.encounters = encounters;
        if (encounters != null) Dialogue = new DialoguePlaybackController(encounters.Dialogue);
    }

    public PurchaseResult Buy(string listingId)
    {
        if (Phase != GamePhase.Market) return new PurchaseResult { error = "Buying requires Market phase." };
        var result = purchases.Buy(listingId);
        if (result.success) Summary.moneySpent += result.moneySpent;
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
        var result = commit ? meals.Serve(input) : meals.Preview(input);
        if (commit && result.success)
        {
            Service.lastDishResult = result;
            Service.step = ServiceStep.DishResult;
            Service.lastRecipeId = input.recipeId;
            Summary.moneyEarned += result.moneyEarned;
            Summary.dishes.Add(new DishRecord { customerId = input.customerId,
                recipeId = input.recipeId, result = result });
        }
        return result;
    }

    public bool BeginService()
    {
        if (Phase != GamePhase.Market) return false;
        var customers = visits.GetTodaysCustomers();
        Service = new ServiceSessionState();
        if (encounters != null) Service.encounters = encounters.Build(customers);
        else foreach (string id in customers) Service.encounters.Add(new EncounterEntry { kind = EncounterKind.Customer, customerId = id });
        // Finish preparing the encounter before notifying observers. An empty queue
        // transitions straight to Results and must not emit a stale Service event.
        Phase = GamePhase.Service;
        StartEncounter();
        if (Phase == GamePhase.Service) PhaseChanged?.Invoke(Phase);
        return true;
    }

    public bool ShowResults()
    {
        if (Phase != GamePhase.Service || Service.CurrentEncounter != null || (Dialogue != null && Dialogue.IsActive)) return false;
        SetPhase(GamePhase.Results);
        return true;
    }

    public bool SkipCustomer()
    {
        if (Phase != GamePhase.Service || Service.CurrentCustomerId == null ||
            (Service.step != ServiceStep.MealSelection && Service.step != ServiceStep.DishResult)) return false;
        if (Service.awaitingDishAcknowledgement) return ContinueAfterDish();
        Summary.skippedCustomerIds.Add(Service.CurrentCustomerId);
        AdvanceEncounter();
        return true;
    }

    public bool ContinueAfterDish()
    {
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
        return Dialogue.Start(encounters.Dialogue.FindNext(trigger, context), context);
    }
    private void StartBeforeDialogue()
    {
        Service.step = ServiceStep.BeforeDialogue;
        if (!StartCustomerDialogue(DialogueTrigger.BeforeService)) Service.step = ServiceStep.MealSelection;
    }
    public bool AdvanceDialogue()
    {
        if (Phase != GamePhase.Service || Dialogue == null || !Dialogue.IsActive) return false;
        if (!Dialogue.Next()) return true;
        switch (Service.step)
        {
            case ServiceStep.StoryDialogue: AdvanceEncounter(); break;
            case ServiceStep.ArrivalDialogue: StartBeforeDialogue(); break;
            case ServiceStep.BeforeDialogue: Service.step = ServiceStep.MealSelection; break;
            case ServiceStep.AfterDialogue: AdvanceEncounter(); break;
        }
        return true;
    }

    public DayResult FinishDay()
    {
        if (Phase != GamePhase.Results) return new DayResult { error = "Finish the day from Results." };
        var result = days.FinishDay();
        if (result.success)
        {
            if (!result.gameCompleted)
            {
                Summary = new DaySummary();
                Service = new ServiceSessionState();
            }
            SetPhase(result.gameCompleted ? GamePhase.Completed : GamePhase.Market);
        }
        return result;
    }

    private void SetPhase(GamePhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }
}
