// File responsibility: Imports sequence and line tables into dialogue assets and tracks a source fingerprint.
// Explicit references are preflighted before mutation; optional media may be absent. CSV is the text source.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DialogueCsvImporter
{
    private static string lastAutomaticAttempt;
    private static string SourceFingerprint()
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                "dialogue-schema-3\n" + File.ReadAllText(SequenceCsvPath) + "\nLINES\n" + File.ReadAllText(LineCsvPath))));
    }

    public static bool EnsureCurrent()
    {
        if (!File.Exists(SequenceCsvPath) || !File.Exists(LineCsvPath)) return false;
        // Wait for the full content import when related assets have not been created yet.
        if (AssetDatabase.LoadAssetAtPath<CustomerDatabase>(CustomerFolder + "/CustomerDatabase.asset") == null ||
            AssetDatabase.LoadAssetAtPath<RecipeDatabase>(RecipeFolder + "/RecipeDatabase.asset") == null) return false;
        string fingerprint = SourceFingerprint();
        var database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        if (database != null && database.importedCsvFingerprint == fingerprint) return true;
        if (lastAutomaticAttempt != fingerprint)
        {
            lastAutomaticAttempt = fingerprint;
            Debug.Log("Dialogue CSVs changed or imported dialogue is stale. Reimporting dialogue before play.");
            ImportDialogues();
        }
        database = AssetDatabase.LoadAssetAtPath<DialogueDatabase>(DatabasePath);
        return database != null && database.importedCsvFingerprint == fingerprint;
    }
    private static readonly string[] RequiredSequenceColumns =
    {
        "sequence_id",
        "display_name",
        "mode",
        "trigger",
        "repeat_rule",
        "priority",
        "random_weight",
        "customer_id",
        "min_day",
        "max_day",
        "min_customer_health",
        "max_customer_health",
        "min_customer_delight",
        "max_customer_delight",
        "required_recipe_id",
        "required_recipe_tiers",
        "min_served_meal_delight",
        "max_served_meal_delight",
        "required_flags",
        "forbidden_flags",
        "set_flags",
        "clear_flags",
        "approval_status",
        "review_notes"
    };

    private static readonly string[] RequiredLineColumns =
    {
        "sequence_id",
        "line_order",
        "speaker_id",
        "text_template",
        "claim_id",
        "portrait_path",
        "emotion"
    };

    private const string SequenceCsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_DialogueSequences.csv";

    private const string LineCsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_DialogueLines.csv";

    private const string OutputFolder =
        "Assets/GameData/Dialogues";

    private const string DatabasePath =
        OutputFolder + "/DialogueDatabase.asset";

    private const string CustomerFolder =
        "Assets/GameData/Customers";

    private const string RecipeFolder =
        "Assets/GameData/Recipes";

    private const string ClaimFolder =
        "Assets/GameData/Claims";

    [MenuItem("Tools/Blue Zone Bistro/Import Dialogue CSV")]
    public static void ImportDialogues()
    {
        if (!CsvImportUtility.TryRead(
                SequenceCsvPath,
                "Dialogue sequence",
                RequiredSequenceColumns,
                out List<List<string>> sequenceRows,
                out Dictionary<string, int> sequenceColumns) ||
            !CsvImportUtility.TryRead(
                LineCsvPath,
                "Dialogue line",
                RequiredLineColumns,
                out List<List<string>> lineRows,
                out Dictionary<string, int> lineColumns))
        {
            return;
        }

        // Resolve explicit references before touching assets: a typo must never turn
        // a character-specific conversation into an unrestricted/global one.
        if (!ValidateReferences(sequenceRows, sequenceColumns, lineRows, lineColumns)) return;
        CsvImportUtility.EnsureFolder(OutputFolder);

        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DialogueSequenceData> sequenceById =
            new Dictionary<string, DialogueSequenceData>(
                StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<int, DialogueLineData>> previousLines =
            new Dictionary<string, Dictionary<int, DialogueLineData>>(
                StringComparer.OrdinalIgnoreCase);
        List<DialogueSequenceData> imported =
            new List<DialogueSequenceData>();

        int created = 0;
        int updated = 0;
        int rejected = 0;

        for (int rowNumber = 1; rowNumber < sequenceRows.Count; rowNumber++)
        {
            List<string> row = sequenceRows[rowNumber];

            if (CsvImportUtility.IsBlankRow(row))
            {
                continue;
            }

            string id = CsvImportUtility.Get(
                row, sequenceColumns, "sequence_id").Trim();

            if (!CsvImportUtility.IsSafeId(id) || !idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: dialogue sequence ID '{id}' " +
                    "is invalid or duplicated.");
                rejected++;
                continue;
            }

            string[] requiredFlags = ParseFlagIds(
                CsvImportUtility.Get(row, sequenceColumns, "required_flags"),
                id,
                "required_flags");
            string[] forbiddenFlags = ParseFlagIds(
                CsvImportUtility.Get(row, sequenceColumns, "forbidden_flags"),
                id,
                "forbidden_flags");
            string[] setFlags = ParseFlagIds(
                CsvImportUtility.Get(row, sequenceColumns, "set_flags"),
                id,
                "set_flags");
            string[] clearFlags = ParseFlagIds(
                CsvImportUtility.Get(row, sequenceColumns, "clear_flags"),
                id,
                "clear_flags");

            if (HasOverlap(requiredFlags, forbiddenFlags) ||
                HasOverlap(setFlags, clearFlags))
            {
                Debug.LogError(
                    $"Dialogue {id}: the same flag cannot appear in both " +
                    "required/forbidden or set/clear columns.");
                rejected++;
                continue;
            }

            DialogueSequenceData sequence =
                CsvImportUtility.GetOrCreateAsset<DialogueSequenceData>(
                    $"{OutputFolder}/{id}.asset",
                    out bool isNew);

            previousLines[id] = (sequence.lines ??
                    Array.Empty<DialogueLineData>())
                .Where(line => line != null)
                .GroupBy(line => line.lineOrder)
                .ToDictionary(group => group.Key, group => group.First());

            sequence.id = id;
            sequence.displayName = CsvImportUtility.Get(
                row, sequenceColumns, "display_name").Trim();
            sequence.mode = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, sequenceColumns, "mode"),
                DialogueMode.Ambient,
                id,
                "mode");
            sequence.trigger = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, sequenceColumns, "trigger"),
                DialogueTrigger.CustomerArrival,
                id,
                "trigger");
            sequence.repeatRule = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, sequenceColumns, "repeat_rule"),
                DialogueRepeatRule.Repeatable,
                id,
                "repeat_rule");
            sequence.priority = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, sequenceColumns, "priority"),
                0,
                int.MinValue,
                int.MaxValue,
                id,
                "priority");
            sequence.randomWeight = CsvImportUtility.ParseFloat(
                CsvImportUtility.Get(row, sequenceColumns, "random_weight"),
                1f,
                0f,
                id,
                "random_weight");
            sequence.customer = LoadOptionalById<CustomerData>(
                CsvImportUtility.Get(row, sequenceColumns, "customer_id"),
                CustomerFolder,
                id,
                "customer");
            sequence.minDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, sequenceColumns, "min_day"),
                1,
                1,
                int.MaxValue,
                id,
                "min_day");
            sequence.maxDay = ParseOptionalInt(
                CsvImportUtility.Get(row, sequenceColumns, "max_day"),
                -1,
                1,
                int.MaxValue,
                id,
                "max_day");
            sequence.minCustomerHealth = ParseOptionalScore(
                row, sequenceColumns, "min_customer_health", id);
            sequence.maxCustomerHealth = ParseOptionalScore(
                row, sequenceColumns, "max_customer_health", id);
            sequence.minCustomerDelight = ParseOptionalScore(
                row, sequenceColumns, "min_customer_delight", id);
            sequence.maxCustomerDelight = ParseOptionalScore(
                row, sequenceColumns, "max_customer_delight", id);
            sequence.requiredRecipe = LoadOptionalById<RecipeData>(
                CsvImportUtility.Get(
                    row, sequenceColumns, "required_recipe_id"),
                RecipeFolder,
                id,
                "required recipe");
            sequence.recipeBuildCondition = !sequenceColumns.ContainsKey("recipe_build_condition") || string.IsNullOrWhiteSpace(CsvImportUtility.Get(row, sequenceColumns, "recipe_build_condition"))
                ? RecipeBuildCondition.Any : CsvImportUtility.ParseEnum(
                    CsvImportUtility.Get(row, sequenceColumns, "recipe_build_condition"), RecipeBuildCondition.Any, id, "recipe_build_condition");
            sequence.mealHealthCondition = !sequenceColumns.ContainsKey("meal_health_condition") || string.IsNullOrWhiteSpace(CsvImportUtility.Get(row, sequenceColumns, "meal_health_condition"))
                ? MealHealthCondition.Any : CsvImportUtility.ParseEnum(
                    CsvImportUtility.Get(row, sequenceColumns, "meal_health_condition"), MealHealthCondition.Any, id, "meal_health_condition");
            sequence.requiredRecipeTiers = ParseOptionalEnumList<RecipeTier>(
                CsvImportUtility.Get(
                    row, sequenceColumns, "required_recipe_tiers"),
                id,
                "required_recipe_tiers");
            sequence.minServedMealDelight = ParseOptionalScore(
                row, sequenceColumns, "min_served_meal_delight", id);
            sequence.maxServedMealDelight = ParseOptionalScore(
                row, sequenceColumns, "max_served_meal_delight", id);
            sequence.requiredFlags = requiredFlags;
            sequence.forbiddenFlags = forbiddenFlags;
            sequence.setFlags = setFlags;
            sequence.clearFlags = clearFlags;
            sequence.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(
                    row, sequenceColumns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            sequence.reviewNotes = CsvImportUtility.Get(
                row, sequenceColumns, "review_notes");

            if (!RangesAreValid(sequence))
            {
                Debug.LogError(
                    $"Dialogue {id}: one or more minimum values exceed " +
                    "their maximum values.");
                rejected++;
                continue;
            }

            EditorUtility.SetDirty(sequence);
            imported.Add(sequence);
            sequenceById.Add(id, sequence);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        Dictionary<string, List<DialogueLineData>> linesBySequence =
            sequenceById.Keys.ToDictionary(
                id => id,
                _ => new List<DialogueLineData>(),
                StringComparer.OrdinalIgnoreCase);
        HashSet<string> lineKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int importedLines = 0;
        int rejectedLines = 0;

        for (int rowNumber = 1; rowNumber < lineRows.Count; rowNumber++)
        {
            List<string> row = lineRows[rowNumber];

            if (CsvImportUtility.IsBlankRow(row))
            {
                continue;
            }

            string sequenceId = CsvImportUtility.Get(
                row, lineColumns, "sequence_id").Trim();

            if (!sequenceById.TryGetValue(
                    sequenceId,
                    out DialogueSequenceData sequence))
            {
                Debug.LogError(
                    $"Dialogue line row {rowNumber + 1}: sequence " +
                    $"'{sequenceId}' was not imported.");
                rejectedLines++;
                continue;
            }

            int lineOrder = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, lineColumns, "line_order"),
                -1,
                0,
                int.MaxValue,
                sequenceId,
                "line_order");
            string lineKey = $"{sequenceId}:{lineOrder}";

            if (lineOrder < 0 || !lineKeys.Add(lineKey))
            {
                Debug.LogError(
                    $"Dialogue line row {rowNumber + 1}: line order " +
                    $"'{lineOrder}' is invalid or duplicated for " +
                    $"'{sequenceId}'.");
                rejectedLines++;
                continue;
            }

            string textTemplate = CsvImportUtility.Get(
                row, lineColumns, "text_template");

            if (string.IsNullOrWhiteSpace(textTemplate))
            {
                Debug.LogError(
                    $"Dialogue line row {rowNumber + 1}: text is blank.");
                rejectedLines++;
                continue;
            }

            previousLines[sequenceId].TryGetValue(
                lineOrder,
                out DialogueLineData previousLine);

            DialogueLineData line = new DialogueLineData
            {
                lineOrder = lineOrder,
                speakerId = CsvImportUtility.Get(
                    row, lineColumns, "speaker_id").Trim(),
                textTemplate = textTemplate,
                claim = LoadOptionalById<ClaimData>(
                    CsvImportUtility.Get(row, lineColumns, "claim_id"),
                    ClaimFolder,
                    sequenceId,
                    "claim"),
                portrait = CsvImportUtility.LoadOptionalAsset(
                    CsvImportUtility.Get(
                        row, lineColumns, "portrait_path"),
                    previousLine?.portrait,
                    sequenceId,
                    "portrait_path"),
                emotion = CsvImportUtility.Get(
                    row, lineColumns, "emotion").Trim(),
                voiceOver = CsvImportUtility.LoadOptionalAsset(
                    lineColumns.ContainsKey("voice_over_path") ? CsvImportUtility.Get(row, lineColumns, "voice_over_path") : "",
                    previousLine?.voiceOver, sequenceId, "voice_over_path")
            };

            linesBySequence[sequenceId].Add(line);
            importedLines++;
        }

        foreach (DialogueSequenceData sequence in imported)
        {
            sequence.lines = linesBySequence[sequence.id]
                .OrderBy(line => line.lineOrder)
                .ToArray();

            if (sequence.lines.Length == 0)
            {
                Debug.LogWarning(
                    $"Dialogue {sequence.id} has no imported lines.");
            }

            EditorUtility.SetDirty(sequence);
        }

        DialogueDatabase database =
            CsvImportUtility.GetOrCreateAsset<DialogueDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported
            .OrderBy(sequence => sequence.id)
            .ToList());
        database.importedCsvFingerprint = rejected == 0 && rejectedLines == 0 && imported.All(s => s.lines.Length > 0)
            ? SourceFingerprint() : string.Empty;
        EditorUtility.SetDirty(database);

        CsvImportUtility.SaveAndReport(
            "Dialogue sequence", created, updated, rejected);
        Debug.Log(
            $"Dialogue line import finished. Imported: {importedLines}, " +
            $"rejected: {rejectedLines}.");
    }

    private static T LoadOptionalById<T>(
        string rawId,
        string folder,
        string ownerId,
        string relationName) where T : UnityEngine.Object
    {
        string id = rawId?.Trim();

        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>($"{folder}/{id}.asset");

        if (asset == null)
        {
            Debug.LogWarning(
                $"{ownerId}: {relationName} ID '{id}' was not found.");
        }

        return asset;
    }

    private static int ParseOptionalScore(
        List<string> row,
        Dictionary<string, int> columns,
        string columnName,
        string ownerId)
    {
        return ParseOptionalInt(
            CsvImportUtility.Get(row, columns, columnName),
            -1,
            0,
            100,
            ownerId,
            columnName);
    }

    private static int ParseOptionalInt(
        string text,
        int fallback,
        int minimum,
        int maximum,
        string ownerId,
        string columnName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return CsvImportUtility.ParseInt(
            text,
            fallback,
            minimum,
            maximum,
            ownerId,
            columnName);
    }

    private static T[] ParseOptionalEnumList<T>(
        string text,
        string ownerId,
        string columnName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<T>();
        }

        List<T> values = new List<T>();

        foreach (string part in text.Split('|'))
        {
            if (Enum.TryParse(part.Trim(), true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                if (!values.Contains(parsed))
                {
                    values.Add(parsed);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"{ownerId}: '{part}' is not a valid " +
                    $"{columnName} value.");
            }
        }

        return values.ToArray();
    }

    private static string[] ParseFlagIds(
        string text,
        string ownerId,
        string columnName)
    {
        List<string> valid = new List<string>();

        foreach (string flagId in CsvImportUtility.SplitIds(text))
        {
            if (!CsvImportUtility.IsSafeId(flagId))
            {
                Debug.LogWarning(
                    $"{ownerId}: flag ID '{flagId}' in {columnName} is " +
                    "invalid and was ignored.");
                continue;
            }

            valid.Add(flagId);
        }

        return valid.ToArray();
    }

    private static bool HasOverlap(
        IEnumerable<string> left,
        IEnumerable<string> right)
    {
        HashSet<string> values = new HashSet<string>(
            left,
            StringComparer.OrdinalIgnoreCase);
        return right.Any(values.Contains);
    }

    private static bool RangesAreValid(DialogueSequenceData sequence)
    {
        return RangeIsValid(sequence.minDay, sequence.maxDay) &&
               RangeIsValid(
                   sequence.minCustomerHealth,
                   sequence.maxCustomerHealth) &&
               RangeIsValid(
                   sequence.minCustomerDelight,
                   sequence.maxCustomerDelight) &&
               RangeIsValid(
                   sequence.minServedMealDelight,
                   sequence.maxServedMealDelight);
    }

    private static bool ValidateReferences(List<List<string>> sequences, Dictionary<string, int> sequenceColumns,
        List<List<string>> lines, Dictionary<string, int> lineColumns)
    {
        bool valid = true;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sequences.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r)))
        {
            string id = CsvImportUtility.Get(row, sequenceColumns, "sequence_id").Trim();
            if (!CsvImportUtility.IsSafeId(id) || !ids.Add(id))
            { Debug.LogError($"Invalid or duplicate dialogue ID '{id}'. Import cancelled."); valid = false; }
            valid &= ReferenceExists<CustomerData>(row, sequenceColumns, "customer_id", CustomerFolder, id);
            valid &= ReferenceExists<RecipeData>(row, sequenceColumns, "required_recipe_id", RecipeFolder, id);
            valid &= EnumIsValid<DialogueMode>(row, sequenceColumns, "mode", id);
            valid &= EnumIsValid<DialogueTrigger>(row, sequenceColumns, "trigger", id);
            valid &= EnumIsValid<DialogueRepeatRule>(row, sequenceColumns, "repeat_rule", id);
            valid &= EnumIsValid<RecipeBuildCondition>(row, sequenceColumns, "recipe_build_condition", id);
            valid &= EnumIsValid<MealHealthCondition>(row, sequenceColumns, "meal_health_condition", id);
        }
        foreach (var row in lines.Skip(1).Where(r => !CsvImportUtility.IsBlankRow(r)))
        {
            string id = CsvImportUtility.Get(row, lineColumns, "sequence_id").Trim();
            if (!ids.Contains(id))
            { Debug.LogError($"Dialogue line references unknown sequence '{id}'. Import cancelled."); valid = false; }
            valid &= ReferenceExists<ClaimData>(row, lineColumns, "claim_id", ClaimFolder, id);
        }
        return valid;
    }

    private static bool ReferenceExists<T>(List<string> row, Dictionary<string, int> columns,
        string column, string folder, string owner) where T : UnityEngine.Object
    {
        string id = CsvImportUtility.Get(row, columns, column).Trim();
        if (id.Length == 0 || (CsvImportUtility.IsSafeId(id) &&
            AssetDatabase.LoadAssetAtPath<T>($"{folder}/{id}.asset") != null)) return true;
        Debug.LogError($"{owner}: {column} '{id}' cannot be resolved. Import cancelled; existing dialogue assets preserved.");
        return false;
    }

    private static bool EnumIsValid<T>(List<string> row, Dictionary<string, int> columns,
        string column, string owner) where T : struct, Enum
    {
        if (!columns.ContainsKey(column)) return true; // Older CSVs may omit optional condition columns.
        string raw = CsvImportUtility.Get(row, columns, column).Trim();
        if (raw.Length == 0 || (Enum.TryParse(raw, true, out T value) && Enum.IsDefined(typeof(T), value))) return true;
        Debug.LogError($"{owner}: invalid {column} '{raw}'. Import cancelled.");
        return false;
    }

    private static bool RangeIsValid(int minimum, int maximum)
    {
        return minimum < 0 || maximum < 0 || minimum <= maximum;
    }
}
