// Regression coverage for separate global scores, bounded ingredient appeal and saved morning plans.
using System;
using System.Collections.Generic;
using System.Linq;
public static class CommunityChecks
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
    public static string Run()
    {
        var b=new BalanceConfig();
        Check(CommunityProgress.Capacity(0,b)==1&&CommunityProgress.Capacity(249,b)==1&&CommunityProgress.Capacity(250,b)==2&&CommunityProgress.Capacity(500,b)==3&&CommunityProgress.Capacity(750,b)==4&&CommunityProgress.Capacity(1000,b)==4,"Popularity thresholds wrong");
        var old=new GameSessionState{popularity=0,communityWellbeing=0};CommunityProgress.Initialize(old,b);
        Check(old.popularity==300&&old.communityWellbeing==50,"Legacy state did not initialize from config");
        old.popularity=0;old.communityWellbeing=0;CommunityProgress.Initialize(old,b);Check(old.popularity==0&&old.communityWellbeing==0,"Legitimate zero reset");
        var s=new GameSessionState{currentDay=4};
        var grain=new FoodData{id="G",recipeCategory=FoodCategory.Grain,trafficLight=TrafficLight.Green,ingredientAppeal=4};
        var fruit=new FoodData{id="F",recipeCategory=FoodCategory.Fruit,trafficLight=TrafficLight.Green,ingredientAppeal=8};
        var foods=new FoodDatabase();foods.SetItems(new List<FoodData>{grain,fruit});
        var store=new StoreData{id="S"};var listings=new StoreListingDatabase();
        listings.SetItems(new List<StoreListingData>{new StoreListingData{food=grain,store=store,servingsPerPackage=4,gamePrice=4},new StoreListingData{food=fruit,store=store,servingsPerPackage=4,gamePrice=4}});foods.RebuildMinimumPrices(listings.Listings);
        var recipe=new RecipeData{id="R",baseSalePrice=8,delightValue=2,cravingTags=new[]{CravingTag.Sweet},slots=new[]{new RecipeSlot{category=FoodCategory.Grain,servings=1},new RecipeSlot{category=FoodCategory.Fruit,servings=3}}};
        var recipes=new RecipeDatabase();recipes.SetItems(new List<RecipeData>{recipe});
        var customer=new CustomerData{id="U",cravingTags=new[]{CravingTag.Sweet,CravingTag.Hearty}};
        var customers=new CustomerDatabase();customers.SetItems(new List<CustomerData>{customer});
        var p=CustomerState.Create(customer,b);p.health=100;p.delight=100;p.cravingDay=4;p.activeCravings=new[]{CravingTag.Sweet,CravingTag.Hearty};s.customers.Add(p);
        s.inventory.Add(new InventoryEntry{inventoryEntryId="G",foodId="G",servings=100,purchaseCostPerServing=1});
        s.inventory.Add(new InventoryEntry{inventoryEntryId="F",foodId="F",servings=100,purchaseCostPerServing=1});
        var input=new ServeInput{customerId="U",recipeId="R",ingredients=new List<IngredientSelection>{new IngredientSelection{inventoryEntryId="G",servingsUsed=1},new IngredientSelection{inventoryEntryId="F",servingsUsed=3}}};
        var meals=new MealService(s,foods,recipes,customers,b);var r=meals.Preview(input);
        Check(r.success&&Near(r.ingredientAppeal,7)&&r.dishDelight==13&&r.delightDelta==0&&r.healthDelta==0&&r.dishHealthEffect==4&&Near(r.popularityDelta,32.5f)&&Near(r.communityWellbeingDelta,1),"Weighted appeal, partial craving or unclamped global effects wrong");
        Check(s.popularity==300&&s.communityWellbeing==50&&s.inventory[0].servings==100,"Preview mutated state");
        listings.Listings[0].gamePrice=400;foods.RebuildMinimumPrices(listings.Listings);
        Check(meals.Preview(input).dishDelight==13,"Price still contributes delight");
        recipe.cravingTags=new[]{CravingTag.Crunchy};r=meals.Preview(input);
        Check(r.dishDelight==9&&r.popularityDelta==0&&r.communityWellbeingDelta==1,"Missed craving changed popularity or health");
        recipe.slots[0].servings=2;r=meals.Preview(input);
        Check(!r.recipeBuilt&&r.cravingsResolved.Length==0&&r.popularityDelta==-40&&r.communityWellbeingDelta==1,"Invalid recipe effects wrong");
        recipe.slots[0].servings=1;recipe.cravingTags=new[]{CravingTag.Sweet,CravingTag.Hearty};
        r=meals.Preview(input);Check(r.dishDelight==17&&r.popularityDelta==85,"Full craving reward wrong");
        grain.ingredientAppeal=float.NaN;Check(!meals.Serve(input).success&&s.popularity==300&&s.inventory[0].servings==100,"Invalid appeal mutated state");grain.ingredientAppeal=4;
        var committed=meals.Serve(input);Check(committed.success&&s.popularity==385&&s.communityWellbeing==51&&!meals.Serve(input).success,"Commit or duplicate protection failed");
        // Same ingredient repeated fifty times still has appeal four.
        p.lastServedDay=0;input.ingredients.RemoveAt(1);recipe.slots=new[]{new RecipeSlot{category=FoodCategory.Grain,servings=1}};
        float single=meals.Preview(input).ingredientAppeal;input.ingredients[0].servingsUsed=50;recipe.slots[0].servings=50;
        Check(meals.Preview(input).ingredientAppeal==single,"Quantity farmed ingredient appeal");
        s.popularity=999;s.communityWellbeing=99.5f;r=meals.Preview(input);Check(r.popularityDelta==1&&Near(r.communityWellbeingDelta,.5f),"Global bounds ignored");
        s.currentDay=3;p.activeCravings=Array.Empty<CravingTag>();r=meals.Preview(input);Check(r.popularityDelta==1,"Introductory days cannot grow popularity");
        s.currentDay=4;r=meals.Preview(input);Check(r.popularityDelta==0,"Missing/stale cravings rewarded popularity after introduction");

        var queueState=new GameSessionState{currentDay=4};int rolls=0;
        var roster=new CustomerDatabase();roster.SetItems(Enumerable.Range(1,6).Select(i=>new CustomerData{id="U"+i,cravingTags=new[]{CravingTag.Sweet,CravingTag.Hearty,CravingTag.Fizzy}}).ToList());
        b.customerPopularityUnlocks=new[]{new CustomerPopularityUnlock{customerId="U6",popularity=750}};
        var visits=new CustomerVisitService(queueState,roster,b,()=>{rolls++;return .2;},recipes,foods,listings);
        var queue=visits.GetTodaysCustomers();int originalRolls=rolls;
        Check(queue.Count==2&&!queue.Contains("U6")&&queue.Distinct().Count()==2,"Morning capacity/unlocks wrong");
        queueState.popularity=900;
        Check(visits.GetTodaysCustomers().SequenceEqual(queue)&&rolls==originalRolls,"Midday popularity rerolled guests");
        var resumed=new CustomerVisitService(queueState,roster,b,()=>throw new Exception("Resume rerolled"),recipes,foods,listings);
        Check(resumed.GetTodaysCustomers().SequenceEqual(queue),"Recreated service lost attendance");
        foreach(var progress in queueState.customers.Where(c=>c.attending))Check(progress.activeCravings.Length>0&&!progress.activeCravings.Contains(CravingTag.Fizzy),"Unsupported craving selected");
        queueState.currentDay++;visits.GetTodaysCustomers();Check(queueState.dailyCustomerCapacity==4&&queueState.unlockedCustomerIds.Contains("U6"),"Next-day popularity not applied");
        queueState.currentDay++;queueState.popularity=0;foreach(var c in queueState.customers)c.delight=0;
        Check(visits.GetTodaysCustomers().Count==1&&queueState.unlockedCustomerIds.Contains("U6"),"Recovery customer or permanent discovery lost");
        return "PASS: weighted appeal, craving fractions, independent global scores, preview/commit, caps, price independence, migration, morning capacity, unlocks, saved plans and recovery attendance.";
    }
}
