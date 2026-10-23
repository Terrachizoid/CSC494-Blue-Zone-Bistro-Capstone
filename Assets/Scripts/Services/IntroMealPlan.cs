// Introductory meal contract shared by the shopping and cooking lessons.
// Any recipe unlocked today counts. The cheapest affordable one is only an example for instruction text.
// No tutorial reproduces recipe composition or meal scoring formulas.
using System;
using System.Collections.Generic;
using System.Linq;
public class IntroMealPlan
{
    public readonly GameSessionState State;
    public readonly FoodDatabase Foods;
    private readonly RecipeDatabase recipes;
    private readonly StoreListingDatabase listings;
    private readonly PurchaseService purchase;
    private readonly BalanceConfig balance;
    public float MaximumDiscount=>balance.maximumSortingDiscount;
    public float RentPerDay=>balance.rentPerDay;
    public IntroMealPlan(GameSessionState state,FoodDatabase foods,RecipeDatabase recipes,StoreListingDatabase listings,BalanceConfig balance)
    {State=state;Foods=foods;this.recipes=recipes;this.listings=listings;this.balance=balance;purchase=new PurchaseService(state,listings,balance);}

    // Recipes the first lesson accepts: unlocked today with valid slots.
    public IEnumerable<RecipeData> Candidates=>(recipes?.Recipes??Array.Empty<RecipeData>())
        .Where(r=>r!=null&&r.unlockDay<=State.currentDay&&RecipeRequirements.TryGetBounds(r,out _,out _));
    public bool IsCandidate(string recipeId)=>Candidates.Any(r=>string.Equals(r.id,recipeId,StringComparison.OrdinalIgnoreCase));

    // Example shown in instructions: the cheapest recipe the player can currently complete.
    public RecipeData Recipe=>Candidates.Select(r=>new{r,cost=CostToComplete(r)}).OrderBy(x=>x.cost).ThenBy(x=>x.r.id,StringComparer.Ordinal).FirstOrDefault()?.r;

    public bool PantryReady()=>Candidates.Any(r=>new RecipeCheck(Foods).CanMake(r,State));
    public bool CartReady(List<CartEntry> cart)
    {
        if(cart==null || !purchase.Quote(cart).success)return false;
        var projected=new GameSessionState{currentDay=State.currentDay};
        projected.inventory.AddRange(State.inventory);
        foreach(var item in cart)
        {
            var listing=listings.GetById(item.listingId);
            long amount=(long)listing.servingsPerPackage*item.packageQuantity;
            if(amount>int.MaxValue)return false;
            projected.inventory.Add(new InventoryEntry{foodId=listing.food.id,servings=(int)amount,daysRemaining=balance.GetShelfLife(listing.food.storageType)});
        }
        return Candidates.Any(r=>new RecipeCheck(Foods).CanMake(r,projected));
    }
    public bool ShoppingFeasible()=>Candidates.Any(r=>CostToComplete(r)<=State.money);

    // Cheapest package cost to cover a recipe's required slots from pantry plus open listings.
    private double CostToComplete(RecipeData recipe)
    {
        if(!RecipeRequirements.TryGetBounds(recipe,out var needed,out _))return double.PositiveInfinity;
        double cost=0;
        foreach(var pair in needed)
        {
            long have=State.inventory.Where(MealAllocation.Usable).Where(e=>Foods.GetById(e.foodId)?.recipeCategory==pair.Key &&
                (recipe.allowNonGreenChoices||Foods.GetById(e.foodId).trafficLight==TrafficLight.Green)).Sum(e=>(long)e.servings);
            long missing=Math.Max(0,pair.Value-have);if(missing==0)continue;
            // Small integer package-cost DP considers mixed offers, not just cheapest price per serving.
            if(missing>10000)return double.PositiveInfinity;
            var best=Enumerable.Repeat(double.PositiveInfinity,(int)missing+1).ToArray();best[0]=0;
            var effects=new MarketEffects(State);
            var offers=listings.Listings.Where(l=>l!=null&&l.food!=null&&l.food.recipeCategory==pair.Key&&
                (recipe.allowNonGreenChoices||l.food.trafficLight==TrafficLight.Green)&&purchase.Quote(new List<CartEntry>{new CartEntry{listingId=l.id}}).success).ToList();
            for(int n=0;n<missing;n++)foreach(var offer in offers)
            {int next=(int)Math.Min(missing,(long)n+offer.servingsPerPackage);best[next]=Math.Min(best[next],best[n]+effects.Quote(offer).effectivePrice);}
            cost+=best[(int)missing];
        }
        return cost;
    }
    public string Requirements{get{var r=Recipe;return r==null?"Unavailable recipe":string.Join(", ",r.slots.Where(s=>!s.optional).Select(s=>$"{s.servings} {s.category}"));}}
}
