// File responsibility: Inputs, results, and interfaces for purchase, meal, and day operations.
// A result with success=false means rejected input; success=true can still mean recipeBuilt=false.

using System;
using System.Collections.Generic;

public interface IPurchaseService { PurchaseResult Buy(string listingId); }
public interface IMealService { ServeResult Serve(ServeInput input); ServeResult Preview(ServeInput input); }
public interface IDayService { DayResult FinishDay(); }

[Serializable]
public class IngredientSelection
{
    public string inventoryEntryId;
    public int servingsUsed;
}

[Serializable]
public class ServeInput
{
    public string customerId;
    public string recipeId;
    public List<IngredientSelection> ingredients = new List<IngredientSelection>();
}

[Serializable]
public class OperationResult
{
    public bool success;
    public string error;
}

[Serializable]
public class PurchaseResult : OperationResult
{
    public string inventoryEntryId;
    public string foodId;
    public int servingsAdded;
    public int daysRemaining;
    public float moneySpent;
}

[Serializable]
public class ConsumedIngredient
{
    public string inventoryEntryId;
    public string foodId;
    public int servingsUsed;
}

[Serializable]
public class ServeResult : OperationResult
{
    // success means the operation is valid; recipeBuilt reports composition separately.
    public bool recipeBuilt;
    public string[] recipeWarnings = Array.Empty<string>();
    public float ingredientCost;
    public float minimumIngredientValue;
    public int customerFinance;
    public float basePayment;
    public float tip;
    public int healthDelta;
    public int delightDelta;
    public bool rushCrashTriggered;
    public int greenServings;
    public int yellowServings;
    public int redServings;
    public float moneyEarned;
    public DailyDozenCategory[] dailyDozenCovered = Array.Empty<DailyDozenCategory>();
    public ConsumedIngredient[] consumedIngredients = Array.Empty<ConsumedIngredient>();
    // Reserved for a later craving system; no craving resolution is implemented yet.
    public CravingTag[] cravingsResolved = Array.Empty<CravingTag>();
}

[Serializable]
public class DayResult : OperationResult
{
    public int currentDay;
    public bool gameCompleted;
    public string[] expiredInventoryEntryIds = Array.Empty<string>();
}
