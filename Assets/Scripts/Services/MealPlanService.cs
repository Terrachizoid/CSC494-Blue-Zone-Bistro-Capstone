// File responsibility: Today's meal plan (the "Today's plan" panel). The player pins recipes, at most one per
// expected guest; the report sums what the remaining planned dishes need per food category and compares it with
// usable pantry servings plus the uncommitted cart. Read-only over the session except for the pins themselves.
// Pins last for the current day only. Servings sitting in meal slots are still pantry stock, so they count as had.
using System;
using System.Collections.Generic;
using System.Linq;

public class MealPlanDish
{
    public RecipeData recipe;
    public int planned;
    public int served;   // Successful dishes of this recipe today (capped at planned).
    public int Remaining => Math.Max(0, planned - served);
}

public class MealPlanNeed
{
    public FoodCategory category;
    public long required;  // Servings the remaining planned dishes must have.
    public long optional;  // Extra servings they could also take.
    public long have;      // Usable pantry servings + cart servings in this category.
    public long Missing => Math.Max(0, required - have);
}

public class MealPlanReport
{
    public List<MealPlanDish> dishes = new List<MealPlanDish>();
    public List<MealPlanNeed> needs = new List<MealPlanNeed>();
    public int planned;
    public int limit;
    public int Remaining => dishes.Sum(d => d.Remaining);
    public long Missing => needs.Sum(n => n.Missing);
    public bool Empty => planned == 0;
    public bool Ready => planned > 0 && Missing == 0;
}

public class MealPlanService
{
    private readonly GameSessionState state;
    private readonly FoodDatabase foods;
    private readonly RecipeDatabase recipes;
    private readonly StoreListingDatabase listings;
    private readonly Dictionary<string, int> pins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int day = -1;

    public MealPlanService(GameSessionState state, FoodDatabase foods, RecipeDatabase recipes, StoreListingDatabase listings)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.foods = foods ?? throw new ArgumentNullException(nameof(foods));
        this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        this.listings = listings;
    }

    // One planned dish per guest expected today (the Guests list).
    public int Limit => state.dailyCustomerIds == null ? 0 : state.dailyCustomerIds.Count;
    public int Total { get { Roll(); return pins.Values.Sum(); } }
    public int Planned(string recipeId) { Roll(); int n; return recipeId != null && pins.TryGetValue(recipeId, out n) ? n : 0; }

    public bool Add(string recipeId)
    {
        Roll();
        var recipe = recipeId == null ? null : recipes.GetById(recipeId);
        if (recipe == null || recipe.unlockDay > state.currentDay || !RecipeRequirements.TryGetBounds(recipe, out _, out _)) return false;
        if (Total >= Limit) return false;
        pins[recipe.id] = Planned(recipe.id) + 1;
        return true;
    }

    public bool Remove(string recipeId)
    {
        int n = Planned(recipeId);
        if (n <= 0) return false;
        if (n == 1) pins.Remove(recipeId); else pins[recipeId] = n - 1;
        return true;
    }

    public void Clear() { pins.Clear(); day = state.currentDay; }

    // cart: the UI's uncommitted cart. servedRecipeIds: recipes of today's successful dishes (GameLoop.Summary.dishes).
    public MealPlanReport Report(IEnumerable<CartEntry> cart, IEnumerable<string> servedRecipeIds)
    {
        Roll();
        var report = new MealPlanReport { limit = Limit };
        var served = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in servedRecipeIds ?? Enumerable.Empty<string>())
        {
            if (id == null) continue;
            int n; served.TryGetValue(id, out n); served[id] = n + 1;
        }
        var required = new Dictionary<FoodCategory, long>();
        var optional = new Dictionary<FoodCategory, long>();
        foreach (var pin in pins.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var recipe = recipes.GetById(pin.Key);
            Dictionary<FoodCategory, long> minimum, maximum;
            if (recipe == null || !RecipeRequirements.TryGetBounds(recipe, out minimum, out maximum)) continue;
            int done; served.TryGetValue(recipe.id, out done);
            var dish = new MealPlanDish { recipe = recipe, planned = pin.Value, served = Math.Min(done, pin.Value) };
            report.dishes.Add(dish);
            report.planned += dish.planned;
            if (dish.Remaining == 0) continue;
            foreach (var pair in maximum)
            {
                long need; minimum.TryGetValue(pair.Key, out need);
                long r, o; required.TryGetValue(pair.Key, out r); optional.TryGetValue(pair.Key, out o);
                required[pair.Key] = r + need * dish.Remaining;
                optional[pair.Key] = o + (pair.Value - need) * dish.Remaining;
            }
        }
        if (required.Count == 0 && optional.Count == 0) return report;

        var owned = new Dictionary<FoodData, long>();
        foreach (var entry in state.inventory)
        {
            if (!MealAllocation.Usable(entry)) continue;
            var food = foods.GetById(entry.foodId);
            if (food == null) continue;
            long n; owned.TryGetValue(food, out n); owned[food] = n + entry.servings;
        }
        foreach (var item in cart ?? Enumerable.Empty<CartEntry>())
        {
            var listing = item == null || listings == null ? null : listings.GetById(item.listingId);
            if (listing == null || listing.food == null || item.packageQuantity <= 0) continue;
            long n; owned.TryGetValue(listing.food, out n);
            owned[listing.food] = n + (long)listing.servingsPerPackage * item.packageQuantity;
        }
        // A multi-category serving (tofu) counts once, toward whichever planned category still needs it.
        var have = RecipeRequirements.Assign(owned, required, null, ignoreSurplus: true);
        foreach (var category in required.Keys.Union(optional.Keys).OrderBy(c => (int)c))
        {
            long r, o, h; required.TryGetValue(category, out r); optional.TryGetValue(category, out o); have.TryGetValue(category, out h);
            if (r == 0 && o == 0) continue;
            report.needs.Add(new MealPlanNeed { category = category, required = r, optional = o, have = h });
        }
        return report;
    }

    private void Roll() { if (day != state.currentDay) { pins.Clear(); day = state.currentDay; } }
}
