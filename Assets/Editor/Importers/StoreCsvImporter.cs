// File responsibility: Editor-only store CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoreCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "store_id",
        "display_name",
        "role_description",
        "description",
        "unlock_day",
        "requires_membership",
        "membership_cost",
        "default_price_display_style",
        "shelf_definitions",
        "exterior_icon_path",
        "shop_background_path",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Stores.csv";

    private const string OutputFolder =
        "Assets/GameData/Stores";

    private const string DatabasePath =
        OutputFolder + "/StoreDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Stores CSV")]
    public static void ImportStores()
    {
        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Store",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        CsvImportUtility.EnsureFolder(OutputFolder);

        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<StoreData> imported = new List<StoreData>();
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
                row, columns, "store_id").Trim();

            if (!ValidateId(id, rowNumber, idsSeen))
            {
                rejected++;
                continue;
            }

            string assetPath = $"{OutputFolder}/{id}.asset";
            StoreData store = CsvImportUtility.GetOrCreateAsset<StoreData>(
                assetPath,
                out bool isNew);

            store.id = id;
            store.displayName = CsvImportUtility.Get(
                row, columns, "display_name").Trim();
            store.roleDescription = CsvImportUtility.Get(
                row, columns, "role_description");
            store.description = CsvImportUtility.Get(
                row, columns, "description");
            store.unlockDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "unlock_day"),
                1, 1, int.MaxValue, id, "unlock_day");
            store.requiresMembership = CsvImportUtility.ParseBool(
                CsvImportUtility.Get(row, columns, "requires_membership"),
                false, id, "requires_membership");
            store.membershipCost = CsvImportUtility.ParseFloat(
                CsvImportUtility.Get(row, columns, "membership_cost"),
                0f, 0f, id, "membership_cost");
            store.defaultPriceDisplayStyle = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(
                    row, columns, "default_price_display_style"),
                PriceDisplayStyle.Standard,
                id,
                "default_price_display_style");
            store.shelves = ParseShelves(
                CsvImportUtility.Get(row, columns, "shelf_definitions"),
                store.shelves,
                id);
            store.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            store.reviewNotes = CsvImportUtility.Get(
                row, columns, "review_notes");
            store.exteriorIcon = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "exterior_icon_path"),
                store.exteriorIcon,
                id,
                "exterior_icon_path");
            store.shopBackground = CsvImportUtility.LoadOptionalAsset(
                CsvImportUtility.Get(row, columns, "shop_background_path"),
                store.shopBackground,
                id,
                "shop_background_path");

            EditorUtility.SetDirty(store);
            imported.Add(store);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        StoreDatabase database =
            CsvImportUtility.GetOrCreateAsset<StoreDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported.OrderBy(store => store.id).ToList());
        EditorUtility.SetDirty(database);

        CsvImportUtility.SaveAndReport(
            "Store", created, updated, rejected);
    }

    private static bool ValidateId(
        string id,
        int rowNumber,
        HashSet<string> idsSeen)
    {
        if (!CsvImportUtility.IsSafeId(id))
        {
            Debug.LogError(
                $"Row {rowNumber + 1}: store ID '{id}' is missing or " +
                "contains invalid characters.");
            return false;
        }

        if (!idsSeen.Add(id))
        {
            Debug.LogError(
                $"Row {rowNumber + 1}: duplicate store ID '{id}'.");
            return false;
        }

        return true;
    }

    private static StoreShelfDefinition[] ParseShelves(
        string text,
        StoreShelfDefinition[] existing,
        string storeId)
    {
        Dictionary<string, StoreShelfDefinition> existingById =
            (existing ?? Array.Empty<StoreShelfDefinition>())
            .Where(shelf =>
                shelf != null && !string.IsNullOrWhiteSpace(shelf.id))
            .GroupBy(shelf => shelf.id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        List<StoreShelfDefinition> result =
            new List<StoreShelfDefinition>();
        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string definition in text.Split('|'))
        {
            if (string.IsNullOrWhiteSpace(definition))
            {
                continue;
            }

            string[] parts = definition.Split(new[] { ':' }, 2);
            string shelfId = parts[0].Trim();
            string displayName = parts.Length > 1
                ? parts[1].Trim()
                : shelfId;

            if (!CsvImportUtility.IsSafeId(shelfId))
            {
                Debug.LogWarning(
                    $"Store {storeId}: shelf ID '{shelfId}' is invalid.");
                continue;
            }

            if (!idsSeen.Add(shelfId))
            {
                Debug.LogWarning(
                    $"Store {storeId}: duplicate shelf ID '{shelfId}'.");
                continue;
            }

            StoreShelfDefinition shelf =
                existingById.TryGetValue(
                    shelfId,
                    out StoreShelfDefinition oldShelf)
                    ? oldShelf
                    : new StoreShelfDefinition();

            shelf.id = shelfId;
            shelf.displayName = displayName;
            result.Add(shelf);
        }

        return result.ToArray();
    }
}
