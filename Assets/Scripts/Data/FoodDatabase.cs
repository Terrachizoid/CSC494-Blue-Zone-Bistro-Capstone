// File responsibility: Imported food catalogue with case-insensitive ID lookup and derived minimum prices across stores.
// RebuildMinimumPrices must run after listings change; minimum value is not the historical purchase cost.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FoodDatabase",
    menuName = "Blue Zone Bistro/Databases/Foods")]
public class FoodDatabase : ScriptableObject
{
    [Serializable]
    public class MinimumServingPrice
    {
        public string foodId;
        public float price;
    }

    [SerializeField] private List<MinimumServingPrice> minimumServingPrices = new List<MinimumServingPrice>();
    public IReadOnlyList<MinimumServingPrice> MinimumServingPrices => minimumServingPrices;

    // All stores participate, including locked ones. Invalid listings never define value.
    public void RebuildMinimumPrices(IEnumerable<StoreListingData> listings)
    {
        var minimums = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (listings != null)
        foreach (var listing in listings)
        {
            if (listing == null || listing.food == null || listing.store == null ||
                string.IsNullOrWhiteSpace(listing.food.id) || GetById(listing.food.id) == null ||
                listing.servingsPerPackage <= 0 || listing.gamePrice < 0 ||
                float.IsNaN(listing.gamePrice) || float.IsInfinity(listing.gamePrice)) continue;
            float unitPrice = listing.gamePrice / listing.servingsPerPackage;
            if (!minimums.TryGetValue(listing.food.id, out float oldPrice) || unitPrice < oldPrice)
                minimums[listing.food.id] = unitPrice;
        }
        minimumServingPrices.Clear();
        var ids = new List<string>(minimums.Keys);
        ids.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids) minimumServingPrices.Add(new MinimumServingPrice { foodId = id, price = minimums[id] });
    }

    public bool TryGetMinimumServingPrice(string foodId, out float price)
    {
        var entry = minimumServingPrices.Find(p => string.Equals(p.foodId, foodId, StringComparison.OrdinalIgnoreCase));
        price = entry == null ? 0 : entry.price;
        return entry != null;
    }
    [SerializeField]
    private List<FoodData> foods = new List<FoodData>();

    private Dictionary<string, FoodData> byId;

    public IReadOnlyList<FoodData> Foods => foods;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<FoodData> items)
    {
        foods = items ?? new List<FoodData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out FoodData food)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out food);
    }

    public FoodData GetById(string id)
    {
        return TryGetById(id, out FoodData food) ? food : null;
    }

    private void EnsureIndex()
    {
        if (byId == null)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        byId = new Dictionary<string, FoodData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (FoodData food in foods)
        {
            if (food == null || string.IsNullOrWhiteSpace(food.id))
            {
                continue;
            }

            if (!byId.TryAdd(food.id, food))
            {
                Debug.LogError(
                    $"Duplicate FoodData ID '{food.id}'.",
                    this);
            }
        }
    }
}
