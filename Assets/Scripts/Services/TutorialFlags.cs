// Stable event flags for future tutorials. They describe committed actions, never previews.
public static class TutorialFlags
{
    // Phase triggers for the day-1 lessons (T_SHOPPING, T_SORTING, T_COOKING in Tutorials.csv).
    public const string ShoppingAvailable="tutorial.shopping_available";
    public const string SortingAvailable="tutorial.sorting_available";
    public const string BistroAvailable="tutorial.bistro_available"; // Day 1, back at the market after the first purchase (T_BISTRO).
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
    // "First time on this screen" tips (future rows use this prefix). Like toolbar clicks they are contextual:
    // set during a hard lesson they are dropped and the flag cleared, so the tip shows the next time instead of late.
    public const string SeenPrefix="tutorial.seen_";
    public static bool Contextual(string flag)=>flag!=null&&(flag.StartsWith(ToolbarPrefix)||flag.StartsWith(SeenPrefix));
    // Text lessons added 2026-11-18 (Tutorials.csv). "First" flags are set once, the first time it happens.
    public const string SeenStore=SeenPrefix+"store", SeenShelf=SeenPrefix+"shelf", SeenPantry=SeenPrefix+"pantry", SeenGuests=SeenPrefix+"guests";
    public const string ClickedLockedStore="tutorial.clicked_locked_store";   // a store that has not opened yet
    public const string ClickedClosedStore="tutorial.clicked_closed_store";   // a store closed by an event
    public const string StoreClosedEvent="tutorial.store_closed_event";       // an event has closed a store today
    public const string NewRecipes="tutorial.new_recipes";                    // recipes unlocked this morning (day 2+)
    public const string SecondCheckout="tutorial.first_second_checkout";
    public const string SortMistake="tutorial.first_sort_mistake";
    public const string SkippedCustomer="tutorial.first_skip_customer";
    public const string IncompleteDish="tutorial.first_incomplete_dish";      // served without building the recipe exactly
    public const string CityDuringService="tutorial.first_city_during_service";
    public const string FoodExpiring="tutorial.food_expiring";                // some usable food has one day left (shown once)
    // Added 2026-11-20 (supervisor notes): a correctly built dish still lost popularity (missed requests), and the first Comfort request.
    public const string PopularityDrop="tutorial.first_popularity_drop";
    public const string ComfortRequest="tutorial.first_comfort_request";
}
