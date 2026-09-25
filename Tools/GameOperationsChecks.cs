// Regression scenarios for service atomicity, economy, inventory, attendance, and day progression.
// Fixtures are isolated from authored content; Test-CsvContent exercises the real CSV dataset separately.
using System;
using System.Collections.Generic;

public static class GameOperationsChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static string Run()
    {
        var state = new GameSessionState();
        var balance = new BalanceConfig { totalDays = 3, startingFinance = 0 };
        var food = new FoodData { id = "F1", storageType = StorageType.Fresh,
            recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green,
            dailyDozenCategories = new[] { DailyDozenCategory.WholeGrains, DailyDozenCategory.WholeGrains } };
        var store = new StoreData { id = "S1" };
        var listing = new StoreListingData { id = "L1", food = food, store = store,
            servingsPerPackage = 4, gamePrice = 5 };
        var recipe = new RecipeData { id = "R1", baseSalePrice = 8, delightValue = 2,
            slots = new[] { new RecipeSlot { category = FoodCategory.Grain, servings = 2 } } };
        var customer = new CustomerData { id = "U1", startingHealth = 50 };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { food });
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> { listing });
        foods.RebuildMinimumPrices(listings.Listings);
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData> { recipe });
        var customers = new CustomerDatabase(); customers.SetItems(new List<CustomerData> { customer });
        var purchases = new PurchaseService(state, listings, balance);
        var meals = new MealService(state, foods, recipes, customers, balance);
        var loop = new GameLoop(purchases, meals, new DayService(state, balance), new CustomerVisitService(state, customers, balance, () => 0));
        Check(!loop.FinishDay().success && state.currentDay == 1, "Phase guard failed");
        listing.unlockDay = 2;
        Check(!loop.Buy("L1").success && state.money == 30, "Locked listing mutated state");
        listing.unlockDay = 1;
        store.requiresMembership = true;
        Check(!loop.Buy("L1").success, "Membership bypass");
        store.requiresMembership = false;
        listing.gamePrice = 31;
        Check(!loop.Buy("L1").success && state.inventory.Count == 0, "Unaffordable purchase mutated state");
        listing.gamePrice = 5;
        var buy = loop.Buy("L1");
        var second = loop.Buy("L1");
        Check(buy.success && buy.daysRemaining == 2 && state.money == 20 &&
            buy.inventoryEntryId != second.inventoryEntryId, "Purchase batches failed");
        var input = new ServeInput { customerId = "U1", recipeId = "R1",
            ingredients = new List<IngredientSelection> {
                new IngredientSelection { inventoryEntryId = buy.inventoryEntryId, servingsUsed = 3 },
                new IngredientSelection { inventoryEntryId = buy.inventoryEntryId, servingsUsed = 3 } } };
        Check(!loop.Serve(input).success, "Serve allowed in Market");
        Check(loop.BeginService() && !loop.Buy("L1").success, "Market transition failed");
        Check(!loop.Serve(input).success && state.inventory[0].servings == 4 && state.money == 20,
            "Duplicate input overspending or failure atomicity");
        input.ingredients.RemoveAt(1);
        input.ingredients[0].servingsUsed = 1;
        Check(!loop.PreviewServe(input).recipeBuilt && state.inventory[0].servings == 4, "Missing required slot preview failed");
        input.ingredients[0].servingsUsed = 3;
        Check(!loop.PreviewServe(input).recipeBuilt, "Overfilled recipe marked built");
        input.ingredients[0].servingsUsed = 2;
        var served = loop.Serve(input);
        Check(served.success && served.greenServings == 2 && served.dailyDozenCovered.Length == 1 &&
            served.healthDelta == 3 && state.customers[0].health == 53 && state.money == 28 &&
            state.inventory[0].servings == 2, "Serve aggregation or mutation failed");
        Check(!loop.Serve(input).success && state.money == 28, "Repeated customer rewarded twice");
        Check(loop.ContinueAfterDish() && loop.Phase == GamePhase.Results, "Results transition failed");
        Check(loop.FinishDay().currentDay == 2 && state.inventory[0].daysRemaining == 1,
            "Day ageing failed");
        food.trafficLight = TrafficLight.Red;
        loop.BeginService();
        recipe.allowNonGreenChoices = false;
        Check(!loop.PreviewServe(input).recipeBuilt, "Non-green restriction did not mark mismatch");
        recipe.allowNonGreenChoices = true;
        served = loop.Serve(input);
        Check(served.success && served.rushCrashTriggered && served.redServings == 2 &&
            served.healthDelta == -6, "Red scoring or rush trigger failed");
        state.inventory.Add(new InventoryEntry { foodId = "F1", servings = 1, daysRemaining = -1 });
        loop.ContinueAfterDish();
        var day = loop.FinishDay();
        Check(day.currentDay == 3 && day.expiredInventoryEntryIds.Length == 1 &&
            state.inventory.Count == 1 && state.inventory[0].daysRemaining == -1, "Expiry failed");
        loop.BeginService(); loop.SkipCustomer();
        Check(loop.FinishDay().gameCompleted && loop.Phase == GamePhase.Completed &&
            !loop.FinishDay().success && state.currentDay == 3, "Completion guard failed");
        CheckFlowAndRecipes();
        CheckFlexibleMeals();
        CheckAttendanceAndFinance();
        return "PASS: operations, previews, minimum-price rebuilds, finance tips, two-day health lag, attendance without rerolls, dish caps, and delight bounds.";
    }

    private static void CheckFlowAndRecipes()
    {
        var state = new GameSessionState();
        var food = new FoodData { id = "F", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { food });
        var recipes = new RecipeDatabase();
        var recipe = new RecipeData { id = "R", baseSalePrice = 2,
            slots = new[] { new RecipeSlot { category = FoodCategory.Grain, servings = 2 },
                new RecipeSlot { category = FoodCategory.Vegetable, servings = 1, optional = true } } };
        recipes.SetItems(new List<RecipeData> { recipe });
        var customers = new CustomerDatabase(); customers.SetItems(new List<CustomerData> {
            new CustomerData { id = "A" }, new CustomerData { id = "B" }, new CustomerData { id = "C", unlockDay = 2 } });
        var balance = new BalanceConfig { startingFinance = 0 };
        var listings = new StoreListingDatabase();
        foods.RebuildMinimumPrices(new[] { new StoreListingData { food = food,
            store = new StoreData(), gamePrice = 2, servingsPerPackage = 4 } });
        var visits = new CustomerVisitService(state, customers, balance, () => 0);
        var checker = new RecipeCheck(foods);
        Check(!checker.CanMake(recipe, state), "Empty inventory offered recipe");
        state.inventory.Add(new InventoryEntry { foodId = "F", servings = 1 });
        state.inventory.Add(new InventoryEntry { foodId = "F", servings = 1, daysRemaining = 0 });
        Check(!checker.CanMake(recipe, state), "Expired stock counted");
        state.inventory[1].daysRemaining = 1;
        Check(checker.CanMake(recipe, state), "Split batches or absent optional slot rejected");
        recipe.slots[1].category = FoodCategory.Grain; recipe.slots[1].optional = false;
        Check(!checker.CanMake(recipe, state), "Repeated category requirements not combined");
        recipe.slots[1].optional = true;
        food.trafficLight = TrafficLight.Red; recipe.allowNonGreenChoices = false;
        Check(!checker.CanMake(recipe, state), "Red stock offered for green-only recipe");
        recipe.allowNonGreenChoices = true;
        Check(checker.CanMake(recipe, state), "Allowed red recipe hidden");
        recipe.unlockDay = 2;
        Check(!checker.CanMake(recipe, state), "Locked recipe offered");
        recipe.unlockDay = 1;
        var loop = new GameLoop(new PurchaseService(state, listings, balance),
            new MealService(state, foods, recipes, customers, balance), new DayService(state, balance), visits);
        loop.BeginService();
        Check(loop.Service.encounters.Count == 2 && loop.Service.CurrentCustomerId == "A", "Visit policy failed");
        Check(!loop.BeginService() && !loop.ShowResults(), "Queue can restart or end prematurely");
        var input = new ServeInput { customerId = "B", recipeId = "R", ingredients = new List<IngredientSelection> {
            new IngredientSelection { inventoryEntryId = state.inventory[0].inventoryEntryId, servingsUsed = 1 },
            new IngredientSelection { inventoryEntryId = state.inventory[1].inventoryEntryId, servingsUsed = 1 } } };
        Check(!loop.Serve(input).success && state.inventory.Count == 2, "Out-of-order serving allowed");
        Check(loop.SkipCustomer() && loop.Service.CurrentCustomerId == "B" && loop.Summary.skippedCustomerIds.Count == 1,
            "Skip failed");
        Check(loop.Serve(input).success && loop.Summary.dishes.Count == 1 && loop.Summary.moneyEarned == 2,
            "Dish summary failed");
        Check(!loop.Serve(input).success && loop.Summary.dishes.Count == 1, "Duplicate submission recorded");
        Check(loop.SkipCustomer() && loop.Phase == GamePhase.Results && loop.Summary.skippedCustomerIds.Count == 1,
            "Acknowledged dish incorrectly marked skipped");
        Check(loop.FinishDay().success && loop.Summary.dishes.Count == 0 && loop.Summary.moneyEarned == 0,
            "Daily summary not reset");
        customers.SetItems(new List<CustomerData>());
        Check(loop.BeginService() && loop.Phase == GamePhase.Results, "Empty queue did not enter Results");
    }

    private static void CheckFlexibleMeals()
    {
        var state = new GameSessionState();
        var chips = new FoodData { id = "chips", recipeCategory = FoodCategory.StarchyVegetable,
            trafficLight = TrafficLight.Red, storageType = StorageType.ShelfStable };
        var greens = new FoodData { id = "greens", recipeCategory = FoodCategory.Vegetable,
            trafficLight = TrafficLight.Green, storageType = StorageType.Fresh,
            dailyDozenCategories = new[] { DailyDozenCategory.Greens } };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { chips, greens });
        var recipe = new RecipeData { id = "soup", baseSalePrice = 10, delightValue = 2,
            slots = new[] { new RecipeSlot { category = FoodCategory.Vegetable, servings = 1 } } };
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData> { recipe });
        var customers = new CustomerDatabase(); customers.SetItems(new List<CustomerData> {
            new CustomerData { id = "A" }, new CustomerData { id = "B" }, new CustomerData { id = "C" } });
        var listing = new StoreListingData { id = "chips-pack", food = chips,
            store = new StoreData { id = "store" }, gamePrice = 8, servingsPerPackage = 8 };
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> { listing });
        foods.RebuildMinimumPrices(new[] { listing, new StoreListingData { food = chips,
            store = new StoreData { unlockDay = 5 }, gamePrice = 4, servingsPerPackage = 8 },
            new StoreListingData { food = greens, store = new StoreData(), gamePrice = 2, servingsPerPackage = 1 } });
        var balance = new BalanceConfig();
        var purchase = new PurchaseService(state, listings, balance).Buy(listing.id);
        Check(purchase.success && state.inventory[0].purchaseCostPerServing == 1, "Purchase cost not captured");
        listing.gamePrice = 80; // Historical cost stays recorded; rewards use the imported minimum table.
        var service = new MealService(state, foods, recipes, customers, balance);
        var input = new ServeInput { customerId = "A", recipeId = "soup", ingredients = new List<IngredientSelection> {
            new IngredientSelection { inventoryEntryId = purchase.inventoryEntryId, servingsUsed = 5 } } };
        var preview = service.Preview(input);
        Check(preview.success && !preview.recipeBuilt && preview.recipeWarnings.Length == 2 &&
            preview.ingredientCost == 5 && preview.minimumIngredientValue == 2.5f && preview.basePayment == 1.875f && preview.tip == 0 &&
            preview.delightDelta == -30 && state.money == 22 && state.customers.Count == 0 && state.inventory[0].servings == 8,
            "Mismatch preview or preview mutation");
        var served = service.Serve(input);
        Check(served.success && !served.recipeBuilt && served.healthDelta == -15 && served.rushCrashTriggered &&
            state.inventory[0].servings == 3 && state.money == 23.875f && state.customers[0].delight == 20 &&
            served.cravingsResolved.Length == 0, "Mismatch serving consequences failed");
        var entry = new InventoryEntry { foodId = "greens", servings = 3, purchaseCostPerServing = 2 };
        state.inventory.Add(entry);
        input.customerId = "B"; input.ingredients[0].inventoryEntryId = entry.inventoryEntryId;
        input.ingredients[0].servingsUsed = 2;
        preview = service.Preview(input);
        Check(preview.success && !preview.recipeBuilt && preview.recipeWarnings[0].Contains("Surplus Vegetable: 1") &&
            preview.tip == 0, "Surplus green farming treated as correct recipe");
        input.ingredients[0].servingsUsed = 1;
        served = service.Serve(input);
        Check(served.recipeBuilt && served.basePayment == 10 && served.tip == 2 && served.moneyEarned == 12 &&
            served.delightDelta == 4, "Correct recipe payment failed");
        input.customerId = "C"; input.ingredients[0].servingsUsed = 1;
        Check(service.Preview(input).success, "Expected valid preview");
        entry.daysRemaining = 0;
        Check(!service.Serve(input).success && state.customers.Count == 2, "Stale expired stock was consumed");
        entry.daysRemaining = 1; input.ingredients.Clear();
        Check(!service.Preview(input).success, "Empty dish allowed");
        input.ingredients.Add(new IngredientSelection { inventoryEntryId = entry.inventoryEntryId, servingsUsed = 50 });
        entry.servings = 50;
        preview = service.Preview(input);
        Check(preview.success && !preview.recipeBuilt && preview.healthDelta == 20 && preview.delightDelta == -30,
            "Large mismatched dish bypassed caps");
        recipe.slots[0].servings = 50;
        preview = service.Preview(input);
        Check(preview.recipeBuilt && preview.delightDelta == 20 && preview.healthDelta == 20, "Correct dish bypassed gain caps");
        state.customers.Add(new CustomerState { customerId = "C", health = 95, delight = 95, finance = 80 });
        preview = service.Preview(input);
        Check(preview.healthDelta == 5 && preview.delightDelta == 5 && preview.tip == 8, "State bounds or finance tip failed");
        recipe.slots[0].servings = 1;
        state.customers[2].delight = 5;
        Check(service.Preview(input).delightDelta == -5, "Delight went below zero");
        foods.RebuildMinimumPrices(new StoreListingData[0]);
        Check(!service.Preview(input).success, "Missing minimum price silently valued as zero");
    }

    private static void CheckAttendanceAndFinance()
    {
        var state = new GameSessionState();
        var balance = new BalanceConfig();
        var db = new CustomerDatabase();
        db.SetItems(new List<CustomerData> { new CustomerData { id = "A" },
            new CustomerData { id = "B" }, new CustomerData { id = "C", unlockDay = 4 } });
        int rolls = 0;
        var visits = new CustomerVisitService(state, db, balance, () => ++rolls == 1 ? 0.49 : 0.5);
        var queue = visits.GetTodaysCustomers();
        Check(queue.Count == 1 && queue[0] == "A" && state.customers[0].delight == 50 &&
            state.customers[0].finance == 20, "Starting state or 50 percent threshold wrong");
        visits.GetTodaysCustomers();
        Check(rolls == 2, "Attendance rerolled within same day");
        state.customers[0].lastServedDay = 1;
        Check(visits.GetTodaysCustomers().Count == 0, "Customer visited twice");
        state.customers[0].health = 70;
        state.customers[1].health = 30;
        var days = new DayService(state, balance, db);
        days.FinishDay();
        Check(state.customers[0].finance == 20 && state.customers[1].finance == 20, "Finance updated before lag");
        state.customers[0].delight = 0;
        state.customers[1].delight = 100;
        Check(visits.GetTodaysCustomers().Count == 1 && visits.GetTodaysCustomers()[0] == "B", "0/100 attendance incorrect");
        state.customers[0].health = 80;
        days.FinishDay();
        Check(state.currentDay == 3 && state.customers[0].finance == 40 && state.customers[1].finance == 0,
            "Finance must apply day 1 health delta on day 3, even when absent");
        days.FinishDay();
        Check(state.customers[0].finance == 50 && state.customers[1].finance == 0,
            "Finance did not apply day 2 delta, or unchanged health raised finance");
        days.FinishDay();
        Check(state.customers[0].finance == 50, "Unchanged health reapplied a previous gain");

        var unchangedState = new GameSessionState();
        var unchangedDays = new DayService(unchangedState, balance, db);
        unchangedDays.FinishDay(); unchangedDays.FinishDay();
        Check(unchangedState.customers[0].finance == 20,
            "Starting health caused a finance jump without any health change");

        var foods = new FoodDatabase(); var food = new FoodData { id = "Oats" };
        foods.SetItems(new List<FoodData> { food });
        var store = new StoreData();
        var prices = new[] { new StoreListingData { food = food, store = store, gamePrice = 8, servingsPerPackage = 8 },
            new StoreListingData { food = food, store = store, gamePrice = 2, servingsPerPackage = 4 },
            new StoreListingData { food = food, store = store, gamePrice = -1, servingsPerPackage = 4 },
            new StoreListingData { food = food, store = store, gamePrice = 0, servingsPerPackage = 0 } };
        foods.RebuildMinimumPrices(prices);
        Check(foods.TryGetMinimumServingPrice("oats", out float minimum) && minimum == 0.5f, "Minimum unit price incorrect");
        foods.RebuildMinimumPrices(new[] { prices[0] });
        Check(foods.TryGetMinimumServingPrice("Oats", out minimum) && minimum == 1, "Stale cheapest price survived rebuild");
    }
}
