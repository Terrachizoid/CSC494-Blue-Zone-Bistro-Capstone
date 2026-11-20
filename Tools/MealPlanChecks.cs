// Regression checks for the day's meal plan (MealPlanService): pin limits, summed category needs, pantry + cart,
// served dishes leaving the plan, and the daily reset.
using System;
using System.Collections.Generic;

public static class MealPlanChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static string Run()
    {
        var grain = new FoodData { id = "G", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green, storageType = StorageType.Dry };
        var veg = new FoodData { id = "V", recipeCategory = FoodCategory.Vegetable, trafficLight = TrafficLight.Green, storageType = StorageType.Fresh };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { grain, veg });
        var store = new StoreData { id = "S" };
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> {
            new StoreListingData { id = "LV", food = veg, store = store, servingsPerPackage = 2, gamePrice = 2 } });
        var sandwich = new RecipeData { id = "R1", displayName = "Sandwich", slots = new[] {
            new RecipeSlot { category = FoodCategory.Grain, servings = 2 }, new RecipeSlot { category = FoodCategory.Vegetable, servings = 1 },
            new RecipeSlot { category = FoodCategory.Vegetable, servings = 1, optional = true } } };
        var bowl = new RecipeData { id = "R2", displayName = "Bowl", unlockDay = 1, slots = new[] { new RecipeSlot { category = FoodCategory.Grain, servings = 1 } } };
        var later = new RecipeData { id = "R3", unlockDay = 5, slots = new[] { new RecipeSlot { category = FoodCategory.Grain, servings = 1 } } };
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData> { sandwich, bowl, later });
        var state = new GameSessionState();
        state.dailyCustomerIds.AddRange(new[] { "U1", "U2", "U3" });
        state.inventory.Add(new InventoryEntry { foodId = "G", servings = 3, daysRemaining = -1 });
        state.inventory.Add(new InventoryEntry { foodId = "V", servings = 5, daysRemaining = 0 }); // spoiled: not counted
        var plan = new MealPlanService(state, foods, recipes, listings);

        Check(plan.Report(null, null).Empty, "New plan not empty");
        Check(!plan.Add("R3") && !plan.Add("NOPE"), "Locked or unknown recipe planned");
        Check(plan.Add("R1") && plan.Add("R1") && plan.Add("R2") && !plan.Add("R2") && plan.Total == 3, "Plan limit is not one per guest");
        var report = plan.Report(new List<CartEntry>(), new string[0]);
        var g = report.needs.Find(n => n.category == FoodCategory.Grain);
        var v = report.needs.Find(n => n.category == FoodCategory.Vegetable);
        // 2 sandwiches need 4 grain + 2 veg (+2 optional veg); 1 bowl needs 1 grain. Pantry: 3 usable grain, no usable veg.
        Check(g.required == 5 && g.have == 3 && g.Missing == 2 && v.required == 2 && v.optional == 2 && v.have == 0 && v.Missing == 2 &&
            report.Missing == 4 && !report.Ready, "Plan needs not summed across dishes");
        var cart = new List<CartEntry> { new CartEntry { listingId = "LV", packageQuantity = 1 } };
        Check(plan.Report(cart, null).needs.Find(n => n.category == FoodCategory.Vegetable).Missing == 0, "Cart not counted");
        report = plan.Report(cart, new[] { "R1", "R2" });
        g = report.needs.Find(n => n.category == FoodCategory.Grain);
        Check(report.Remaining == 1 && g.required == 2 && g.Missing == 0 && report.Ready, "Served dishes still counted in the plan");
        Check(plan.Remove("R1") && plan.Planned("R1") == 1 && !plan.Remove("R3"), "Remove failed");
        state.currentDay = 2;
        Check(plan.Total == 0 && plan.Report(null, null).Empty, "Plan did not reset for a new day");
        CheckSuggestions();
        return "PASS: meal plan limits, summed needs, pantry + cart, served dishes, daily reset, and navigation suggestions.";
    }

    // Navigation helper: open stores, unlocked listings, affordable first, else the cheapest.
    private static void CheckSuggestions()
    {
        var grain = new FoodData { id = "G", recipeCategory = FoodCategory.Grain };
        var oats = new FoodData { id = "O", recipeCategory = FoodCategory.Grain };
        var veg = new FoodData { id = "V", recipeCategory = FoodCategory.Vegetable };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { grain, oats, veg });
        var open = new StoreData { id = "S1", unlockDay = 1 };
        var later = new StoreData { id = "S2", unlockDay = 4 };
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> {
            new StoreListingData { id = "cheap", food = grain, store = open, shelfId = "A", servingsPerPackage = 2, gamePrice = 2 },
            new StoreListingData { id = "dear", food = oats, store = open, shelfId = "A", servingsPerPackage = 2, gamePrice = 9 },
            new StoreListingData { id = "locked", food = grain, store = open, shelfId = "A", servingsPerPackage = 2, gamePrice = 1, unlockDay = 3 },
            new StoreListingData { id = "closedstore", food = grain, store = later, shelfId = "B", servingsPerPackage = 2, gamePrice = 1 },
            new StoreListingData { id = "veg", food = veg, store = open, shelfId = "C", servingsPerPackage = 2, gamePrice = 1 } });
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData>());
        var customers = new CustomerDatabase(); customers.SetItems(new List<CustomerData>());
        var state = new GameSessionState { money = 5 };
        var queries = new GameContentQueries(state, foods, listings, recipes, customers);
        var random = new Random(1);
        for (int i = 0; i < 10; i++)
            Check(queries.SuggestListing(FoodCategory.Grain, random)?.id == "cheap", "Suggested a locked, unaffordable or not-yet-open listing");
        state.money = 0;
        Check(queries.SuggestListing(FoodCategory.Grain, random)?.id == "cheap", "No cheapest fallback when nothing is affordable");
        state.money = 20;
        Check(queries.SuggestListing(FoodCategory.Grain, random, "cheap")?.id == "dear", "Repeat of the last suggestion not avoided");
        Check(queries.SuggestListing(FoodCategory.Fruit, random) == null, "Suggested food for a category nobody sells");
    }
}
