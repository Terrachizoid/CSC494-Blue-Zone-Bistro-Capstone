// Builds one morning plan: popularity caps attendance, delight weights unique visitors.
// Plan IDs and active cravings are session data, so querying or resuming cannot reroll them.
using System;
using System.Collections.Generic;
using System.Linq;
public interface ICustomerVisitService { List<string> GetTodaysCustomers(); }
public class CustomerVisitService : ICustomerVisitService
{
    private readonly GameSessionState state;
    private readonly CustomerDatabase customers;
    private readonly BalanceConfig balance;
    private readonly Func<double> random;
    private readonly RecipeDatabase recipes;
    private readonly FoodDatabase foods;
    private readonly StoreListingDatabase listings;
    public CustomerVisitService(GameSessionState state,CustomerDatabase customers,BalanceConfig balance,
        Func<double> random=null,RecipeDatabase recipes=null,FoodDatabase foods=null,StoreListingDatabase listings=null)
    {
        this.state=state??throw new ArgumentNullException(nameof(state));
        this.customers=customers??throw new ArgumentNullException(nameof(customers));
        this.balance=balance??throw new ArgumentNullException(nameof(balance));
        var generator=new Random();this.random=random??generator.NextDouble;
        this.recipes=recipes;this.foods=foods;this.listings=listings;
        CommunityProgress.Initialize(state,balance);
    }
    private double Roll(){double v=random();if(double.IsNaN(v)||double.IsInfinity(v))throw new InvalidOperationException("Invalid attendance random value.");return Math.Max(0,Math.Min(.999999999,v));}
    public List<string> GetTodaysCustomers()
    {
        if(state.attendancePlanDay!=state.currentDay)Prepare();
        return state.dailyCustomerIds.Where(id=>customers.GetById(id)!=null&&state.customers.Any(c=>Same(c.customerId,id)&&c.lastServedDay!=state.currentDay)).ToList();
    }
    private void Prepare()
    {
        if(!CommunityProgress.Valid(balance))throw new InvalidOperationException("Invalid attendance tuning.");
        state.morningPopularity=CommunityProgress.Clamp((double)state.popularity+new MarketEffects(state).Publicity,balance.maximumPopularity);
        state.dailyCustomerCapacity=CommunityProgress.Capacity(state.morningPopularity,balance);
        var eligible=new List<CustomerData>();
        foreach(var data in customers.Customers.Where(c=>c!=null && c.customerType!=CustomerType.VisitingChef && !string.IsNullOrWhiteSpace(c.id) && c.unlockDay<=state.currentDay).OrderBy(c=>c.id,StringComparer.OrdinalIgnoreCase))
        {
            if(eligible.Any(c=>Same(c.id,data.id)))continue;
            bool known=state.unlockedCustomerIds.Any(id=>Same(id,data.id));
            // Popularity requirement is authored per customer (Customers.csv unlock_popularity).
            if(!known && data.unlockPopularity>0 && state.popularity<data.unlockPopularity)continue;
            if(!known)state.unlockedCustomerIds.Add(data.id);
            var progress=state.customers.Find(c=>Same(c.customerId,data.id));
            if(progress==null){progress=CustomerState.Create(data,balance);state.customers.Add(progress);}
            if(progress.lastServedDay!=state.currentDay)eligible.Add(data);
        }
        // Story chefs do not occupy these seats. Zero popularity still has one seat.
        foreach(var p in state.customers){p.attendanceDay=state.currentDay;p.attending=false;p.cravingDay=state.currentDay;p.activeCravings=Array.Empty<CravingTag>();}
        var chosen=new List<string>();
        var availableRecipes=AvailableRecipes();
        while(chosen.Count<state.dailyCustomerCapacity && eligible.Count>0)
        {
            var weights=eligible.Select(c=>(double)Math.Max(0,Math.Min(balance.maxDelight,state.customers.Find(p=>Same(p.customerId,c.id)).delight))).ToArray();
            double total=weights.Sum();
            if(total<=0 && chosen.Count>0)break;
            double ticket=Roll()*(total>0?total:eligible.Count);
            int index=eligible.Count-1;
            for(int i=0;i<eligible.Count;i++){ticket-=total>0?weights[i]:1;if(ticket<0){index=i;break;}}
            var data=eligible[index];eligible.RemoveAt(index);chosen.Add(data.id);
            var p=state.customers.Find(c=>Same(c.customerId,data.id));p.attending=true;
            if(state.currentDay>=balance.cravingsStartDay)p.activeCravings=ChooseCravings(data,availableRecipes);
        }
        state.dailyCustomerIds=chosen;state.attendancePlanDay=state.currentDay;
    }
    private List<RecipeData> AvailableRecipes()
    {
        if(recipes==null)return null;
        var categories=new HashSet<FoodCategory>();
        var greenCategories=new HashSet<FoodCategory>();
        if(foods!=null)foreach(var entry in state.inventory.Where(MealAllocation.Usable))
        {var food=foods.GetById(entry.foodId);if(food!=null){categories.Add(food.recipeCategory);if(food.trafficLight==TrafficLight.Green)greenCategories.Add(food.recipeCategory);}}
        if(listings!=null)foreach(var l in listings.Listings)
            if(l!=null&&l.food!=null&&l.store!=null&&new MarketEffects(state).ClosureReason(l.store.id)==null&&l.unlockDay<=state.currentDay&&l.store.unlockDay<=state.currentDay&&
                (!l.store.requiresMembership||state.storeMembershipIds.Contains(l.store.id))&&
                (string.IsNullOrEmpty(l.requiredFlag)||state.flags.HasFlag(l.requiredFlag))&&CommunityProgress.Finite(l.gamePrice)&&l.gamePrice>0&&l.servingsPerPackage>0)
                {categories.Add(l.food.recipeCategory);if(l.food.trafficLight==TrafficLight.Green)greenCategories.Add(l.food.recipeCategory);}
        return recipes.Recipes.Where(r=>r!=null&&r.unlockDay<=state.currentDay&&RecipeRequirements.TryGetBounds(r,out _,out _) &&
            (foods==null||listings==null||r.slots.Where(s=>!s.optional).All(s=>(r.allowNonGreenChoices?categories:greenCategories).Contains(s.category)))).ToList();
    }
    private CravingTag[] ChooseCravings(CustomerData customer,List<RecipeData> available)
    {
        var pool=(customer.cravingTags??Array.Empty<CravingTag>()).Where(t=>t!=CravingTag.None&&Enum.IsDefined(typeof(CravingTag),t)).Distinct().OrderBy(t=>t).ToList();
        var chosen=new List<CravingTag>();
        int wanted=balance.minimumDailyCravings+(int)(Roll()*(balance.maximumDailyCravings-balance.minimumDailyCravings+1));
        while(chosen.Count<wanted)
        {
            var options=pool.Where(t=>available==null||available.Any(r=>chosen.Concat(new[]{t}).All(tag=>(r.cravingTags??Array.Empty<CravingTag>()).Contains(tag)))).ToList();
            if(options.Count==0)break;
            var tag=options[(int)(Roll()*options.Count)];chosen.Add(tag);pool.Remove(tag);
        }
        return chosen.ToArray();
    }
    private static bool Same(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
}
