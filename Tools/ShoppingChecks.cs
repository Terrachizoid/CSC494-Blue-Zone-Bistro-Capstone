// Regression tests for checkout atomicity, per-checkout sorting, the bistro gate, unique food questions, mastery, and one-time refunds.
using System;
using System.Collections.Generic;
public static class ShoppingChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static string Run()
    {
        var state = new GameSessionState { money = 100 };
        var balance = new BalanceConfig();
        var green = new FoodData { id = "G", trafficLight = TrafficLight.Green, storageType = StorageType.Fresh };
        var red = new FoodData { id = "R", trafficLight = TrafficLight.Red, storageType = StorageType.Fresh };
        var foods = new FoodDatabase(); foods.SetItems(new List<FoodData> { green, red });
        var store = new StoreData { id = "S" };
        var a = new StoreListingData { id = "A", food = green, store = store, gamePrice = 5, servingsPerPackage = 5 };
        var b = new StoreListingData { id = "B", food = green, store = store, gamePrice = 5, servingsPerPackage = 5 };
        var c = new StoreListingData { id = "C", food = red, store = store, gamePrice = 5, servingsPerPackage = 5 };
        var listings = new StoreListingDatabase(); listings.SetItems(new List<StoreListingData> { a,b,c });
        var customers = new CustomerDatabase();
        var purchase = new PurchaseService(state, listings, balance);
        var sorting = new FoodSortingService(state, foods);
        var loop = new GameLoop(purchase, new MealService(state, foods, new RecipeDatabase(), customers, balance),
            new DayService(state,balance), new CustomerVisitService(state,customers,balance), null, sorting);
        var cart = new List<CartEntry> { new CartEntry { listingId="A", packageQuantity=2 },
            new CartEntry { listingId="B" }, new CartEntry { listingId="C" } };
        Check(loop.QuoteCart(cart).total == 20 && state.money == 100 && state.inventory.Count == 0, "Quote mutated state");
        Check(!loop.Buy("A").success, "Direct buy bypassed the cart");
        state.money = 19;
        Check(!loop.Checkout(cart).success && state.inventory.Count == 0 && state.shoppingReceipt == null, "Overbudget partial purchase");
        state.money = 100; c.unlockDay = 2;
        Check(!loop.Checkout(cart).success && state.money == 100 && state.inventory.Count == 0, "Locked last item partially purchased cart");
        c.unlockDay = 1; balance.maximumSortingDiscount = float.NaN;
        Check(!loop.Checkout(cart).success && state.money == 100, "Invalid discount accepted");
        balance.maximumSortingDiscount = .3f;
        var old = new InventoryEntry { foodId="G", servings=2, purchaseCostPerServing=9, daysRemaining=-1 };
        state.inventory.Add(old);
        Check(loop.Checkout(cart).success && state.money == 80 && state.inventory.Count == 5 &&
            loop.Phase == GamePhase.FoodSorting && loop.SortingQuestions.Count == 2, "Checkout batches or deduplication failed");
        Check(state.inventory[1].inventoryEntryId != state.inventory[2].inventoryEntryId && state.inventory[1].listingId == "A", "Batch identity lost");
        Check(!loop.BeginService() && !loop.ReturnToMarket() && !loop.EnterBistro() && !loop.FinishDay().success && !loop.Checkout(cart).success, "Unsubmitted phase bypass");
        var answers = new List<SortingChoice> { new SortingChoice { foodId="g", answer=TrafficLight.Green },
            new SortingChoice { foodId="R", answer=TrafficLight.Green } };
        Check(!loop.SubmitSorting(new List<SortingChoice>()).success, "Incomplete answers accepted");
        Check(!loop.SubmitSorting(new List<SortingChoice> { answers[0],answers[0] }).success, "Duplicate answers accepted");
        Check(state.money == 80 && state.correctlySortedFoodIds.Count == 0, "Invalid answers mutated state");
        var result = loop.SubmitSorting(answers);
        // Refund = 30% (test rate) of the $15 paid for the correctly sorted green food only; the wrong red food earns nothing.
        Check(result.success && result.accuracy == .5f && result.refundAmount == 4.5f && state.money == 84.5f &&
            loop.Summary.moneySpent == 15.5f && state.correctlySortedFoodIds.Count == 1 && state.correctlySortedFoodIds[0] == "G", "Accuracy/refund/mastery incorrect");
        Check(Math.Abs(state.inventory[1].purchaseCostPerServing - .7f) < .0001 && Math.Abs(state.inventory[4].purchaseCostPerServing - 1f) < .0001 &&
            old.purchaseCostPerServing == 9, "Refund cost allocation incorrect");
        Check(!loop.SubmitSorting(answers).success && state.money == 84.5f && loop.Summary.moneySpent == 15.5f, "Double refund");
        Check(loop.ReturnToMarket() && loop.Phase == GamePhase.Market, "Sorting did not return to the market");
        // A second checkout the same day gets its own receipt and sorting round.
        Check(loop.Checkout(new List<CartEntry> { new CartEntry { listingId="C" } }).success && loop.Phase == GamePhase.FoodSorting &&
            loop.SortingQuestions.Count == 1 && loop.SortingQuestions[0].foodId == "R" && state.money == 79.5f, "Wrong food failed to return or mastered food repeated");
        Check(loop.SubmitSorting(new List<SortingChoice> { new SortingChoice { foodId="R", answer=TrafficLight.Red } }).refundAmount == 1.5f && state.money == 81f,
            "Retry on later purchase must refund only the new food's own price");
        Check(loop.ReturnToMarket() && loop.EnterBistro() && loop.Phase == GamePhase.Results && loop.FinishDay().success, "Bistro did not start service");
        float before = state.money;
        Check(loop.Checkout(cart).success && loop.Phase == GamePhase.Market && state.money == before-20 && sorting.GetQuestions().Count == 0,
            "Known-food-only cart granted refund or left the market");
        Check(loop.Checkout(cart).success && state.money == before-40 && loop.Phase == GamePhase.Market, "Known-food-only second checkout failed");
        Check(loop.EnterBistro() && loop.Phase == GamePhase.Results, "Known-food-only day failed to open the bistro");
        loop.FinishDay(); before = state.money;
        Check(loop.EnterBistro() && loop.Phase == GamePhase.Results && state.money == before, "Opening the bistro without shopping failed");
        return "PASS: cart atomicity, per-checkout sorting, phase guards, bistro gate, batch IDs, unique questions, partial refunds, correct-only mastery, later retries, and no-shopping/known-only days.";
    }
}
