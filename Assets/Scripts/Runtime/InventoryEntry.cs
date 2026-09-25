// File responsibility: One purchase batch with remaining servings, expiry, and historical unit cost.
// Use the unique batch ID for selections; separate purchases of one food must remain distinguishable.

using System;

[Serializable]
public class InventoryEntry
{
    public string inventoryEntryId = Guid.NewGuid().ToString("N");
    // Matches FoodData.id; store remaining servings rather than purchased packages.
    public string foodId;
    public int servings;
    // Historical cost, captured at purchase; never recomputed from current shop prices.
    public float purchaseCostPerServing;

    // -1 means no expiry; 0 means expired. Set from storage rules when purchased.
    public int daysRemaining = -1;
}
