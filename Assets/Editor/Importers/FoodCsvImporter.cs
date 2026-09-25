// File responsibility: Editor-only food CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
// structure:  Find food assets: if already exist update, if not exist create it.
// Parse, validate, convert, actuall create or update, save and report.
// Usage: Go to unity, Tools -> BlueZoneBistro -> Import Food CSV.
// Behavior summary output in console for debuging.
public static class FoodCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "id",
        "display_name",
        "recipe_category",
        "daily_dozen_categories",
        "traffic_light",
        "marketing_label",
        "ingredient_list",
        "ingredient_count",
        "storage_type",
        "claim_id",
        "icon_path",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Foods.csv";

    private const string OutputFolder =
        "Assets/GameData/Foods";

    private const string DatabasePath =
        OutputFolder + "/FoodDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Foods CSV")]
    public static void ImportFoods()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"Food CSV was not found at: {CsvPath}");
            return;
        }

        EnsureFolder("Assets/GameData");
        EnsureFolder("Assets/GameData/CSV");
        EnsureFolder(OutputFolder);

        List<List<string>> rows = ParseCsv(File.ReadAllText(CsvPath));

        if (rows.Count < 2)
        {
            Debug.LogError("The food CSV has no data rows.");
            return;
        }

        Dictionary<string, int> columns = BuildColumnMap(rows[0]);

        string[] missingColumns = RequiredColumns
            .Where(column => !columns.ContainsKey(column))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            Debug.LogError(
                "Food CSV is missing required columns: " +
                string.Join(", ", missingColumns));
            return;
        }

        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        // This list becomes the serialized contents of FoodDatabase.asset.
        List<FoodData> importedFoods = new List<FoodData>();

        int created = 0;
        int updated = 0;
        int rejected = 0;

        for (int rowNumber = 1; rowNumber < rows.Count; rowNumber++)
        {
            List<string> row = rows[rowNumber];

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string id = Get(row, columns, "id").Trim();

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"Row {rowNumber + 1}: missing food ID.");
                rejected++;
                continue;
            }

            if (!IsSafeId(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: ID '{id}' contains invalid " +
                    "characters. Use letters, numbers, underscores or hyphens.");
                rejected++;
                continue;
            }

            if (!idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: duplicate food ID '{id}'.");
                rejected++;
                continue;
            }

            string assetPath = $"{OutputFolder}/{id}.asset";
            FoodData food = AssetDatabase.LoadAssetAtPath<FoodData>(assetPath);
            bool isNew = food == null;

            if (isNew)
            {
                food = ScriptableObject.CreateInstance<FoodData>();
                AssetDatabase.CreateAsset(food, assetPath);
            }

            food.id = id;
            food.displayName = Get(row, columns, "display_name").Trim();
            food.recipeCategory = ParseEnum(
                Get(row, columns, "recipe_category"),
                FoodCategory.Unassigned,
                id,
                "recipe_category");

            food.dailyDozenCategories = ParseEnumList(
                Get(row, columns, "daily_dozen_categories"),
                DailyDozenCategory.None,
                id,
                "daily_dozen_categories");

            food.trafficLight = ParseEnum(
                Get(row, columns, "traffic_light"),
                TrafficLight.Unassigned,
                id,
                "traffic_light");

            food.marketingLabel = Get(row, columns, "marketing_label");
            food.ingredientList = Get(row, columns, "ingredient_list");
            food.ingredientCount = ParseNonNegativeInt(
                Get(row, columns, "ingredient_count"), id);

            food.storageType = ParseEnum(
                Get(row, columns, "storage_type"),
                StorageType.Unassigned,
                id,
                "storage_type");

            // This stays as an ID until the Claims importer exists.
            food.claimId = Get(row, columns, "claim_id").Trim();

            food.approvalStatus = ParseEnum(
                Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");

            food.reviewNotes = Get(row, columns, "review_notes");

            // A blank path preserves any sprite assigned manually in Unity.
            string iconPath = Get(row, columns, "icon_path").Trim();
            if (!string.IsNullOrEmpty(iconPath))
            {
                Sprite importedIcon =
                    AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

                if (importedIcon == null)
                {
                    Debug.LogWarning(
                        $"Food {id}: no Sprite found at '{iconPath}'. " +
                        "The existing icon was preserved.");
                }
                else
                {
                    food.icon = importedIcon;
                }
            }

            EditorUtility.SetDirty(food);
            importedFoods.Add(food);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        // Keep database order deterministic even if CSV rows are rearranged.
        importedFoods.Sort((left, right) =>
            string.Compare(
                left.id,
                right.id,
                StringComparison.OrdinalIgnoreCase));

        // Create the database asset on the first import, then update the same
        // asset on every later import.
        FoodDatabase database =
            AssetDatabase.LoadAssetAtPath<FoodDatabase>(DatabasePath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<FoodDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.SetItems(importedFoods);
        var priceDatabase = AssetDatabase.LoadAssetAtPath<StoreListingDatabase>("Assets/GameData/Prices/StoreListingDatabase.asset");
        database.RebuildMinimumPrices(priceDatabase == null ? null : priceDatabase.Listings);
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Food import finished. Created: {created}, " +
            $"updated: {updated}, rejected: {rejected}.");
    }

    private static Dictionary<string, int> BuildColumnMap(
        List<string> headerRow)
    {
        Dictionary<string, int> columns = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headerRow.Count; i++)
        {
            string header = headerRow[i]
                .Trim()
                .TrimStart('\uFEFF');

            if (!string.IsNullOrEmpty(header))
            {
                columns[header] = i;
            }
        }

        return columns;
    }

    private static string Get(
        List<string> row,
        Dictionary<string, int> columns,
        string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index))
        {
            throw new InvalidDataException(
                $"Required CSV column '{columnName}' is missing.");
        }

        return index < row.Count ? row[index] : string.Empty;
    }

    private static T ParseEnum<T>(
        string text,
        T fallback,
        string foodId,
        string columnName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (Enum.TryParse(text.Trim(), true, out T result))
        {
            return result;
        }

        Debug.LogWarning(
            $"Food {foodId}: '{text}' is not a valid {columnName}. " +
            $"Using {fallback}.");
        return fallback;
    }

    private static T[] ParseEnumList<T>(
        string text,
        T fallback,
        string foodId,
        string columnName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { fallback };
        }

        List<T> values = new List<T>();

        foreach (string part in text.Split('|'))
        {
            if (Enum.TryParse(part.Trim(), true, out T parsed))
            {
                values.Add(parsed);
            }
            else
            {
                Debug.LogWarning(
                    $"Food {foodId}: '{part}' is not a valid " +
                    $"{columnName} value.");
            }
        }

        return values.Count > 0 ? values.ToArray() : new[] { fallback };
    }

    private static int ParseNonNegativeInt(string text, string foodId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (int.TryParse(text, out int result) && result >= 0)
        {
            return result;
        }

        Debug.LogWarning(
            $"Food {foodId}: ingredient_count '{text}' is invalid. " +
            "Using 0 (unknown).");
        return 0;
    }

    private static bool IsSafeId(string id)
    {
        return id.All(character =>
            char.IsLetterOrDigit(character) ||
            character == '_' ||
            character == '-');
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (string.IsNullOrEmpty(parent))
        {
            throw new InvalidDataException(
                $"Cannot determine parent folder for '{path}'.");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    insideQuotes = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;

                default:
                    field.Append(character);
                    break;
            }
        }

        if (insideQuotes)
        {
            throw new InvalidDataException(
                "CSV ended inside a quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
