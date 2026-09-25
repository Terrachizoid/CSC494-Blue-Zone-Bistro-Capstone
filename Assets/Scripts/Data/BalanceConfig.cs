// File responsibility: Designer tuning shared by services. Values are fictional gameplay numbers, not nutrition advice.
// Changing this asset tunes future operations; it does not reset existing customer/session state.

using UnityEngine;

[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Blue Zone Bistro/Balance")]
public class BalanceConfig : ScriptableObject
{
    [Header("Prototype tuning; game values, not real-world shelf life")]
    [Min(1)] public int totalDays = 6;
    [Min(1)] public int freshDays = 2;
    [Min(1)] public int frozenDays = 5;
    [Min(-1)] public int cannedDays = -1;
    [Min(-1)] public int dryDays = -1;
    [Min(-1)] public int shelfStableDays = -1;

    [Header("Provisional scoring; requires balance review")]
    [Min(0)] public int healthPerGreenServing = 1;
    [Min(0)] public int healthPerGreenDailyDozenCategory = 1;
    [Min(0)] public int healthPenaltyPerYellowServing = 1;
    [Min(0)] public int healthPenaltyPerRedServing = 3;
    [Header("Meal payments and recipe failure")]
    [Min(0)] public int failedRecipeDelightPenalty = 30;
    [Range(0, 1)] public float failedRecipePaymentRate = 0.75f;
    [Min(0)] public int maxHealthPerDish = 20;
    [Min(0)] public int maxDelightPerDish = 20;
    [Min(0)] public float delightPerIngredientDollar = 1f;
    [Header("Customer visits and finance")]
    [Min(1)] public int maxDelight = 100;
    [Min(0)] public int startingDelight = 50;
    [Range(0, 100)] public int startingFinance = 20;
    [Min(1)] public int financeHealthLagDays = 2;

    public int GetShelfLife(StorageType type)
    {
        switch (type)
        {
            case StorageType.Fresh: return freshDays;
            case StorageType.Frozen: return frozenDays;
            case StorageType.Canned: return cannedDays;
            case StorageType.Dry: return dryDays;
            case StorageType.ShelfStable: return shelfStableDays;
            default: return 0; // Unconfigured storage cannot be purchased.
        }
    }
}
