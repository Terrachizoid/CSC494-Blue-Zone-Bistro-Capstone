// File responsibility: Global recipe fatigue. Serving the same recipe too often across all guests in a short
// window halves the popularity it can earn ("guests are used to it"). Shared by MealService and the UI.
using System;
using System.Linq;

public static class RecipeFatigue
{
    // Servings of this recipe (any guest) on today and the previous recipeFatigueWindowDays-1 days.
    public static int RecentServes(GameSessionState s, BalanceConfig b, string recipeId)
    {
        if (s?.recipeServes == null || b == null || string.IsNullOrEmpty(recipeId)) return 0;
        int window = Math.Max(1, b.recipeFatigueWindowDays);
        return s.recipeServes.Count(r => r != null && string.Equals(r.recipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                                         r.day > s.currentDay - window && r.day <= s.currentDay);
    }
    public static bool Enabled(BalanceConfig b) => b != null && b.recipeFatigueThreshold > 0;
    // True when the next serving of this recipe would be fatigued.
    public static bool IsFatigued(GameSessionState s, BalanceConfig b, string recipeId) =>
        Enabled(b) && RecentServes(s, b, recipeId) >= b.recipeFatigueThreshold;
}

[Serializable]
public class RecipeServeRecord
{
    public int day;
    public string recipeId;
    public string customerId;
    public bool built;
    public int green, yellow, red;
}
