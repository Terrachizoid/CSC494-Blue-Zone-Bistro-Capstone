// File responsibility: Validates an offer/access/budget, then creates a priced inventory batch and deducts money.
// Shelf life is assigned from balance at purchase. Failure must leave the session unchanged.

using System;

public class PurchaseService : IPurchaseService
{
    private readonly GameSessionState state;
    private readonly StoreListingDatabase listings;
    private readonly BalanceConfig balance;

    public PurchaseService(GameSessionState state, StoreListingDatabase listings, BalanceConfig balance)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.listings = listings ?? throw new ArgumentNullException(nameof(listings));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
    }

    public PurchaseResult Buy(string listingId)
    {
        var listing = listings.GetById(listingId);
        if (listing == null || listing.food == null || listing.store == null)
            return Fail("Listing, food, or store was not found.");
        if (state.currentDay < listing.unlockDay || state.currentDay < listing.store.unlockDay)
            return Fail("This listing or store is locked.");
        if (!string.IsNullOrWhiteSpace(listing.requiredFlag) && !state.flags.HasFlag(listing.requiredFlag))
            return Fail("The required unlock flag is missing.");
        if (listing.store.requiresMembership && !state.storeMembershipIds.Exists(
                id => string.Equals(id, listing.store.id, StringComparison.OrdinalIgnoreCase)))
            return Fail("This store requires membership.");
        int days = balance.GetShelfLife(listing.food.storageType);
        if (days < -1 || days == 0 || listing.servingsPerPackage <= 0 ||
            string.IsNullOrWhiteSpace(listing.food.id) || float.IsNaN(listing.gamePrice) ||
            float.IsInfinity(listing.gamePrice) || listing.gamePrice < 0)
            return Fail("Invalid price, servings, food ID, or shelf life.");
        if (float.IsNaN(state.money) || float.IsInfinity(state.money) || state.money < listing.gamePrice)
            return Fail("Insufficient or invalid funds.");

        var entry = new InventoryEntry
        {
            foodId = listing.food.id,
            servings = listing.servingsPerPackage,
            purchaseCostPerServing = listing.gamePrice / listing.servingsPerPackage,
            daysRemaining = days
        };
        state.inventory.Add(entry); // Each purchase is a distinct expiry batch.
        state.money -= listing.gamePrice;
        return new PurchaseResult { success = true, inventoryEntryId = entry.inventoryEntryId,
            foodId = entry.foodId, servingsAdded = entry.servings, daysRemaining = days,
            moneySpent = listing.gamePrice };
    }

    private static PurchaseResult Fail(string error) => new PurchaseResult { error = error };
}
