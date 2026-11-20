using System;
using System.Collections.Generic;
using System.Linq;

// Shared interpretation of category slots for pantry availability and dish evaluation.
// Required slots set the minimum; required plus optional slots set the maximum.
// This is pure validation: it never reads or changes the session or consumes stock.
public static class RecipeRequirements
{
    public static bool TryGetBounds(RecipeData recipe,
        out Dictionary<FoodCategory, long> minimum,
        out Dictionary<FoodCategory, long> maximum)
    {
        minimum = new Dictionary<FoodCategory, long>();
        maximum = new Dictionary<FoodCategory, long>();
        if (recipe == null || recipe.slots == null || recipe.slots.Length == 0) return false;
        foreach (var slot in recipe.slots)
        {
            if (slot == null || slot.category == FoodCategory.Unassigned ||
                !Enum.IsDefined(typeof(FoodCategory), slot.category) || slot.servings <= 0) return false;
            minimum.TryGetValue(slot.category, out long lower);
            maximum.TryGetValue(slot.category, out long upper);
            minimum[slot.category] = lower + (slot.optional ? 0 : slot.servings);
            maximum[slot.category] = upper + slot.servings;
        }
        return true;
    }

    // Multi-category foods (Foods.csv also_categories): each serving counts toward exactly one category.
    // Every serving starts in its food's main category; servings of flexible foods then move one at a time to
    // another of their categories while that lowers the shortfall against minimum (and, unless ignoreSurplus,
    // the surplus over maximum). Foods with one category always stay put, so single-category play is unchanged.
    public static Dictionary<FoodCategory, long> Assign(IEnumerable<KeyValuePair<FoodData, long>> servings,
        IDictionary<FoodCategory, long> minimum, IDictionary<FoodCategory, long> maximum, bool ignoreSurplus = false)
    {
        var counts = new Dictionary<FoodCategory, long>();
        var placed = new List<KeyValuePair<FoodData, Dictionary<FoodCategory, long>>>();
        foreach (var pair in servings ?? Enumerable.Empty<KeyValuePair<FoodData, long>>())
        {
            if (pair.Key == null || pair.Value <= 0) continue;
            Add(counts, pair.Key.recipeCategory, pair.Value);
            if (pair.Key.Categories.Skip(1).Any())
                placed.Add(new KeyValuePair<FoodData, Dictionary<FoodCategory, long>>(pair.Key,
                    new Dictionary<FoodCategory, long> { { pair.Key.recipeCategory, pair.Value } }));
        }
        long Term(FoodCategory c, long n)
        {
            long low = 0, high = 0;
            if (minimum != null) minimum.TryGetValue(c, out low);
            long over = 0;
            if (!ignoreSurplus) { if (maximum != null) maximum.TryGetValue(c, out high); over = Math.Max(0, n - high); }
            return Math.Max(0, low - n) + over;
        }
        // Each move strictly lowers the total, so this ends; the cap is only a safety net.
        for (int guard = 0; guard < 100000; guard++)
        {
            bool moved = false;
            foreach (var food in placed)
            {
                foreach (var from in food.Value.Where(p => p.Value > 0).Select(p => p.Key).ToList())
                {
                    foreach (var to in food.Key.Categories)
                    {
                        if (to == from) continue;
                        long a, b; counts.TryGetValue(from, out a); counts.TryGetValue(to, out b);
                        if (Term(from, a - 1) + Term(to, b + 1) >= Term(from, a) + Term(to, b)) continue;
                        counts[from] = a - 1; counts[to] = b + 1;
                        food.Value[from] -= 1; Add(food.Value, to, 1);
                        moved = true; break;
                    }
                    if (moved) break;
                }
                if (moved) break;
            }
            if (!moved) break;
        }
        return counts;
    }

    private static void Add(Dictionary<FoodCategory, long> map, FoodCategory key, long n)
    {
        long old; map.TryGetValue(key, out old); map[key] = old + n;
    }
}
