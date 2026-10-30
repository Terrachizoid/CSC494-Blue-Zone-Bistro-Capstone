// File responsibility: Editor-only import of per-category art (Categories.csv) into the FoodDatabase.
// Run after the Foods import. A blank icon_path keeps a sprite assigned in the Inspector.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CategoryCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "category_id",
        "icon_path"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Categories.csv";

    private const string FoodDatabasePath =
        "Assets/GameData/Foods/FoodDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Categories CSV")]
    public static void ImportCategories()
    {
        FoodDatabase database =
            AssetDatabase.LoadAssetAtPath<FoodDatabase>(FoodDatabasePath);

        if (database == null)
        {
            Debug.LogError(
                "Category import needs the food database. Run Import Foods CSV first.");
            return;
        }

        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Category",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        var result = new List<FoodDatabase.CategoryArt>();
        var seen = new HashSet<FoodCategory>();
        int rejected = 0;

        for (int rowNumber = 1; rowNumber < rows.Count; rowNumber++)
        {
            List<string> row = rows[rowNumber];

            if (CsvImportUtility.IsBlankRow(row))
            {
                continue;
            }

            string id = CsvImportUtility.Get(row, columns, "category_id").Trim();

            if (!Enum.TryParse(id, true, out FoodCategory category) ||
                category == FoodCategory.Unassigned)
            {
                Debug.LogError($"Row {rowNumber + 1}: '{id}' is not a food category.");
                rejected++;
                continue;
            }

            if (!seen.Add(category))
            {
                Debug.LogError($"Row {rowNumber + 1}: duplicate category '{id}'.");
                rejected++;
                continue;
            }

            result.Add(new FoodDatabase.CategoryArt
            {
                category = category,
                icon = CsvImportUtility.LoadOptionalAsset(
                    CsvImportUtility.Get(row, columns, "icon_path"),
                    database.CategoryIcon(category),
                    id,
                    "icon_path")
            });
        }

        database.SetCategoryArt(result);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log($"Category import finished. Imported: {result.Count}, rejected: {rejected}.");
    }
}
