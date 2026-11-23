// File responsibility: Transient encounter cursor, current service step, and dish result for GameLoop.
// DaySummary records accepted actions; this is not a second persistent customer or inventory database.

using System;
using System.Collections.Generic;

[Serializable]
public class ServiceSessionState
{
    public List<EncounterEntry> encounters = new List<EncounterEntry>();
    public int encounterIndex;
    public ServiceStep step = ServiceStep.MealSelection;
    // Derived from the step so acknowledgement and the state machine cannot disagree.
    public bool awaitingDishAcknowledgement => step == ServiceStep.DishResult;
    public ServeResult lastDishResult;
    public string lastRecipeId;
    public string foodAidMessage; // Set when the farmer gives food during this service; shown on the meal screen.
    // The closing dialogue chosen when the dish was served, so its first expression can show with the dish result
    // (characters, 2026-11-22). Never saved: service progress is not part of the day-start checkpoint.
    [NonSerialized] public DialogueSequenceData pendingAfterDialogue;
    public EncounterEntry CurrentEncounter => encounters != null && encounterIndex >= 0 &&
        encounterIndex < encounters.Count ? encounters[encounterIndex] : null;
    public string CurrentCustomerId => CurrentEncounter?.customerId;
}

public enum ServiceStep { StoryDialogue, ArrivalDialogue, BeforeDialogue, MealSelection, DishResult, AfterDialogue, Finished }
public enum EncounterKind { Story, Customer }
[Serializable]
public class EncounterEntry
{
    public EncounterKind kind;
    public string customerId;
    public DialogueSequenceData dialogue;
}

[Serializable]
public class DaySummary
{
    public float moneySpent;
    public float moneyEarned; // Includes walkInIncome.
    public float walkInIncome;
    public float rentPaid; // Charged this morning, before the market opened.
    public float pantrySales; // Cash from selling pantry batches today (not included in moneyEarned).
    public string foodAidMessage; // The farmer's gift today, for the results screen.
    public List<DishRecord> dishes = new List<DishRecord>();
    public List<string> skippedCustomerIds = new List<string>();
}

[Serializable]
public class DishRecord
{
    public string customerId;
    public string recipeId;
    public ServeResult result;
}
