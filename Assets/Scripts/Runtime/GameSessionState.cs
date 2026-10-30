// File responsibility: Shared mutable state for one run: day, money, pantry, customers, flags, and dialogue history.
// Services receive this same instance. Serialization alone does not implement saving or restoring a run.

using System;
using System.Collections.Generic;

// Mutable data for one playthrough. Game flow and save/load belong in separate systems.
[Serializable]
public class GameSessionState
{
    public int eventPlanDay;
    public List<EventRecord> events=new List<EventRecord>();
    public string endingId;
    public int currentDay = 1;
    public bool gameCompleted;
    public List<TutorialRecord> tutorials = new List<TutorialRecord>();
    public DaySummary completedDaySummary;
    public float money = 30f;
    // Version marker distinguishes older checkpoints from a legitimate zero score.
    public int communityStateVersion;
    public float popularity = 300f;
    public float communityWellbeing = 50f;
    public int attendancePlanDay;
    public float morningPopularity;
    public int dailyCustomerCapacity;
    public List<string> dailyCustomerIds = new List<string>();
    public List<string> unlockedCustomerIds = new List<string>();
    public List<InventoryEntry> inventory = new List<InventoryEntry>();
    public string currentCustomerId;
    public string selectedRecipeId;
    public List<CustomerState> customers = new List<CustomerState>();
    public GameFlagState flags = new GameFlagState();
    public DialogueHistoryState dialogueHistory = new DialogueHistoryState();
    // Correct answers only. Wrong foods can return after a later day's purchase.
    public List<string> correctlySortedFoodIds = new List<string>();
    public ShoppingReceipt shoppingReceipt;
    // Last day the farmer gave free food (0 = never). At most once per day.
    public int lastFoodAidDay;
    // Every served dish (built or not), for recipe fatigue and the end-of-game recap.
    public List<RecipeServeRecord> recipeServes = new List<RecipeServeRecord>();
}
