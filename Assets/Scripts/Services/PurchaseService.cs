// File responsibility: Validates an offer/access/budget, then creates a priced inventory batch and deducts money.
// Shelf life is assigned from balance at purchase. Failure must leave the session unchanged.

using System;
using System.Collections.Generic;

public class PurchaseService : IPurchaseService, ICartPurchaseService
{
    private readonly GameSessionState state;
    private readonly StoreListingDatabase listings;
    private readonly BalanceConfig balance;
    private readonly MarketEffects effects;

    public PurchaseService(GameSessionState state, StoreListingDatabase listings, BalanceConfig balance)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        effects=new MarketEffects(state);
        this.listings = listings ?? throw new ArgumentNullException(nameof(listings));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
    }

    public PurchaseResult Buy(string listingId)
    {
        var listing = listings.GetById(listingId);
        string error = ValidateListing(listing);
        if (error != null) return Fail(error);
        if (!Finite(state.money) || state.money < effects.Quote(listing).effectivePrice) return Fail("Insufficient or invalid funds.");
        if (state.shoppingReceipt != null && state.shoppingReceipt.day == state.currentDay)
            return Fail("Today's checkout is already complete.");
        var entry = CreateBatch(listing);
        state.inventory.Add(entry);
        state.money -= effects.Quote(listing).effectivePrice;
        state.flags.SetFlag(TutorialFlags.Purchased);
        return new PurchaseResult { success = true, inventoryEntryId = entry.inventoryEntryId,
            foodId = entry.foodId, servingsAdded = entry.servings, daysRemaining = entry.daysRemaining,
            moneySpent = effects.Quote(listing).effectivePrice };
    }

    private string ValidateListing(StoreListingData listing)
    {
        if (listing == null || listing.food == null || listing.store == null)
            return "Listing, food, or store was not found.";
        if (state.currentDay < listing.unlockDay || state.currentDay < listing.store.unlockDay)
            return "This listing or store is locked.";
        if (!string.IsNullOrWhiteSpace(listing.requiredFlag) && !state.flags.HasFlag(listing.requiredFlag))
            return "The required unlock flag is missing.";
        string closed=effects.ClosureReason(listing.store.id);
        if(closed!=null)return closed;
        var price=effects.Quote(listing);if(!price.valid)return price.error;
        int days = balance.GetShelfLife(listing.food.storageType);
        if (days < -1 || days == 0 || listing.servingsPerPackage <= 0 ||
            string.IsNullOrWhiteSpace(listing.food.id) || float.IsNaN(listing.gamePrice) ||
            float.IsInfinity(listing.gamePrice) || listing.gamePrice < 0)
            return "Invalid price, servings, food ID, or shelf life.";
        if (listing.food.trafficLight != TrafficLight.Green && listing.food.trafficLight != TrafficLight.Yellow &&
            listing.food.trafficLight != TrafficLight.Red) return "Food has no valid sorting classification.";
        return null;
    }

    private InventoryEntry CreateBatch(StoreListingData listing) => new InventoryEntry
        {
            listingId = listing.id,
            foodId = listing.food.id,
            servings = listing.servingsPerPackage,
            purchaseCostPerServing = effects.Quote(listing).effectivePrice / listing.servingsPerPackage,
            daysRemaining = balance.GetShelfLife(listing.food.storageType)
        };

    public CartResult Quote(List<CartEntry> cart)
    {
        if (cart == null) return new CartResult { error = "Cart is missing." };
        double total = 0;
        long packages = 0;
        foreach (var item in cart)
        {
            if (item == null || item.packageQuantity <= 0) return new CartResult { error = "Package quantity must be positive." };
            var listing = listings.GetById(item.listingId);
            string error = ValidateListing(listing);
            if (error != null) return new CartResult { error = error };
            packages += item.packageQuantity;
            // Protect against malformed input allocating millions of batch cards.
            if (packages > 10000) return new CartResult { error = "Cart exceeds 10,000 packages." };
            total += (double)effects.Quote(listing).effectivePrice * item.packageQuantity;
        }
        if (total > float.MaxValue || !Finite(state.money))
            return new CartResult { error = "Invalid cart total or funds." };
        return new CartResult { success = true, total = (float)total, packages = (int)packages };
    }

    public CartResult Checkout(List<CartEntry> cart)
    {
        if (state.shoppingReceipt != null && state.shoppingReceipt.day == state.currentDay)
            return new CartResult { error = "Today's checkout is already complete." };
        var quote = Quote(cart);
        if (!quote.success) return quote;
        // Money can be negative after rent; an empty cart is still allowed so the day can continue.
        if (quote.total > Math.Max(0f, state.money)) return new CartResult { error = "Cannot afford the full cart before the sorting refund." };
        if (!Finite(balance.maximumSortingDiscount) || balance.maximumSortingDiscount < 0 || balance.maximumSortingDiscount > 1)
            return new CartResult { error = "Maximum sorting discount must be between 0 and 1." };

        var receipt = new ShoppingReceipt { day = state.currentDay, total = quote.total,
            maximumDiscount = balance.maximumSortingDiscount };
        var batches = new List<InventoryEntry>();
        var mastered = new HashSet<string>(state.correctlySortedFoodIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var questionByFood = new Dictionary<string, SortingQuestion>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in cart)
        {
            var listing = listings.GetById(item.listingId);
            for (int i = 0; i < item.packageQuantity; i++)
            {
                var batch = CreateBatch(listing);
                batches.Add(batch);
                receipt.batchIds.Add(batch.inventoryEntryId);
            }
            if (mastered.Contains(listing.food.id)) continue;
            if (!questionByFood.TryGetValue(listing.food.id, out var question))
            {
                question = new SortingQuestion { foodId = listing.food.id,
                    listingId = listing.id, correctAnswer = listing.food.trafficLight };
                questionByFood.Add(listing.food.id, question);
                receipt.questions.Add(question);
            }
            question.cost += effects.Quote(listing).effectivePrice * item.packageQuantity;
        }
        // All validation and batch preparation precede the single transaction commit.
        receipt.sortingSubmitted = receipt.questions.Count == 0;
        state.inventory.AddRange(batches);
        state.money -= quote.total;
        state.shoppingReceipt = receipt;
        if (quote.packages > 0) state.flags.SetFlag(TutorialFlags.Purchased);
        return quote;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static PurchaseResult Fail(string error) => new PurchaseResult { error = error };
}
