// Shared introductory recipe contract. Both shopping and cooking refer to this one definition.
// No tutorial reproduces recipe composition or meal scoring formulas.
using System;
using System.Collections.Generic;
using System.Linq;
public class IntroMealPlan
{
    public const string RecipeId="R001";
    public readonly GameSessionState State;
    public readonly FoodDatabase Foods;
    public readonly RecipeData Recipe;
    private readonly StoreListingDatabase listings;
    private readonly PurchaseService purchase;
    private readonly BalanceConfig balance;
    public float MaximumDiscount=>balance.maximumSortingDiscount;
    public IntroMealPlan(GameSessionState state,FoodDatabase foods,RecipeDatabase recipes,StoreListingDatabase listings,BalanceConfig balance)
    {State=state;Foods=foods;Recipe=recipes.GetById(RecipeId);this.listings=listings;this.balance=balance;purchase=new PurchaseService(state,listings,balance);}
    public bool PantryReady()=>new RecipeCheck(Foods).CanMake(Recipe,State);
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
        return new RecipeCheck(Foods).CanMake(Recipe,projected);
    }
    public bool ShoppingFeasible()
    {
        if(Recipe==null || Recipe.unlockDay>State.currentDay || !RecipeRequirements.TryGetBounds(Recipe,out var needed,out _))return false;
        double cost=0;
        foreach(var pair in needed)
        {
            long have=State.inventory.Where(MealAllocation.Usable).Where(e=>Foods.GetById(e.foodId)?.recipeCategory==pair.Key &&
                (Recipe.allowNonGreenChoices||Foods.GetById(e.foodId).trafficLight==TrafficLight.Green)).Sum(e=>(long)e.servings);
            long missing=Math.Max(0,pair.Value-have);if(missing==0)continue;
            // Small integer package-cost DP considers mixed offers, not just cheapest price per serving.
            if(missing>10000)return false;
            var best=Enumerable.Repeat(double.PositiveInfinity,(int)missing+1).ToArray();best[0]=0;
            var offers=listings.Listings.Where(l=>l!=null&&l.food!=null&&l.food.recipeCategory==pair.Key&&
                (Recipe.allowNonGreenChoices||l.food.trafficLight==TrafficLight.Green)&&purchase.Quote(new List<CartEntry>{new CartEntry{listingId=l.id}}).success).ToList();
            for(int n=0;n<missing;n++)foreach(var offer in offers)
            {int next=(int)Math.Min(missing,(long)n+offer.servingsPerPackage);best[next]=Math.Min(best[next],best[n]+offer.gamePrice);}
            cost+=best[(int)missing];
        }
        return !double.IsInfinity(cost) && cost<=State.money;
    }
    public string Requirements=>Recipe==null?"Unavailable recipe":string.Join(", ",Recipe.slots.Where(s=>!s.optional).Select(s=>$"{s.servings} {s.category}"));
}
