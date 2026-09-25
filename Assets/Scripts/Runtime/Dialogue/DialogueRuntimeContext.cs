// File responsibility: Selection/rendering snapshot of the current day, character, and optional meal result.
// This is input to dialogue rules, not an independently maintained copy of the game session.

using System;
using System.Collections.Generic;

public class DialogueRuntimeContext
{
    public int currentDay = 1;

    public string customerId;
    public string customerName;
    public int customerHealth = -1;
    public int customerDelight = -1;

    // This is the recipe/meal served during the current service interaction,
    // not a global "last meal" flag.
    public RecipeData servedRecipe;
    public int servedMealDelight = -1;
    public bool hasServedMeal;
    public bool servedRecipeBuilt;
    public int servedMealHealthDelta;

    private readonly Dictionary<string, string> additionalTokens =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void SetToken(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        additionalTokens[name.Trim()] = value?.ToString() ?? string.Empty;
    }

    public bool TryGetToken(string name, out string value)
    {
        return additionalTokens.TryGetValue(name ?? string.Empty, out value);
    }
}
