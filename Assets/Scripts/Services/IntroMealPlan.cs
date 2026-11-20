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
    public float ComfortMinimumAppeal=>balance.comfortMinimumAppeal;
    public float PopularityPerMissedCraving=>balance.popularityPerMissedCraving;
    public float MaximumDiscount=>balance.maximumSortingDiscount;
    public float RentPerDay=>balance.rentPerDay;
    public int RentStartDay=>balance.rentStartDay;
    public float PantrySellRate=>balance.pantrySellRate;
    // Values quoted by the top-bar lessons (Day, Popularity, Wallet); all read from BalanceConfig.
    public int TotalDays=>balance.totalDays;
    public int CravingsStartDay=>balance.cravingsStartDay;
    public float WalkInThreshold=>balance.walkInPopularityThreshold;
    public float WalkInPerHundred=>balance.walkInIncomePerPopularity*100;
    public int MaxGuests=>balance.attendanceThresholds==null?1:Math.Max(1,balance.attendanceThresholds.Length);
    public string GuestThresholds=>JoinAnd((balance.attendanceThresholds??Array.Empty<float>()).Where(t=>t>0).Select(t=>t.ToString("0")).ToList());
    public IntroMealPlan(GameSessionState state,FoodDatabase foods,RecipeDatabase recipes,StoreListingDatabase listings,BalanceConfig balance)
    {State=state;Foods=foods;this.recipes=recipes;this.listings=listings;this.balance=balance;purchase=new PurchaseService(state,listings,balance);}

    // Recipes the first lesson accepts: unlocked today with valid slots.
    public IEnumerable<RecipeData> Candidates=>(recipes?.Recipes??Array.Empty<RecipeData>())
        .Where(r=>r!=null&&r.unlockDay<=State.currentDay&&RecipeRequirements.TryGetBounds(r,out _,out _));
    public bool IsCandidate(string recipeId)=>Candidates.Any(r=>string.Equals(r.id,recipeId,StringComparison.OrdinalIgnoreCase));

    // Example shown in instructions: the cheapest recipe the player can currently complete.
    public RecipeData Recipe=>Candidates.Select(r=>new{r,cost=CostToComplete(r)}).OrderBy(x=>x.cost).ThenBy(x=>x.r.id,StringComparer.Ordinal).FirstOrDefault()?.r;

    public bool PantryReady()=>Candidates.Any(r=>new RecipeCheck(Foods).CanMake(r,State));
    // The pantry holds everything this menu dish needs right now (the first-meal lesson only accepts these).
    public bool CanMakeNow(string recipeId)=>Candidates.Any(r=>string.Equals(r.id,recipeId,StringComparison.OrdinalIgnoreCase)&&new RecipeCheck(Foods).CanMake(r,State));
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
    // The day-1 lesson only lets the player plan a dish they can complete with today's money.
    public bool Affordable(string recipeId)=>Candidates.Any(r=>string.Equals(r.id,recipeId,StringComparison.OrdinalIgnoreCase)&&CostToComplete(r)<=State.money);

    // Cheapest package cost to cover a recipe's required slots from pantry plus open listings.
    private double CostToComplete(RecipeData recipe)
    {
        if(!RecipeRequirements.TryGetBounds(recipe,out var needed,out _))return double.PositiveInfinity;
        double cost=0;
        var owned=new Dictionary<FoodData,long>();
        foreach(var e in State.inventory.Where(MealAllocation.Usable))
        {
            var food=Foods.GetById(e.foodId);
            if(food==null||(!recipe.allowNonGreenChoices&&food.trafficLight!=TrafficLight.Green))continue;
            long n;owned.TryGetValue(food,out n);owned[food]=n+e.servings;
        }
        var assigned=RecipeRequirements.Assign(owned,needed,null,ignoreSurplus:true);
        foreach(var pair in needed)
        {
            long have;assigned.TryGetValue(pair.Key,out have);
            long missing=Math.Max(0,pair.Value-have);if(missing==0)continue;
            // Small integer package-cost DP considers mixed offers, not just cheapest price per serving.
            if(missing>10000)return double.PositiveInfinity;
            var best=Enumerable.Repeat(double.PositiveInfinity,(int)missing+1).ToArray();best[0]=0;
            var effects=new MarketEffects(State);
            var offers=listings.Listings.Where(l=>l!=null&&l.food!=null&&l.food.Fits(pair.Key)&&
                (recipe.allowNonGreenChoices||l.food.trafficLight==TrafficLight.Green)&&purchase.Quote(new List<CartEntry>{new CartEntry{listingId=l.id}}).success).ToList();
            for(int n=0;n<missing;n++)foreach(var offer in offers)
            {int next=(int)Math.Min(missing,(long)n+offer.servingsPerPackage);best[next]=Math.Min(best[next],best[n]+effects.Quote(offer).effectivePrice);}
            cost+=best[(int)missing];
        }
        return cost;
    }
    public string Requirements{get{var r=Recipe;return r==null?"Unavailable recipe":RequirementsOf(r);}}
    // "2 Grain and 1 Vegetable" from the recipe's required slots.
    public static string RequirementsOf(RecipeData r)=>JoinAnd(r.slots.Where(s=>!s.optional).Select(s=>$"{s.servings} {CategoryName(s.category)}").ToList());
    // "a, b and c" so lists read as a sentence.
    public static string JoinAnd(IList<string> items)=>items.Count==0?"":items.Count==1?items[0]:string.Join(", ",items.Take(items.Count-1))+" and "+items[items.Count-1];
    // "StarchyVegetable" -> "Starchy vegetable", so players can tell it apart from "Vegetable".
    public static string CategoryName(FoodCategory c)=>Readable(c);
    // Player-facing name for any enum value: "OtherVegetables" -> "Other vegetables", "None" -> "none".
    public static string ReadableList<T>(IEnumerable<T> values) where T:struct
    {
        var names=(values??Enumerable.Empty<T>()).Where(v=>v.ToString()!="None").Select(v=>Readable((Enum)(object)v)).ToList();
        return names.Count==0?"none":string.Join(", ",names);
    }
    public static string Readable(Enum value)
    {
        var raw=value.ToString();var text=new System.Text.StringBuilder();
        for(int i=0;i<raw.Length;i++){if(i>0&&char.IsUpper(raw[i])){text.Append(' ');text.Append(char.ToLowerInvariant(raw[i]));}else text.Append(raw[i]);}
        return text.ToString();
    }
    // Every recipe unlocked today, read from content, as one sentence fragment:
    // "Sandwich needs 2 Grain and 1 Vegetable; Bean and grain bowl needs ...; or Oat and fruit breakfast needs ...". Any of them completes the lesson.
    public string Examples
    {
        get
        {
            var list=Candidates.OrderBy(r=>r.id,StringComparer.Ordinal).Select(r=>$"{r.displayName} needs {RequirementsOf(r)}").ToList();
            if(list.Count==0)return "no recipe is unlocked yet";
            return list.Count==1?list[0]:string.Join("; ",list.Take(list.Count-1))+"; or "+list[list.Count-1];
        }
    }
}
