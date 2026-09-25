// File responsibility: Validates selections and computes the same outcome for preview and committed service.
// Preview never consumes stock. Serve revalidates before committing; a recipe mismatch is a playable outcome.

using System;
using System.Collections.Generic;
using System.Linq;

public class MealService : IMealService
{
    private readonly GameSessionState state;
    private readonly FoodDatabase foods;
    private readonly RecipeDatabase recipes;
    private readonly CustomerDatabase customers;
    private readonly BalanceConfig balance;

    public MealService(GameSessionState state, FoodDatabase foods, RecipeDatabase recipes,
        CustomerDatabase customers, BalanceConfig balance)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.foods = foods ?? throw new ArgumentNullException(nameof(foods));
        this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
    }

    public ServeResult Serve(ServeInput input) => Evaluate(input, true);
    public ServeResult Preview(ServeInput input) => Evaluate(input, false);

    private ServeResult Evaluate(ServeInput input, bool commit)
    {
        if (input == null || input.ingredients == null || input.ingredients.Count == 0)
            return Fail("Select ingredients first.");
        var recipe = recipes.GetById(input.recipeId);
        var customer = customers.GetById(input.customerId);
        if (recipe == null || customer == null) return Fail("Recipe or customer was not found.");
        if (recipe.unlockDay > state.currentDay || customer.unlockDay > state.currentDay)
            return Fail("Recipe or customer is locked.");
        var customerState = state.customers.Find(c => Same(c.customerId, customer.id));
        if (customerState != null && customerState.lastServedDay == state.currentDay)
            return Fail("This customer has already been served today.");
        if (recipe.slots == null || recipe.slots.Length == 0 ||
            recipe.delightValue < 0 || float.IsNaN(recipe.baseSalePrice) ||
            float.IsInfinity(recipe.baseSalePrice) || recipe.baseSalePrice < 0 ||
            float.IsNaN(state.money + recipe.baseSalePrice) || float.IsInfinity(state.money + recipe.baseSalePrice))
            return Fail("Invalid recipe or money configuration.");
        if (balance.healthPerGreenServing < 0 || balance.healthPerGreenDailyDozenCategory < 0 ||
            balance.healthPenaltyPerYellowServing < 0 || balance.healthPenaltyPerRedServing < 0 ||
            balance.failedRecipeDelightPenalty < 0 || balance.maxHealthPerDish < 0 || balance.maxDelightPerDish < 0 ||
            balance.maxDelight < 1 || balance.delightPerIngredientDollar < 0 ||
            float.IsNaN(balance.delightPerIngredientDollar) || float.IsInfinity(balance.delightPerIngredientDollar) ||
            float.IsNaN(balance.failedRecipePaymentRate) || balance.failedRecipePaymentRate < 0 || balance.failedRecipePaymentRate > 1)
            return Fail("Invalid balance tuning values.");

        // Aggregate repeated input rows before checking availability, preventing double consumption.
        var selected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in input.ingredients)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.inventoryEntryId) || item.servingsUsed <= 0)
                return Fail("Each ingredient needs a batch ID and positive servings.");
            selected.TryGetValue(item.inventoryEntryId, out long previous);
            selected[item.inventoryEntryId] = previous + item.servingsUsed;
        }

        var result = new ServeResult();
        var warnings = new List<string>();
        double ingredientCost = 0;
        double minimumIngredientValue = 0;
        var coverage = new HashSet<DailyDozenCategory>();
        var greenCoverage = new HashSet<DailyDozenCategory>();
        var categories = new Dictionary<FoodCategory, long>();
        var consumed = new List<ConsumedIngredient>();
        var batches = new List<InventoryEntry>();
        long green = 0, yellow = 0, red = 0;
        foreach (var pair in selected)
        {
            var matches = state.inventory.FindAll(e => Same(e.inventoryEntryId, pair.Key));
            if (matches.Count != 1) return Fail("Inventory batch is missing or duplicated.");
            var entry = matches[0];
            if (entry.daysRemaining == 0 || entry.daysRemaining < -1 || pair.Value > entry.servings)
                return Fail("An ingredient is expired or has insufficient servings.");
            var food = foods.GetById(entry.foodId);
            if (food == null || food.recipeCategory == FoodCategory.Unassigned)
                return Fail("Food or category is missing.");
            switch (food.trafficLight)
            {
                case TrafficLight.Green: green += pair.Value; break;
                case TrafficLight.Yellow: yellow += pair.Value; break;
                case TrafficLight.Red: red += pair.Value; break;
                default: return Fail("Food has no valid Traffic Light classification.");
            }
            if (!recipe.allowNonGreenChoices && food.trafficLight != TrafficLight.Green)
                warnings.Add($"{food.displayName ?? food.id}: this recipe requires green ingredients.");
            if (entry.purchaseCostPerServing < 0 || float.IsNaN(entry.purchaseCostPerServing) ||
                float.IsInfinity(entry.purchaseCostPerServing)) return Fail("Invalid ingredient purchase cost.");
            ingredientCost += entry.purchaseCostPerServing * (double)pair.Value;
            if (!foods.TryGetMinimumServingPrice(entry.foodId, out float minimumPrice) ||
                minimumPrice < 0 || float.IsNaN(minimumPrice) || float.IsInfinity(minimumPrice))
                return Fail($"Missing minimum price for {entry.foodId}. Import the price CSV again.");
            minimumIngredientValue += minimumPrice * (double)pair.Value;
            categories.TryGetValue(food.recipeCategory, out long count);
            categories[food.recipeCategory] = count + pair.Value;
            foreach (var category in food.dailyDozenCategories ?? Array.Empty<DailyDozenCategory>())
            {
                if (category == DailyDozenCategory.None) continue;
                coverage.Add(category);
                if (food.trafficLight == TrafficLight.Green) greenCoverage.Add(category);
            }
            batches.Add(entry);
            consumed.Add(new ConsumedIngredient { inventoryEntryId = entry.inventoryEntryId,
                foodId = entry.foodId, servingsUsed = (int)pair.Value });
        }
        if (green > int.MaxValue || yellow > int.MaxValue || red > int.MaxValue)
            return Fail("Too many servings.");

        // Optional slots can be empty or partially filled; no category may exceed total slot capacity.
        if (!RecipeRequirements.TryGetBounds(recipe, out var required, out var capacity))
            return Fail("Invalid recipe slot.");
        foreach (var pair in required)
        {
            categories.TryGetValue(pair.Key, out long amount);
            if (amount < pair.Value) warnings.Add($"Missing {pair.Key}: {pair.Value - amount} serving(s) (selected {amount}, required {pair.Value}).");
        }
        foreach (var pair in categories)
        {
            capacity.TryGetValue(pair.Key, out long maximum);
            if (pair.Value > maximum) warnings.Add($"Surplus {pair.Key}: {pair.Value - maximum} serving(s) (selected {pair.Value}, maximum {maximum}).");
        }
        result.recipeBuilt = warnings.Count == 0;
        result.recipeWarnings = warnings.ToArray();
        result.ingredientCost = (float)ingredientCost;
        result.minimumIngredientValue = (float)minimumIngredientValue;
        result.customerFinance = Math.Max(0, Math.Min(100, customerState == null ? balance.startingFinance : customerState.finance));
        result.basePayment = result.recipeBuilt ? recipe.baseSalePrice : (float)(minimumIngredientValue * balance.failedRecipePaymentRate);
        result.tip = result.recipeBuilt ? recipe.baseSalePrice * result.customerFinance / 100f : 0;
        result.moneyEarned = result.basePayment + result.tip;
        if (float.IsNaN(result.moneyEarned) || float.IsInfinity(result.moneyEarned) ||
            float.IsInfinity(state.money + result.moneyEarned)) return Fail("Invalid meal payment.");

        int oldHealth = customerState == null ? customer.startingHealth : customerState.health;
        int oldDelight = Math.Max(0, Math.Min(balance.maxDelight,
            customerState == null ? balance.startingDelight : customerState.delight));
        if (oldHealth < 0 || oldHealth > 100)
            return Fail("Invalid customer state.");
        long healthChange = green * balance.healthPerGreenServing +
            (long)greenCoverage.Count * balance.healthPerGreenDailyDozenCategory -
            yellow * balance.healthPenaltyPerYellowServing - red * balance.healthPenaltyPerRedServing;
        healthChange = Math.Min(balance.maxHealthPerDish, healthChange); // Only positive gain is capped.
        int nextHealth = (int)Math.Max(0L, Math.Min(100L, oldHealth + healthChange));
        int delightChange = result.recipeBuilt
            ? (int)Math.Min(balance.maxDelightPerDish,
                recipe.delightValue + Math.Floor(minimumIngredientValue * balance.delightPerIngredientDollar))
            : -balance.failedRecipeDelightPenalty;
        int nextDelight = (int)Math.Max(0L, Math.Min(balance.maxDelight, (long)oldDelight + delightChange));

        // Commit only after all validation succeeds. Failed requests never spend or consume anything.
        if (commit)
        {
            if (customerState == null)
            {
                customerState = CustomerState.Create(customer, balance);
                state.customers.Add(customerState);
            }
            for (int i = 0; i < batches.Count; i++) batches[i].servings -= consumed[i].servingsUsed;
            state.inventory.RemoveAll(e => e.servings == 0);
            customerState.health = nextHealth;
            customerState.delight = nextDelight;
            customerState.lastServedDay = state.currentDay;
            state.money += result.moneyEarned;
            state.currentCustomerId = customer.id;
            state.selectedRecipeId = recipe.id;
        }

        result.success = true;
        result.healthDelta = nextHealth - oldHealth;
        result.delightDelta = nextDelight - oldDelight;
        result.rushCrashTriggered = red > 0;
        result.greenServings = (int)green;
        result.yellowServings = (int)yellow;
        result.redServings = (int)red;
        result.dailyDozenCovered = coverage.OrderBy(c => c).ToArray();
        result.consumedIngredients = consumed.ToArray();
        return result;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static ServeResult Fail(string error) => new ServeResult { error = error };
}
