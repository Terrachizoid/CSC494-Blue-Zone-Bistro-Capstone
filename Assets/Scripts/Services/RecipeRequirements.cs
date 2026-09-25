using System;
using System.Collections.Generic;

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
}
