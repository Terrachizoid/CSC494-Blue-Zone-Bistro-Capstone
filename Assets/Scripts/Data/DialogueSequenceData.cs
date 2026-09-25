// File responsibility: One schedulable conversation and its ordered lines, eligibility conditions, and completion effects.
// Voice/portrait references are optional. Flags/history change only after the final line is acknowledged.

using System;
using UnityEngine;

[Serializable]
public class DialogueLineData
{
    [Min(0)]
    public int lineOrder;

    public string speakerId;

    [TextArea(2, 6)]
    public string textTemplate;

    public ClaimData claim;
    public Sprite portrait;
    public string emotion;
    public AudioClip voiceOver;
}

[CreateAssetMenu(
    fileName = "NewDialogueSequence",
    menuName = "Blue Zone Bistro/Dialogue Sequence")]
public class DialogueSequenceData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Selection")]
    public DialogueMode mode = DialogueMode.Ambient;
    public DialogueTrigger trigger = DialogueTrigger.CustomerArrival;
    public DialogueRepeatRule repeatRule = DialogueRepeatRule.Repeatable;
    public int priority;

    [Min(0f)]
    public float randomWeight = 1f;

    [Header("Who and When")]
    public CustomerData customer;

    [Min(1)]
    public int minDay = 1;

    // -1 means that there is no maximum day.
    public int maxDay = -1;

    [Header("Customer State Conditions")]
    // -1 means that this condition is not used.
    public int minCustomerHealth = -1;
    public int maxCustomerHealth = -1;
    public int minCustomerDelight = -1;
    public int maxCustomerDelight = -1;

    [Header("Served Meal Conditions")]
    public RecipeData requiredRecipe;
    public RecipeBuildCondition recipeBuildCondition = RecipeBuildCondition.Any;
    public MealHealthCondition mealHealthCondition = MealHealthCondition.Any;
    public RecipeTier[] requiredRecipeTiers = Array.Empty<RecipeTier>();

    // These refer to the completed meal served to this customer.
    // -1 means that this condition is not used.
    public int minServedMealDelight = -1;
    public int maxServedMealDelight = -1;

    [Header("Persistent Flag Conditions")]
    public string[] requiredFlags = Array.Empty<string>();
    public string[] forbiddenFlags = Array.Empty<string>();

    [Header("Completion Effects")]
    public string[] setFlags = Array.Empty<string>();
    public string[] clearFlags = Array.Empty<string>();

    [Header("Lines")]
    public DialogueLineData[] lines = Array.Empty<DialogueLineData>();

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;
}
