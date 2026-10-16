// File responsibility: Designer tuning shared by services. Values are fictional gameplay numbers, not nutrition advice.
// Changing this asset tunes future operations; it does not reset existing customer/session state.

using UnityEngine;

[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Blue Zone Bistro/Balance")]
public class BalanceConfig : ScriptableObject
{
    [Min(0)] public float startingMoney = 30f;
    [Header("First successful food sorting reward")]
    [Range(0, 1)] public float maximumSortingDiscount = 0.30f;
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
    [Header("Ingredient appeal and cravings")]
    [Min(0)] public float ingredientAppealMultiplier = 1f;
    [Min(0)] public float recipeDelightMultiplier = 1f;
    [Min(0)] public float delightPerResolvedCraving = 4f;
    [Min(1)] public int cravingsStartDay = 4;
    [Min(1)] public int minimumDailyCravings = 1;
    [Min(1)] public int maximumDailyCravings = 2;
    [Header("Bistro popularity and community wellbeing")]
    [Min(1)] public float maximumPopularity = 1000f;
    [Min(0)] public float startingPopularity = 300f;
    [Min(0)] public float popularityPerDelight = 5f;
    [Min(0)] public float failedRecipePopularityPenalty = 40f;
    [Min(0)] public float maximumPopularityGainPerDish = 100f;
    public float[] attendanceThresholds = { 0f, 250f, 500f, 750f };
    public CustomerPopularityUnlock[] customerPopularityUnlocks = {
        new CustomerPopularityUnlock { customerId="U005", popularity=500f },
        new CustomerPopularityUnlock { customerId="U006", popularity=750f }
    };
    [Min(1)] public float maximumCommunityWellbeing = 100f;
    [Min(0)] public float startingCommunityWellbeing = 50f;
    [Min(0)] public float wellbeingPerHealthPoint = .25f;
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

[System.Serializable]
public class CustomerPopularityUnlock
{
    public string customerId;
    [Min(0)] public float popularity;
}
