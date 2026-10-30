// File responsibility: Sells pantry servings back for cash so a broke bistro can keep operating.
// Per serving: pantrySellRate × the cheaper of (lowest current shelf price per serving, what was paid per serving).
// Using the cheaper of the two means a buy-sort-sell loop can never make a profit.
// The UI collects servings in a SELL slot (one per drag) and confirms; the sale is validated and applied atomically.
using System;
using System.Collections.Generic;

[Serializable]
public class PantrySaleItem
{
    public string inventoryEntryId;
    public int servings;
}

[Serializable]
public class PantrySaleResult : OperationResult
{
    public int servingsSold;
    public float amount;
}

public class PantryService
{
    private readonly GameSessionState state;
    private readonly FoodDatabase foods;
    private readonly BalanceConfig balance;
    public PantryService(GameSessionState state, FoodDatabase foods, BalanceConfig balance)
    { this.state = state; this.foods = foods; this.balance = balance; }

    public float SellRate => balance.pantrySellRate;

    // Value of one serving of a batch, or a negative number when it cannot be sold.
    public float ValuePerServing(string inventoryEntryId)
    {
        var entry = Find(inventoryEntryId);
        return entry == null ? -1 : ValuePerServing(entry, out _);
    }

    // Preview only: never mutates state.
    public PantrySaleResult Quote(IList<PantrySaleItem> items) => Evaluate(items, false);
    public PantrySaleResult Sell(IList<PantrySaleItem> items) => Evaluate(items, true);

    private InventoryEntry Find(string id) => state.inventory.Find(e => e != null && string.Equals(e.inventoryEntryId, id, StringComparison.OrdinalIgnoreCase));

    private float ValuePerServing(InventoryEntry entry, out string error)
    {
        error = null;
        if (!MealAllocation.Usable(entry)) { error = "Spoiled food cannot be sold."; return -1; }
        if (balance.pantrySellRate <= 0) { error = "Selling pantry food is turned off."; return -1; }
        if (!foods.TryGetMinimumServingPrice(entry.foodId, out float minimum) || minimum < 0 || float.IsNaN(minimum) || float.IsInfinity(minimum))
        { error = "No shelf price is known for this food. Import the price CSV again."; return -1; }
        float paid = entry.purchaseCostPerServing;
        float basis = paid >= 0 && !float.IsNaN(paid) && !float.IsInfinity(paid) ? Math.Min(minimum, paid) : minimum;
        return basis * balance.pantrySellRate;
    }

    private PantrySaleResult Evaluate(IList<PantrySaleItem> items, bool commit)
    {
        if (items == null || items.Count == 0) return new PantrySaleResult { error = "Drag servings into SELL first." };
        // Merge repeated batch IDs, then validate every batch before anything changes.
        var wanted = new Dictionary<InventoryEntry, int>();
        double total = 0;
        foreach (var item in items)
        {
            if (item == null || item.servings <= 0) return new PantrySaleResult { error = "Invalid sale quantity." };
            var entry = Find(item.inventoryEntryId);
            if (entry == null) return new PantrySaleResult { error = "A selected batch is no longer in the pantry." };
            wanted.TryGetValue(entry, out int count);
            wanted[entry] = count + item.servings;
        }
        int servings = 0;
        foreach (var pair in wanted)
        {
            if (pair.Value > pair.Key.servings) return new PantrySaleResult { error = "Not enough servings left in a selected batch." };
            float each = ValuePerServing(pair.Key, out string error);
            if (error != null) return new PantrySaleResult { error = error };
            total += each * (double)pair.Value; servings += pair.Value;
        }
        float amount = (float)Math.Round(total, 2, MidpointRounding.AwayFromZero);
        if (float.IsNaN(amount) || float.IsInfinity(state.money + amount)) return new PantrySaleResult { error = "Invalid sale amount." };
        if (commit)
        {
            foreach (var pair in wanted) pair.Key.servings -= pair.Value;
            state.inventory.RemoveAll(e => e != null && e.servings == 0);
            state.money += amount;
            state.flags.SetFlag(TutorialFlags.PantrySold);
        }
        return new PantrySaleResult { success = true, servingsSold = servings, amount = amount };
    }
}
