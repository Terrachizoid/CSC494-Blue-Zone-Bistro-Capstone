// File responsibility: Editor importer for Blue_Zone_Bistro_CharacterExpressions.csv -> CharacterExpressionDatabase.
// Columns: expression_id (e.g. U001_happy; blank = character_expression), character_id, expression, emotions (| separated), image_path, part_paths (| separated, back to front),
// video_path (relative to Assets/StreamingAssets), tint (#RRGGBB, silhouette colour), notes.
// character_id "Default" rows are the base for every character without art (see CharacterExpressionDatabase.DefaultId). Missing images are allowed: the row is kept and the game
// shows the customer's old portrait or the silhouette until the art exists. One summary line lists what is missing.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterExpressionCsvImporter
{
    public const string CsvPath = "Assets/GameData/CSV/Blue_Zone_Bistro_CharacterExpressions.csv";
    public const string AssetPath = "Assets/GameData/Characters/CharacterExpressionDatabase.asset";
    private static readonly string[] RequiredColumns = { "character_id", "expression", "image_path" };

    [MenuItem("Tools/Blue Zone Bistro/Import Character Expressions CSV")]
    public static void Import()
    {
        if (!File.Exists(CsvPath)) { Debug.LogWarning("No character expressions CSV at " + CsvPath + "."); return; }
        if (!CsvImportUtility.TryRead(CsvPath, "Character expression", RequiredColumns, out List<List<string>> rows, out Dictionary<string, int> columns))
            return;
        CsvImportUtility.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
        var database = CsvImportUtility.GetOrCreateAsset<CharacterExpressionDatabase>(AssetPath, out bool isNew);
        var imported = new List<CharacterExpression>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        int rejected = 0;
        string Column(List<string> row, string name) => columns.ContainsKey(name) ? CsvImportUtility.Get(row, columns, name).Trim() : "";

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (CsvImportUtility.IsBlankRow(row)) continue;
            string character = Column(row, "character_id"), expression = Column(row, "expression").ToLowerInvariant();
            string id = Column(row, "expression_id");
            if (id.Length == 0) id = character.Replace(" ", "") + "_" + expression;
            if (character.Length == 0 || expression.Length == 0 || !seen.Add(character + "/" + expression) || !seen.Add("id:" + id))
            {
                Debug.LogError($"Character expressions row {r + 1}: '{id}' ({character}/{expression}) is empty or duplicated.");
                rejected++;
                continue;
            }
            var entry = new CharacterExpression
            {
                id = id,
                characterId = character,
                expression = expression,
                emotions = CsvImportUtility.SplitIds(Column(row, "emotions")),
                image = LoadSprite(Column(row, "image_path"), missing),
                parts = CsvImportUtility.SplitIds(Column(row, "part_paths")).Select(p => LoadSprite(p, missing)).Where(s => s != null).ToArray(),
                videoPath = Column(row, "video_path").Replace('\\', '/'),
                tint = ColorUtility.TryParseHtmlString(Column(row, "tint"), out Color parsedTint) ? parsedTint : Color.clear
            };
            if (entry.videoPath.Length > 0 && !File.Exists(Path.Combine(Application.streamingAssetsPath, entry.videoPath)))
                missing.Add("Assets/StreamingAssets/" + entry.videoPath);
            imported.Add(entry);
        }
        database.expressions = imported;
        EditorUtility.SetDirty(database);
        if (missing.Count > 0)
            Debug.Log($"Character expressions: {missing.Count} image/video file(s) not made yet (the game shows the plain portrait instead). First: {string.Join(", ", missing.Take(3))}");
        CsvImportUtility.SaveAndReport("Character expression", isNew ? imported.Count : 0, isNew ? 0 : imported.Count, rejected);
    }

    private static Sprite LoadSprite(string path, List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Trim());
        if (sprite == null) missing.Add(path.Trim());
        return sprite;
    }
}
