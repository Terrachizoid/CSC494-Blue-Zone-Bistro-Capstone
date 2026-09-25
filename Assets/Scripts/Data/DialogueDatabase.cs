// File responsibility: Imported dialogue catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DialogueDatabase",
    menuName = "Blue Zone Bistro/Databases/Dialogues")]
public class DialogueDatabase : ScriptableObject
{
    // Editor import provenance; detects stale assets before entering Play Mode.
    public string importedCsvFingerprint;
    [SerializeField]
    private List<DialogueSequenceData> dialogues =
        new List<DialogueSequenceData>();

    private Dictionary<string, DialogueSequenceData> byId;

    public IReadOnlyList<DialogueSequenceData> Dialogues => dialogues;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<DialogueSequenceData> items)
    {
        dialogues = items ?? new List<DialogueSequenceData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out DialogueSequenceData dialogue)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out dialogue);
    }

    public DialogueSequenceData GetById(string id)
    {
        return TryGetById(id, out DialogueSequenceData dialogue)
            ? dialogue
            : null;
    }

    private void EnsureIndex()
    {
        if (byId == null)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        byId = new Dictionary<string, DialogueSequenceData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (DialogueSequenceData dialogue in dialogues)
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.id))
            {
                continue;
            }

            if (!byId.TryAdd(dialogue.id, dialogue))
            {
                Debug.LogError(
                    $"Duplicate DialogueSequenceData ID '{dialogue.id}'.",
                    this);
            }
        }
    }
}
