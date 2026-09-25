// File responsibility: Serializable case-insensitive flag set shared by gameplay unlocks and dialogue completion.
// Keep stable flag IDs in CSVs; clearing a flag does not erase dialogue playback history.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class GameFlagState : ISerializationCallbackReceiver
{
    [SerializeField]
    private List<string> activeFlags = new List<string>();

    [NonSerialized]
    private HashSet<string> index;

    public IReadOnlyList<string> ActiveFlags => activeFlags;

    public bool HasFlag(string flagId)
    {
        EnsureIndex();
        return !string.IsNullOrWhiteSpace(flagId) && index.Contains(flagId);
    }

    // Setting a flag is also its registration step. Duplicate sets are safe.
    public bool SetFlag(string flagId)
    {
        string normalized = Normalize(flagId);

        if (string.IsNullOrEmpty(normalized))
        {
            Debug.LogWarning("Cannot register a blank game flag.");
            return false;
        }

        EnsureIndex();

        if (!index.Add(normalized))
        {
            return false;
        }

        activeFlags.Add(normalized);
        activeFlags.Sort(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    public bool ClearFlag(string flagId)
    {
        string normalized = Normalize(flagId);
        EnsureIndex();

        if (string.IsNullOrEmpty(normalized) || !index.Remove(normalized))
        {
            return false;
        }

        activeFlags.RemoveAll(value =>
            string.Equals(
                value,
                normalized,
                StringComparison.OrdinalIgnoreCase));
        return true;
    }

    public bool HasAll(IEnumerable<string> flagIds)
    {
        if (flagIds == null)
        {
            return true;
        }

        return flagIds.All(HasFlag);
    }

    public bool HasAny(IEnumerable<string> flagIds)
    {
        if (flagIds == null)
        {
            return false;
        }

        return flagIds.Any(HasFlag);
    }

    public void Reset()
    {
        activeFlags.Clear();
        index = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void OnBeforeSerialize()
    {
        activeFlags = activeFlags
            .Select(Normalize)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void OnAfterDeserialize()
    {
        RebuildIndex();
    }

    private void EnsureIndex()
    {
        if (index == null)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        activeFlags = activeFlags ?? new List<string>();
        index = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string flag in activeFlags)
        {
            string normalized = Normalize(flag);

            if (!string.IsNullOrEmpty(normalized))
            {
                index.Add(normalized);
            }
        }
    }

    private static string Normalize(string flagId)
    {
        return flagId?.Trim() ?? string.Empty;
    }
}
