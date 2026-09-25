// File responsibility: Serializable played-sequence history for Once and OncePerDay eligibility.
// The live game shares this object with DialogueService; do not construct a separate gameplay history.

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueLastPlayedRecord
{
    public string dialogueId;
    public int day;
}

[Serializable]
public class DialogueHistoryState
{
    [SerializeField]
    private List<string> completedOnceIds = new List<string>();

    [SerializeField]
    private List<DialogueLastPlayedRecord> lastPlayed =
        new List<DialogueLastPlayedRecord>();

    public bool CanPlay(
        DialogueSequenceData sequence,
        int currentDay)
    {
        if (sequence == null)
        {
            return false;
        }

        switch (sequence.repeatRule)
        {
            case DialogueRepeatRule.Once:
                return !ContainsIgnoreCase(completedOnceIds, sequence.id);

            case DialogueRepeatRule.OncePerDay:
                DialogueLastPlayedRecord record = FindRecord(sequence.id);
                return record == null || record.day != currentDay;

            default:
                return true;
        }
    }

    public void MarkPlayed(
        DialogueSequenceData sequence,
        int currentDay)
    {
        if (sequence == null || string.IsNullOrWhiteSpace(sequence.id))
        {
            return;
        }

        if (sequence.repeatRule == DialogueRepeatRule.Once &&
            !ContainsIgnoreCase(completedOnceIds, sequence.id))
        {
            completedOnceIds.Add(sequence.id);
        }

        DialogueLastPlayedRecord record = FindRecord(sequence.id);

        if (record == null)
        {
            record = new DialogueLastPlayedRecord
            {
                dialogueId = sequence.id
            };
            lastPlayed.Add(record);
        }

        record.day = currentDay;
    }

    public void Reset()
    {
        completedOnceIds.Clear();
        lastPlayed.Clear();
    }

    private DialogueLastPlayedRecord FindRecord(string dialogueId)
    {
        return lastPlayed.Find(record =>
            record != null &&
            string.Equals(
                record.dialogueId,
                dialogueId,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsIgnoreCase(
        List<string> values,
        string target)
    {
        return values.Exists(value =>
            string.Equals(
                value,
                target,
                StringComparison.OrdinalIgnoreCase));
    }
}
