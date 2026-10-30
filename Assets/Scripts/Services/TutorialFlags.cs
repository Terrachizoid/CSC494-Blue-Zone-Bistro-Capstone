// Stable event flags for future tutorials. They describe committed actions, never previews.
public static class TutorialFlags
{
    // Phase triggers for the day-1 lessons (T_SHOPPING, T_SORTING, T_COOKING in Tutorials.csv).
    public const string ShoppingAvailable="tutorial.shopping_available";
    public const string SortingAvailable="tutorial.sorting_available";
    public const string CookingAvailable="tutorial.cooking_available";
    public const string Purchased="tutorial.first_purchase";
    public const string Served="tutorial.first_served";
    public const string BuiltRecipe="tutorial.first_recipe_built";
    public const string CrashExplained="tutorial.first_crash_dialogue_completed";
    public const string RentStarted="tutorial.rent_started";
    public const string RentUpcoming="tutorial.rent_upcoming"; // Set the day before rent starts.
    public const string MoneyNegative="tutorial.money_negative"; // Set the first time money is below zero.
    public const string MoneyLow="tutorial.money_low"; // Set once rent has started and money is below one day's rent.
    public const string PantrySold="tutorial.first_pantry_sale";
    public const string FoodAidGiven="tutorial.food_aid_given"; // The farmer gave free food (first time triggers T_FARMER).
    // Set when the player clicks a top-bar item (day, popularity, wallet). CSV lessons may trigger on them.
    public const string ToolbarPrefix="tutorial.toolbar_";
}
