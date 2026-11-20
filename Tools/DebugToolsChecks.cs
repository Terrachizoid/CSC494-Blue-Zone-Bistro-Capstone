// Debug panel shortcuts (GameDebugTools, GameLoop.DebugEndDay): they must use the real rules and never stack effects.
using System;
using System.Collections.Generic;
using System.Linq;

public static class DebugToolsChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static string Run()
    {
        var state = new GameSessionState { money = 20 };
        var balance = new BalanceConfig { totalDays = 4, rentStartDay = 3, rentPerDay = 5, maximumPopularity = 1000 };
        var store = new StoreData { id = "S", unlockDay = 1 };
        var cheap = new FoodData { id = "G1", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green, storageType = StorageType.Dry };
        var dear = new FoodData { id = "G2", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green, storageType = StorageType.Dry };
        var red = new FoodData { id = "R1", recipeCategory = FoodCategory.Sweet, trafficLight = TrafficLight.Red, storageType = StorageType.Dry };
        var listings = new StoreListingDatabase();
        listings.SetItems(new List<StoreListingData> {
            new StoreListingData { id = "L1", store = store, food = cheap, gamePrice = 4, servingsPerPackage = 4, unlockDay = 1 },
            new StoreListingData { id = "L2", store = store, food = dear, gamePrice = 9, servingsPerPackage = 3, unlockDay = 1 },
            new StoreListingData { id = "L3", store = store, food = red, gamePrice = 1, servingsPerPackage = 1, unlockDay = 1 } });
        var events = new EventDatabase();
        events.events.Add(new EventDefinition { id = "SALE", minDay = 5, durationDays = 2, effects = new List<EventEffect> {
            new EventEffect { type = EventEffectType.Price, target = EventTarget.Store, targetId = "S", operation = EventOperation.Multiply, value = .5f } } });
        var tools = new GameDebugTools(state, events, listings, balance);

        tools.AddMoney(10.005f);
        Check(Math.Abs(state.money - 30.01f) < .001f, "AddMoney did not round to cents");
        tools.AddPopularity(5000);
        Check(state.popularity == 1000, "AddPopularity ignored the maximum");
        tools.AddPopularity(-5000);
        Check(state.popularity == 0, "AddPopularity went below zero");

        Check(tools.StartEvent("NOPE") != null, "Unknown event accepted");
        Check(tools.StartEvent("sale") == null && tools.ActiveEvents.Count == 1, "Event outside its day range did not start");
        Check(tools.StartEvent("SALE") == null && tools.ActiveEvents.Count == 1, "Starting an event twice stacked it");
        Check(state.events.Last().expiresOnDay == 3, "Event duration not applied");
        Check(new MarketEffects(state).Quote(listings.Listings[0]).effectivePrice == 2, "Started event has no effect");
        Check(tools.EndEvents() == 1 && tools.ActiveEvents.Count == 0, "EndEvents left an event running");

        Check(tools.StockPantry(4) == 1 && state.inventory.Count == 1, "Pantry stock should add one green food per type");
        Check(state.inventory[0].foodId == "G1" && state.inventory[0].servings == 4 && state.inventory[0].purchaseCostPerServing == 1,
            "Pantry stock did not pick the cheapest green food per serving");
        Check(tools.ClearPantry() == 1 && state.inventory.Count == 0, "ClearPantry failed");

        var customers = new CustomerDatabase(); customers.SetItems(new List<CustomerData> { new CustomerData { id = "U1", startingHealth = 50 } });
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { cheap, dear, red });
        foods.RebuildMinimumPrices(listings.Listings);
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData>());
        var loop = new GameLoop(new PurchaseService(state, listings, balance), new MealService(state, foods, recipes, customers, balance),
            new DayService(state, balance), new CustomerVisitService(state, customers, balance, () => 0));
        state.money = 20;
        var day = loop.DebugEndDay();
        Check(day.success && state.currentDay == 2 && loop.Phase == GamePhase.Market, "DebugEndDay from the market failed");
        loop.BeginService();
        day = loop.DebugEndDay();
        Check(day.success && state.currentDay == 3 && day.rentCharged == 5 && state.money == 15 && loop.Phase == GamePhase.Market,
            "DebugEndDay during service skipped rent or the day");
        loop.DebugEndDay();
        day = loop.DebugEndDay();
        Check(day.gameCompleted && loop.Phase == GamePhase.Completed && !loop.DebugEndDay().success, "DebugEndDay past the last day");
        return "PASS: debug shortcuts follow the game rules (events, pantry, money, popularity, day ends).";
    }
}
