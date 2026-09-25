// File responsibility: Dialogue scheduling and outcome condition vocabulary shared by CSVs and runtime selection.
// Use recipe build conditions for failures: actual delight delta can be zero at the lower bound.

public enum DialogueMode
{
    Mandatory,
    Ambient
}

public enum DialogueTrigger
{
    DayStart,
    CustomerArrival,
    BeforeService,
    AfterService,
    DayEnd,
    ServiceEncounter
}

public enum RecipeBuildCondition { Any, Built, Failed }
public enum MealHealthCondition { Any, Gain, Loss, Unchanged }

public enum DialogueRepeatRule
{
    Once,
    OncePerDay,
    Repeatable
}
