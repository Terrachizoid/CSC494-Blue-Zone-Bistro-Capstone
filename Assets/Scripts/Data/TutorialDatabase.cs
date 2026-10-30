// Authored tutorial metadata/text. Behaviour IDs map to registered code, never UI object names.
using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable] public class TutorialStepText { public string stepId; public string text; }
[Serializable] public class TutorialDefinition
{
    public string id, triggerFlag, behaviourId;
    public int priority;
    public string activationPhase;
    public string[] completionFlags=Array.Empty<string>();
    public List<TutorialStepText> steps=new List<TutorialStepText>();
}
[CreateAssetMenu(menuName="Blue Zone Bistro/Tutorial Database")]
public class TutorialDatabase : ScriptableObject
{
    public List<TutorialDefinition> tutorials=new List<TutorialDefinition>();
    public string fingerprint;
}
[Serializable] public class TutorialRecord
{
    public string tutorialId, stepId, status, skipReason;
    // Action step whose instruction the player has already seen. Kept with the lesson (not in the UI) so a
    // lesson paused for another one, or reopened after a reload, does not pop its instruction again.
    public string seenStepId;
}
