// File responsibility: Editor-only recipe CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RecipeCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "recipe_id",
        "display_name",
        "region_label",
        "required_slots",
        "optional_slots",
        "unlock_day",
        "tier",
        "base_sale_price",
        "delight_value",
        "craving_tags",
        "claim_ids",
        "allow_non_green_choices",
        "icon_path",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Recipes.csv";

    private const string OutputFolder =
        "Assets/GameData/Recipes";

    private const string ClaimFolder =
        "Assets/GameData/Claims";

    private const string DatabasePath =
        OutputFolder + "/RecipeDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Recipes CSV")]
    public static void ImportRecipes()
    {
        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Recipe",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        CsvImportUtility.EnsureFolder(OutputFolder);
        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<RecipeData> imported = new List<RecipeData>();
        int created = 0;
        int updated = 0;
        int rejected = 0;

        for (int rowNumber = 1; rowNumber < rows.Count; rowNumber++)
        {
            List<string> row = rows[rowNumber];

            if (CsvImportUtility.IsBlankRow(row))
            {
                continue;
            }

            string id = CsvImportUtility.Get(
                row, columns, "recipe_id").Trim();

            if (!CsvImportUtility.IsSafeId(id) || !idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: recipe ID '{id}' is invalid " +
                    "or duplicated.");
                rejected++;
                continue;
            }

            List<RecipeSlot> slots = new List<RecipeSlot>();
            bool requiredSlotsValid = TryParseSlots(
                CsvImportUtility.Get(row, columns, "required_slots"),
                false,
                id,
                slots);
            bool optionalSlotsValid = TryParseSlots(
                CsvImportUtility.Get(row, columns, "optional_slots"),
                true,
                id,
                slots);

            if (!requiredSlotsValid || !optionalSlotsValid ||
                slots.All(slot => slot.optional))
            {
                Debug.LogError(
                    $"Recipe {id}: at least one valid required slot is needed.");
                rejected++;
                continue;
            }

            RecipeData recipe =
                CsvImportUtility.GetOrCreateAsset<RecipeData>(
                    $"{OutputFolder}/{id}.asset",
                    out bool isNew);

            recipe.id = id;
            recipe.displayName = CsvImportUtility.Get(
                row, columns, "display_name").Trim();
            recipe.regionLabel = CsvImportUtility.Get(
                row, columns, "region_label").Trim();
            recipe.slots = slots.ToArray();
            recipe.unlockDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "unlock_day"),
                1, 1, int.MaxValue, id, "unlock_day");
            recipe.tier = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "tier"),
                RecipeTier.Basic,
                id,
                "tier");
            recipe.baseSalePrice = CsvImportUtility.ParseFloat(
                CsvImportUtility.Get(row, columns, "base_sale_price"),
                0f, 0f, id, "base_sale_price");
            recipe.delightValue = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "delight_value"),
                0, 0, int.MaxValue, id, "delight_value");
            recipe.cravingTags = CsvImportUtility.ParseEnumList(
                CsvImportUtility.Get(row, columns, "craving_tags"),
                CravingTag.None,
                id,
                "craving_tags");
            recipe.claims = CsvImportUtility.LoadAssetsByIds<ClaimData>(
                CsvImportUtility.Get(row, columns, "claim_ids"),
                ClaimFolder,
                id,
                "claim");
            recipe.allowNonGreenChoices = CsvImportUtility.ParseBool(
                CsvImportUtility.Get(
                    row, columns, "allow_non_green_choices"),
                true,
                id,
                "allow_non_green_choices");
            recipe.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            recipe.reviewNotes = CsvImportUtility.Get(
                row, columns, "review_notes");
            recipe.icon = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "icon_path"),
                recipe.icon,
                id,
                "icon_path");

            EditorUtility.SetDirty(recipe);
            imported.Add(recipe);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        RecipeDatabase database =
            CsvImportUtility.GetOrCreateAsset<RecipeDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported.OrderBy(recipe => recipe.id).ToList());
        EditorUtility.SetDirty(database);

        CsvImportUtility.SaveAndReport(
            "Recipe", created, updated, rejected);
    }

    private static bool TryParseSlots(
        string text,
        bool optional,
        string recipeId,
        List<RecipeSlot> destination)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return optional;
        }

        bool valid = true;

        foreach (string definition in text.Split('|'))
        {
            string[] parts = definition.Split(':');

            if (parts.Length != 2 ||
                !Enum.TryParse(
                    parts[0].Trim(),
                    true,
                    out FoodCategory category) ||
                category == FoodCategory.Unassigned ||
                !int.TryParse(parts[1].Trim(), out int servings) ||
                servings < 1)
            {
                Debug.LogWarning(
                    $"Recipe {recipeId}: slot '{definition}' is invalid. " +
                    "Use Category:Count, for example Grain:2.");
                valid = false;
                continue;
            }

            destination.Add(new RecipeSlot
            {
                category = category,
                servings = servings,
                optional = optional
            });
        }

        return valid;
    }
}
