// File responsibility: Editor-only storelisting CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoreListingCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "listing_id",
        "food_id",
        "store_id",
        "shelf_id",
        "unlock_day",
        "required_flag",
        "observed_price_cad",
        "servings_per_package",
        "game_price",
        "price_source",
        "price_display_style",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Prices.csv";

    private const string OutputFolder =
        "Assets/GameData/Prices";

    private const string FoodFolder =
        "Assets/GameData/Foods";

    private const string StoreFolder =
        "Assets/GameData/Stores";

    private const string DatabasePath =
        OutputFolder + "/StoreListingDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Prices CSV")]
    public static void ImportPrices()
    {
        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Price listing",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        CsvImportUtility.EnsureFolder(OutputFolder);
        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<StoreListingData> imported =
            new List<StoreListingData>();
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
                row, columns, "listing_id").Trim();

            if (!CsvImportUtility.IsSafeId(id) || !idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: listing ID '{id}' is invalid " +
                    "or duplicated.");
                rejected++;
                continue;
            }

            string foodId = CsvImportUtility.Get(
                row, columns, "food_id").Trim();
            string storeId = CsvImportUtility.Get(
                row, columns, "store_id").Trim();
            string shelfId = CsvImportUtility.Get(
                row, columns, "shelf_id").Trim();

            FoodData food = AssetDatabase.LoadAssetAtPath<FoodData>(
                $"{FoodFolder}/{foodId}.asset");
            StoreData store = AssetDatabase.LoadAssetAtPath<StoreData>(
                $"{StoreFolder}/{storeId}.asset");

            if (food == null)
            {
                Debug.LogError(
                    $"Listing {id}: food ID '{foodId}' does not resolve " +
                    "to a FoodData asset. Import Foods first.");
                rejected++;
                continue;
            }

            if (store == null)
            {
                Debug.LogError(
                    $"Listing {id}: store ID '{storeId}' does not resolve " +
                    "to a StoreData asset. Import Stores first.");
                rejected++;
                continue;
            }

            if (!store.HasShelf(shelfId))
            {
                Debug.LogError(
                    $"Listing {id}: shelf ID '{shelfId}' is not defined " +
                    $"by store {store.id}.");
                rejected++;
                continue;
            }

            StoreListingData listing =
                CsvImportUtility.GetOrCreateAsset<StoreListingData>(
                    $"{OutputFolder}/{id}.asset",
                    out bool isNew);

            listing.id = id;
            listing.food = food;
            listing.store = store;
            listing.shelfId = shelfId;
            listing.unlockDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "unlock_day"),
                1, 1, int.MaxValue, id, "unlock_day");
            listing.requiredFlag = CsvImportUtility.Get(
                row, columns, "required_flag").Trim();
            listing.observedPriceCad = CsvImportUtility.ParseFloat(
                CsvImportUtility.Get(row, columns, "observed_price_cad"),
                0f, 0f, id, "observed_price_cad");
            listing.servingsPerPackage = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(
                    row, columns, "servings_per_package"),
                0, 0, int.MaxValue, id, "servings_per_package");
            listing.gamePrice = CsvImportUtility.ParseFloat(
                CsvImportUtility.Get(row, columns, "game_price"),
                0f, 0f, id, "game_price");
            listing.priceSource = CsvImportUtility.Get(
                row, columns, "price_source");
            listing.priceDisplayStyle = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(
                    row, columns, "price_display_style"),
                store.defaultPriceDisplayStyle,
                id,
                "price_display_style");
            listing.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            listing.reviewNotes = CsvImportUtility.Get(
                row, columns, "review_notes");

            EditorUtility.SetDirty(listing);
            imported.Add(listing);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        StoreListingDatabase database =
            CsvImportUtility.GetOrCreateAsset<StoreListingDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported.OrderBy(item => item.id).ToList());
        EditorUtility.SetDirty(database);
        var foodDatabase = AssetDatabase.LoadAssetAtPath<FoodDatabase>("Assets/GameData/Foods/FoodDatabase.asset");
        if (foodDatabase != null)
        {
            foodDatabase.RebuildMinimumPrices(database.Listings);
            EditorUtility.SetDirty(foodDatabase);
        }

        CsvImportUtility.SaveAndReport(
            "Price listing", created, updated, rejected);
    }
}
