// File responsibility: Every sound in the game, imported from Blue_Zone_Bistro_Sounds.csv. The code never names a
// file: it plays a cue (what happened, e.g. "button.primary", "card.flip", "dish.good", "music.city") and this table
// says which clip(s) it means. Lookup falls back from the most specific cue to its parents ("event.news.E003" ->
// "event.news" -> "event"), then plays nothing, so a missing sound is silence, never an error.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundKind { Sfx, Music }

[Serializable]
public class SoundCue
{
    public string id;                                  // cue_id, dot-separated, most general first (button.primary)
    public SoundKind kind = SoundKind.Sfx;
    public AudioClip[] clips = Array.Empty<AudioClip>(); // One is picked at random each time (variants).
    [Range(0, 1)] public float volume = 1;
    [Range(0, .5f)] public float pitchVariation;       // Random +/- pitch so repeated clicks do not sound identical.
    public bool HasClips => clips != null && Array.Exists(clips, c => c != null);
}

[CreateAssetMenu(fileName = "SoundDatabase", menuName = "Blue Zone Bistro/Sounds")]
public class SoundDatabase : ScriptableObject
{
    public List<SoundCue> cues = new List<SoundCue>();
    private Dictionary<string, SoundCue> index;

    // The cue itself or its nearest parent that has clips; null = silence.
    public SoundCue Find(string cueId)
    {
        if (string.IsNullOrWhiteSpace(cueId)) return null;
        if (index == null)
        {
            index = new Dictionary<string, SoundCue>(StringComparer.OrdinalIgnoreCase);
            foreach (var cue in cues) if (cue != null && !string.IsNullOrWhiteSpace(cue.id) && cue.HasClips) index[cue.id.Trim()] = cue;
        }
        for (string id = cueId.Trim(); id.Length > 0; )
        {
            if (index.TryGetValue(id, out var cue)) return cue;
            int dot = id.LastIndexOf('.');
            if (dot < 0) break;
            id = id.Substring(0, dot);
        }
        return null;
    }

    // True when this exact cue (no parent fallback) has a clip.
    public bool Has(string cueId)
    {
        var cue = Find(cueId);
        return cue != null && string.Equals(cue.id, cueId?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void OnValidate() { index = null; }
    private void OnEnable() { index = null; }
}
