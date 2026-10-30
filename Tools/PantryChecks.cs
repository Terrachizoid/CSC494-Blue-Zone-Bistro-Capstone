// Regression checks for selling pantry servings (PantryService) and the farmer's food aid (FoodAidService).
using System;
using System.Collections.Generic;

public static class PantryChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    public static string Run()
    {
        var food = new FoodData { id = "F1", storageType = StorageType.Fresh, recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green };
        var listing = new StoreListingData { id = "L1", food = food, store = new StoreData { id = "S1" }, servingsPerPackage = 4, gamePrice = 4 };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { food });
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> { listing });
        foods.RebuildMinimumPrices(listings.Listings); // 1.00 per serving
        var balance = new BalanceConfig { pantrySellRate = 0.75f };
        var state = new GameSessionState { money = -2 };
        var full = new InventoryEntry { foodId = "F1", servings = 4, purchaseCostPerServing = 1f, daysRemaining = 2 };
        var refunded = new InventoryEntry { foodId = "F1", servings = 2, purchaseCostPerServing = 0.7f, daysRemaining = -1 };
        var spoiled = new InventoryEntry { foodId = "F1", servings = 3, purchaseCostPerServing = 1f, daysRemaining = 0 };
        state.inventory.AddRange(new[] { full, refunded, spoiled });
        var pantry = new PantryService(state, foods, balance);
        List<PantrySaleItem> Items(InventoryEntry e, int n) => new List<PantrySaleItem> { new PantrySaleItem { inventoryEntryId = e.inventoryEntryId, servings = n } };

        Check(Near(pantry.ValuePerServing(full.inventoryEntryId), .75f), "Per-serving value must be 75% × 1.00");
        Check(Near(pantry.ValuePerServing(refunded.inventoryEntryId), .525f), "Sale must use the lower paid cost so buy-sort-sell cannot profit");
        var quote = pantry.Quote(Items(full, 2));
        Check(quote.success && Near(quote.amount, 1.5f) && full.servings == 4 && Near(state.money, -2), "Quote must not mutate");
        Check(!pantry.Sell(Items(spoiled, 1)).success && spoiled.servings == 3, "Spoiled food must not sell");
        Check(!pantry.Sell(Items(full, 5)).success && full.servings == 4, "Cannot sell more servings than the batch holds");
        var mixed = Items(full, 1); mixed.Add(new PantrySaleItem { inventoryEntryId = spoiled.inventoryEntryId, servings = 1 });
        Check(!pantry.Sell(mixed).success && full.servings == 4 && Near(state.money, -2), "A sale with one invalid batch must change nothing");

        var sale = pantry.Sell(Items(full, 3));
        Check(sale.success && Near(state.money, .25f) && full.servings == 1 && sale.servingsSold == 3, "Partial sale must add money and keep the rest");
        Check(state.flags.HasFlag(TutorialFlags.PantrySold), "Sale must set the first-sale flag");
        Check(pantry.Sell(Items(full, 1)).success && !state.inventory.Contains(full), "Emptied batch must leave the pantry");

        balance.pantrySellRate = 0;
        Check(!pantry.Sell(Items(refunded, 1)).success && refunded.servings == 2, "Rate 0 turns selling off");
        CheckFoodAid();
        return "PASS: pantry sale value, atomicity and guards; farmer food aid.";
    }

    private static void CheckFoodAid()
    {
        var oats = new FoodData { id = "OATS", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green, ingredientAppeal = 3, storageType = StorageType.Dry };
        var bread = new FoodData { id = "BREAD", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Green, ingredientAppeal = 8, storageType = StorageType.Fresh };
        var chips = new FoodData { id = "CHIPS", recipeCategory = FoodCategory.Grain, trafficLight = TrafficLight.Red, ingredientAppeal = 2, storageType = StorageType.Dry };
        var cabbage = new FoodData { id = "CABBAGE", recipeCategory = FoodCategory.Vegetable, trafficLight = TrafficLight.Green, ingredientAppeal = 4, storageType = StorageType.Fresh };
        var store = new StoreData { id = "S1" };
        var listingData = new List<StoreListingData>();
        foreach (var f in new[] { oats, bread, chips, cabbage }) listingData.Add(new StoreListingData { id = "L" + f.id, food = f, store = store, servingsPerPackage = 4, gamePrice = 2 });
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { oats, bread, chips, cabbage });
        var listings = new StoreListingDatabase(); listings.SetItems(listingData);
        foods.RebuildMinimumPrices(listings.Listings);
        Check(foods.InCategory(FoodCategory.Grain).Count == 3 && foods.InCategory(FoodCategory.Fruit).Count == 0, "Category index wrong");
        var recipe = new RecipeData { id = "R1", displayName = "Bowl", unlockDay = 1, baseSalePrice = 4,
            slots = new[] { new RecipeSlot { category = FoodCategory.Grain, servings = 2 }, new RecipeSlot { category = FoodCategory.Vegetable, servings = 1 } } };
        var recipes = new RecipeDatabase(); recipes.SetItems(new List<RecipeData> { recipe });
        var balance = new BalanceConfig { foodAidEnabled = true, foodAidMaxAppeal = 4, foodAidShelfLifeDays = 1 };
        var state = new GameSessionState { currentDay = 3 };
        var aid = new FoodAidService(state, foods, recipes, balance);

        var gift = aid.TryGive();
        Check(gift.given && state.inventory.Count == 2 && state.lastFoodAidDay == 3, "Empty pantry must receive one crate");
        Check(state.inventory.Exists(e => e.foodId == "OATS" && e.servings == 2 && e.daysRemaining == 1 && Math.Abs(e.purchaseCostPerServing - .5f) < .001f), "Gift must be low-appeal green grain, exact servings, 1-day life, shelf-price cost");
        Check(state.inventory.Exists(e => e.foodId == "CABBAGE" && e.servings == 1), "Gift must cover every required category");
        Check(!state.inventory.Exists(e => e.foodId == "BREAD" || e.foodId == "CHIPS"), "High-appeal or non-green food must never be gifted");
        Check(state.flags.HasFlag(TutorialFlags.FoodAidGiven), "Gift must set the farmer flag");
        state.inventory.Clear();
        Check(!aid.TryGive().given && state.inventory.Count == 0, "At most one gift per day");
        state.currentDay = 4; state.inventory.Add(new InventoryEntry { foodId = "BREAD", servings = 2 }); state.inventory.Add(new InventoryEntry { foodId = "CABBAGE", servings = 1 });
        Check(!aid.TryGive().given && state.inventory.Count == 2, "No gift when a recipe can already be made");
        state.inventory.Clear(); balance.foodAidEnabled = false;
        Check(!aid.TryGive().given, "Disabled aid must not give");
    }
}
