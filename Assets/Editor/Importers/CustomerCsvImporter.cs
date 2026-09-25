// File responsibility: Editor-only customer CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CustomerCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "customer_id",
        "display_name",
        "customer_type",
        "unlock_day",
        "starting_health",
        "starting_prosperity",
        "theme_from_plan",
        "preferred_recipe_ids",
        "craving_tags",
        "dialogue_claim_ids",
        "portrait_path",
        "sprite_path",
        "dialogue_asset_path",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Customers.csv";

    private const string OutputFolder =
        "Assets/GameData/Customers";

    private const string RecipeFolder =
        "Assets/GameData/Recipes";

    private const string ClaimFolder =
        "Assets/GameData/Claims";

    private const string DatabasePath =
        OutputFolder + "/CustomerDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Customers CSV")]
    public static void ImportCustomers()
    {
        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Customer",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        CsvImportUtility.EnsureFolder(OutputFolder);
        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<CustomerData> imported = new List<CustomerData>();
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
                row, columns, "customer_id").Trim();

            if (!CsvImportUtility.IsSafeId(id) || !idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: customer ID '{id}' is invalid " +
                    "or duplicated.");
                rejected++;
                continue;
            }

            CustomerData customer =
                CsvImportUtility.GetOrCreateAsset<CustomerData>(
                    $"{OutputFolder}/{id}.asset",
                    out bool isNew);

            customer.id = id;
            customer.displayName = CsvImportUtility.Get(
                row, columns, "display_name").Trim();
            customer.customerType = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "customer_type"),
                CustomerType.Regular,
                id,
                "customer_type");
            customer.unlockDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "unlock_day"),
                1, 1, int.MaxValue, id, "unlock_day");
            customer.startingHealth = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "starting_health"),
                50, 0, 100, id, "starting_health");
            customer.startingProsperity = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "starting_prosperity"),
                0, 0, int.MaxValue, id, "starting_prosperity");
            customer.themeFromPlan = CsvImportUtility.Get(
                row, columns, "theme_from_plan");
            customer.preferredRecipes =
                CsvImportUtility.LoadAssetsByIds<RecipeData>(
                    CsvImportUtility.Get(
                        row, columns, "preferred_recipe_ids"),
                    RecipeFolder,
                    id,
                    "preferred recipe");
            customer.cravingTags = CsvImportUtility.ParseEnumList(
                CsvImportUtility.Get(row, columns, "craving_tags"),
                CravingTag.None,
                id,
                "craving_tags");
            customer.dialogueClaims =
                CsvImportUtility.LoadAssetsByIds<ClaimData>(
                    CsvImportUtility.Get(
                        row, columns, "dialogue_claim_ids"),
                    ClaimFolder,
                    id,
                    "dialogue claim");
            customer.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            customer.reviewNotes = CsvImportUtility.Get(
                row, columns, "review_notes");
            customer.portrait = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "portrait_path"),
                customer.portrait,
                id,
                "portrait_path");
            customer.customerSprite = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "sprite_path"),
                customer.customerSprite,
                id,
                "sprite_path");
            customer.dialogueAsset = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "dialogue_asset_path"),
                customer.dialogueAsset,
                id,
                "dialogue_asset_path");

            EditorUtility.SetDirty(customer);
            imported.Add(customer);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        CustomerDatabase database =
            CsvImportUtility.GetOrCreateAsset<CustomerDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported.OrderBy(customer => customer.id).ToList());
        EditorUtility.SetDirty(database);

        CsvImportUtility.SaveAndReport(
            "Customer", created, updated, rejected);
    }
}
