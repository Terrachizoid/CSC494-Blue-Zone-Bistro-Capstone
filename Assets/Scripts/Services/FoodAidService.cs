// File responsibility: The farmer's food aid. When no unlocked recipe can be completed from the pantry,
// gives just enough low-appeal green food for one random completable recipe, at most once a day.
// Gifted batches are ordinary inventory: they record the cheapest shelf price as their cost, so selling and
// meal cost reporting use the normal rules, and they spoil after foodAidShelfLifeDays.
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class FoodAidResult
{
    public bool given;
    public string recipeId;
    public List<IngredientSelection> gifts = new List<IngredientSelection>(); // inventoryEntryId + servings
    public string message;
}

public class FoodAidService
{
    private readonly GameSessionState state;
    private readonly FoodDatabase foods;
    private readonly RecipeDatabase recipes;
    private readonly BalanceConfig balance;
    public FoodAidService(GameSessionState state, FoodDatabase foods, RecipeDatabase recipes, BalanceConfig balance)
    { this.state = state; this.foods = foods; this.recipes = recipes; this.balance = balance; }

    private IEnumerable<RecipeData> Unlocked => (recipes?.Recipes ?? Array.Empty<RecipeData>())
        .Where(r => r != null && r.unlockDay <= state.currentDay && RecipeRequirements.TryGetBounds(r, out _, out _));

    // Low-appeal green foods that can fill a category.
    private List<FoodData> Candidates(FoodCategory category) => foods.InCategory(category)
        .Where(f => f.trafficLight == TrafficLight.Green && f.ingredientAppeal <= balance.foodAidMaxAppeal &&
                    foods.TryGetMinimumServingPrice(f.id, out _)).ToList();

    public bool Needed()
    {
        var check = new RecipeCheck(foods);
        // With the city open during service the player can shop instead, so the farmer only helps when money is short too.
        return balance.foodAidEnabled && state.lastFoodAidDay != state.currentDay &&
            (!balance.cityDuringService || state.money < balance.foodAidMoneyBelow) && !Unlocked.Any(r => check.CanMake(r, state));
    }

    public FoodAidResult TryGive()
    {
        var result = new FoodAidResult();
        if (!Needed()) return result;
        // Only recipes whose every required category has a gift candidate.
        var options = Unlocked.Where(r => RecipeRequirements.TryGetBounds(r, out var need, out _) && need.All(p => Candidates(p.Key).Count > 0))
            .OrderBy(r => r.id, StringComparer.Ordinal).ToList();
        if (options.Count == 0) return result;
        // Seeded by day so a reloaded checkpoint gives the same crate.
        var random = new Random(state.currentDay * 7919 + options.Count);
        var recipe = options[random.Next(options.Count)];
        RecipeRequirements.TryGetBounds(recipe, out var required, out _);
        var names = new List<string>();
        foreach (var pair in required.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal))
        {
            var pool = Candidates(pair.Key);
            var food = pool[random.Next(pool.Count)];
            foods.TryGetMinimumServingPrice(food.id, out float price);
            var entry = new InventoryEntry { foodId = food.id, servings = (int)pair.Value,
                purchaseCostPerServing = price, daysRemaining = Math.Max(1, balance.foodAidShelfLifeDays) };
            state.inventory.Add(entry);
            result.gifts.Add(new IngredientSelection { inventoryEntryId = entry.inventoryEntryId, servingsUsed = entry.servings });
            names.Add($"{food.displayName ?? food.id} ×{entry.servings}");
        }
        state.lastFoodAidDay = state.currentDay;
        state.flags.SetFlag(TutorialFlags.FoodAidGiven);
        result.given = true; result.recipeId = recipe.id;
        result.message = $"A local farmer dropped off {string.Join(", ", names)}: enough for {recipe.displayName}. Use it today; it won't keep.";
        return result;
    }
}
