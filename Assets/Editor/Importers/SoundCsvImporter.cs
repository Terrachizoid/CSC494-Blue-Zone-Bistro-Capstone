// File responsibility: Editor importer for Blue_Zone_Bistro_Sounds.csv -> SoundDatabase.
// Columns: cue_id, kind (Sfx/Music), clip_paths (| separated variants), volume (0-1), pitch_variation (0-0.5),
// source, license, notes. Missing files are allowed (that cue stays silent); one summary line lists them.
// source/license are for ASSET_LICENSES.md bookkeeping only (CC0 or a paid commercial licence; see Data/Sound.md).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SoundCsvImporter
{
    public const string CsvPath = "Assets/GameData/CSV/Blue_Zone_Bistro_Sounds.csv";
    public const string AssetPath = "Assets/GameData/Audio/SoundDatabase.asset";
    private static readonly string[] RequiredColumns = { "cue_id", "kind", "clip_paths" };

    [MenuItem("Tools/Blue Zone Bistro/Import Sounds CSV")]
    public static void Import()
    {
        if (!File.Exists(CsvPath)) { Debug.LogWarning("No sounds CSV at " + CsvPath + "."); return; }
        if (!CsvImportUtility.TryRead(CsvPath, "Sound", RequiredColumns, out List<List<string>> rows, out Dictionary<string, int> columns))
            return;
        CsvImportUtility.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
        var database = CsvImportUtility.GetOrCreateAsset<SoundDatabase>(AssetPath, out bool isNew);
        var imported = new List<SoundCue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        int rejected = 0;
        string Column(List<string> row, string name) => columns.ContainsKey(name) ? CsvImportUtility.Get(row, columns, name).Trim() : "";

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (CsvImportUtility.IsBlankRow(row)) continue;
            string id = Column(row, "cue_id");
            if (id.Length == 0 || !seen.Add(id))
            {
                Debug.LogError($"Sounds row {r + 1}: cue '{id}' is empty or duplicated.");
                rejected++;
                continue;
            }
            var clips = new List<AudioClip>();
            foreach (string path in CsvImportUtility.SplitIds(Column(row, "clip_paths")))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip); else missing.Add(path);
            }
            imported.Add(new SoundCue
            {
                id = id,
                kind = CsvImportUtility.ParseEnum(Column(row, "kind"), SoundKind.Sfx, id, "kind"),
                clips = clips.ToArray(),
                volume = Mathf.Clamp01(CsvImportUtility.ParseFloat(Column(row, "volume"), 1f, 0f, id, "volume")),
                pitchVariation = Mathf.Clamp(CsvImportUtility.ParseFloat(Column(row, "pitch_variation"), 0f, 0f, id, "pitch_variation"), 0, .5f)
            });
        }
        database.cues = imported;
        EditorUtility.SetDirty(database);
        int silent = imported.Count(c => !c.HasClips);
        if (silent > 0 || missing.Count > 0)
            Debug.Log($"Sounds: {silent} of {imported.Count} cue(s) have no clip yet (they stay silent or use their parent cue)." +
                (missing.Count > 0 ? $" Missing files: {string.Join(", ", missing.Take(4))}{(missing.Count > 4 ? "..." : "")}" : ""));
        CsvImportUtility.SaveAndReport("Sound", isNew ? imported.Count : 0, isNew ? 0 : imported.Count, rejected);
    }
}
