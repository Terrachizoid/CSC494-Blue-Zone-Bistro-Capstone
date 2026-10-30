// File responsibility: Designer tuning shared by services. Values are fictional gameplay numbers, not nutrition advice.
// Changing this asset tunes future operations; it does not reset existing customer/session state.

using UnityEngine;

[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Blue Zone Bistro/Balance")]
public class BalanceConfig : ScriptableObject
{
    [Header("Morning events")]
    [Min(2)] public int eventsStartDay=2;
    [Range(0,1)] public float noEventChance=.25f;
    [Range(0,1)] public float twoEventChance=.25f;
    [Min(0)] public float startingMoney = 30f;
    [Header("First successful food sorting reward")]
    // Refund on each correctly sorted new food's price (known foods and wrong answers earn nothing).
    [Range(0, 1)] public float maximumSortingDiscount = 0.50f;
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
    // Customer popularity unlocks are authored per customer in Customers.csv (unlock_popularity).
    [Min(1)] public float maximumCommunityWellbeing = 100f;
    [Min(0)] public float startingCommunityWellbeing = 50f;
    [Min(0)] public float wellbeingPerHealthPoint = .25f;
    [Header("Rent, walk-ins and craving pressure (C# defaults off; tuned in BalanceConfig.asset)")]
    [Tooltip("First day whose morning charges rent.")] [Min(1)] public int rentStartDay = 3;
    [Tooltip("Charged each morning from rentStartDay. Money may go negative.")] [Min(0)] public float rentPerDay = 0f;
    [Tooltip("Walk-in takeaway income per popularity point above the threshold, paid when service closes.")] [Min(0)] public float walkInIncomePerPopularity = 0f;
    [Min(0)] public float walkInPopularityThreshold = 450f;
    [Tooltip("Indulgent plates draw crowds: extra popularity per point of plate appeal above popularityAppealPivot (scaled by requests answered). 0 turns it off.")] [Min(0)] public float popularityPerAppealPoint = 0f;
    [Range(0, 10)] public float popularityAppealPivot = 7f;
    [Tooltip("If on, a dish's Daily Dozen health bonus counts only groups this guest has not eaten before in the run.")] public bool dailyDozenNewGroupsOnly = false;
    [Tooltip("A Comfort request is met by any built plate whose average ingredient appeal is at least this.")] [Range(0, 10)] public float comfortMinimumAppeal = 7f;
    [Tooltip("Recipe fatigue: servings of one recipe (all guests) within this many days, counting today.")] [Min(1)] public int recipeFatigueWindowDays = 2;
    [Tooltip("Recipe fatigue starts when a recipe has already been served this many times in the window. 0 turns it off.")] [Min(0)] public int recipeFatigueThreshold = 0;
    [Tooltip("Positive popularity from a fatigued recipe is multiplied by this.")] [Range(0, 1)] public float recipeFatigueMultiplier = 0.5f;
    [Tooltip("At most once a day, a farmer gives free low-appeal food for one recipe when nothing on the menu can be completed.")] public bool foodAidEnabled = false;
    [Tooltip("Farmer gifts are green foods with ingredient appeal at or below this.")] [Range(0, 10)] public float foodAidMaxAppeal = 4f;
    [Tooltip("Days before gifted food spoils.")] [Min(1)] public int foodAidShelfLifeDays = 1;
    [Tooltip("Pantry batches sell back for this share of the lower of the cheapest shelf price and the price paid, per serving.")] [Range(0, 1)] public float pantrySellRate = 0f;
    [Tooltip("Popularity lost per active craving a built dish leaves unresolved (day cravingsStartDay onward).")] [Min(0)] public float popularityPerMissedCraving = 0f;
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
