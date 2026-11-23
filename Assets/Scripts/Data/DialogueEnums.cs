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
    ServiceEncounter,
    RushAfterService,
    CrashArrival,
    Ending
}

public enum RecipeBuildCondition { Any, Built, Failed }
public enum MealHealthCondition { Any, Gain, Loss, Unchanged }
// The guest's requests (cravings) for the dish just served. All/Some/None need at least one request; NoRequest = the
// guest asked for nothing today; Missed = Some or None. A plate that did not build its recipe meets no request.
public enum RequestCondition { Any, All, Some, None, NoRequest, Missed }
// On/off checks on the dish just served (2026-11-22). Any = not checked.
public enum DialogueSwitch { Any, Yes, No }

public enum DialogueRepeatRule
{
    Once,
    OncePerDay,
    Repeatable
}
