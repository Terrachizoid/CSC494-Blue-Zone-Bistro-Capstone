// File responsibility: Read-only pantry feasibility hint used by UI; does not choose or reserve ingredients.
// Shared RecipeRequirements supplies slot bounds. MealService remains authoritative when serving.

using System.Collections.Generic;

// Availability checks stock across batches without reserving or consuming it.
public class RecipeCheck
{
    private readonly FoodDatabase foods;
    public RecipeCheck(FoodDatabase foods) { this.foods = foods ?? throw new System.ArgumentNullException(nameof(foods)); }

    public bool CanMake(RecipeData recipe, GameSessionState state)
    {
        if (state == null || recipe == null || recipe.unlockDay > state.currentDay ||
            !RecipeRequirements.TryGetBounds(recipe, out var required, out var capacity)) return false;
        var available = new Dictionary<FoodCategory, long>();
        bool anyUsable = false;
        foreach (var entry in state.inventory)
        {
            if (entry == null || entry.servings <= 0 || entry.daysRemaining == 0 ||
                entry.daysRemaining < -1 || string.IsNullOrWhiteSpace(entry.inventoryEntryId)) continue;
            var food = foods.GetById(entry.foodId);
            if (food == null || !capacity.ContainsKey(food.recipeCategory) ||
                (food.trafficLight != TrafficLight.Green && food.trafficLight != TrafficLight.Yellow &&
                 food.trafficLight != TrafficLight.Red) ||
                (!recipe.allowNonGreenChoices && food.trafficLight != TrafficLight.Green)) continue;
            anyUsable = true;
            available.TryGetValue(food.recipeCategory, out long count);
            available[food.recipeCategory] = count + entry.servings;
        }
        foreach (var pair in required)
        {
            available.TryGetValue(pair.Key, out long count);
            if (count < pair.Value) return false;
        }
        return anyUsable; // MealService requires at least one ingredient, even for optional-only recipes.
    }
}
