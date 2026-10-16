// File responsibility: Read-side access for UI: available stores/listings/recipes and content by ID.
// Queries do not purchase or serve; returned assets are shared definitions and must not be edited by UI.

using System.Collections.Generic;
using System.Linq;

public class GameContentQueries
{
    private readonly GameSessionState state;
    private readonly StoreListingDatabase listings;
    private readonly RecipeDatabase recipes;
    private readonly CustomerDatabase customers;
    private readonly RecipeCheck recipeCheck;
    public FoodDatabase Foods { get; }
    public GameContentQueries(GameSessionState state, FoodDatabase foods,
        StoreListingDatabase listings, RecipeDatabase recipes, CustomerDatabase customers)
    {
        this.state = state ?? throw new System.ArgumentNullException(nameof(state));
        Foods = foods ?? throw new System.ArgumentNullException(nameof(foods));
        this.listings = listings ?? throw new System.ArgumentNullException(nameof(listings));
        this.recipes = recipes ?? throw new System.ArgumentNullException(nameof(recipes));
        this.customers = customers ?? throw new System.ArgumentNullException(nameof(customers));
        recipeCheck = new RecipeCheck(foods);
    }
    public List<StoreData> GetStores()
    {
        var result = new List<StoreData>();
        foreach (var listing in listings.Listings)
            if (listing != null && listing.store != null && listing.store.unlockDay <= state.currentDay &&
                !result.Contains(listing.store)) result.Add(listing.store);
        return result;
    }
    public List<StoreListingData> GetListings(StoreData store)
    {
        var result = new List<StoreListingData>();
        foreach (var listing in listings.GetForStore(store))
            if (listing.food != null && listing.unlockDay <= state.currentDay &&
                (string.IsNullOrWhiteSpace(listing.requiredFlag) || state.flags.HasFlag(listing.requiredFlag)))
                result.Add(listing);
        return result;
    }
    public List<RecipeData> GetMakeableRecipes()
    {
        var result = new List<RecipeData>();
        foreach (var recipe in recipes.Recipes)
            if (recipeCheck.CanMake(recipe, state)) result.Add(recipe);
        return result;
    }
    public bool CanMake(RecipeData recipe) => recipeCheck.CanMake(recipe, state);
    public List<RecipeData> GetUnlockedRecipes()
    {
        var result = new List<RecipeData>();
        foreach (var recipe in recipes.Recipes)
            if (recipe != null && recipe.unlockDay <= state.currentDay) result.Add(recipe);
        return result;
    }
    public CustomerData GetCustomer(string id) => customers.GetById(id);
    public StoreListingData GetListing(string id) => listings.GetById(id);
    public bool TryGetMinimumServingPrice(string foodId, out float price) => Foods.TryGetMinimumServingPrice(foodId, out price);
    public List<CustomerData> GetExpectedCustomers()=>state.dailyCustomerIds.Select(customers.GetById).Where(c=>c!=null).ToList();
    public CravingTag[] GetActiveCravings(string customerId)
    {
        var progress=state.customers.Find(c=>string.Equals(c.customerId,customerId,System.StringComparison.OrdinalIgnoreCase));
        return progress!=null&&progress.cravingDay==state.currentDay ? (progress.activeCravings??System.Array.Empty<CravingTag>()).ToArray() : System.Array.Empty<CravingTag>();
    }
    // Normalized presentation input only. Apply to world art later, never UI text/cards.
    public float GetCommunityVisualScore(BalanceConfig balance)=>CommunityProgress.Clamp(state.communityWellbeing/balance.maximumCommunityWellbeing,1);
}
