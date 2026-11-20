// Regression checks for the Comfort request (plate appeal) and global recipe fatigue.
using System;
using System.Collections.Generic;
using System.Linq;
public static class ComfortFatigueChecks
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    public static string Run()
    {
        var b=new BalanceConfig{comfortMinimumAppeal=7,recipeFatigueWindowDays=2,recipeFatigueThreshold=2,recipeFatigueMultiplier=.5f};
        var s=new GameSessionState{currentDay=4};
        var plain=new FoodData{id="OATS",recipeCategory=FoodCategory.Grain,trafficLight=TrafficLight.Green,ingredientAppeal=3,dailyDozenCategories=new[]{DailyDozenCategory.WholeGrains}};
        var chips=new FoodData{id="CHIPS",recipeCategory=FoodCategory.Grain,trafficLight=TrafficLight.Red,ingredientAppeal=9};
        var foods=new FoodDatabase();foods.SetItems(new List<FoodData>{plain,chips});
        var store=new StoreData{id="S"};var listings=new StoreListingDatabase();
        listings.SetItems(new List<StoreListingData>{new StoreListingData{food=plain,store=store,servingsPerPackage=4,gamePrice=1},new StoreListingData{food=chips,store=store,servingsPerPackage=4,gamePrice=1}});
        foods.RebuildMinimumPrices(listings.Listings);
        var recipe=new RecipeData{id="R",baseSalePrice=4,delightValue=2,allowNonGreenChoices=true,cravingTags=new[]{CravingTag.Savory},slots=new[]{new RecipeSlot{category=FoodCategory.Grain,servings=1}}};
        var recipes=new RecipeDatabase();recipes.SetItems(new List<RecipeData>{recipe});
        var roster=new List<CustomerData>();for(int i=1;i<=4;i++)roster.Add(new CustomerData{id="U"+i,startingHealth=50,cravingTags=new[]{CravingTag.Comfort}});
        var customers=new CustomerDatabase();customers.SetItems(roster);
        foreach(var c in roster){var cs=CustomerState.Create(c,b);cs.cravingDay=4;cs.activeCravings=new[]{CravingTag.Comfort};s.customers.Add(cs);}
        s.inventory.Add(new InventoryEntry{inventoryEntryId="O",foodId="OATS",servings=50,purchaseCostPerServing=.25f});
        s.inventory.Add(new InventoryEntry{inventoryEntryId="C",foodId="CHIPS",servings=50,purchaseCostPerServing=.25f});
        var meals=new MealService(s,foods,recipes,customers,b);
        ServeInput Plate(string guest,string batch)=>new ServeInput{customerId=guest,recipeId="R",ingredients=new List<IngredientSelection>{new IngredientSelection{inventoryEntryId=batch,servingsUsed=1}}};

        var plainPlate=meals.Preview(Plate("U1","O"));
        Check(plainPlate.cravingsResolved.Length==0,"Plain plate (appeal 3) must not satisfy Comfort");
        var tasty=meals.Preview(Plate("U1","C"));
        Check(tasty.cravingsResolved.SequenceEqual(new[]{CravingTag.Comfort}),"Appeal 9 plate must satisfy Comfort without the recipe tag");
        Check(!tasty.recipeFatigued&&tasty.recipeRecentServes==0,"Fresh recipe must not be fatigued");

        var first=meals.Serve(Plate("U1","O"));
        Check(first.success&&s.recipeServes.Count==1&&s.recipeServes[0].green==1,"Serve history not recorded");
        Check(s.customers[0].dailyDozenEaten.Contains(DailyDozenCategory.WholeGrains),"Guest Daily Dozen not recorded");
        meals.Serve(Plate("U2","C"));
        var unfatigued=meals.Preview(Plate("U3","C"));
        Check(unfatigued.recipeFatigued&&unfatigued.recipeRecentServes==2,"Third serving within the window must be fatigued");
        b.recipeFatigueThreshold=0;var full=meals.Preview(Plate("U3","C"));b.recipeFatigueThreshold=2;
        Check(full.popularityDelta>0&&Math.Abs(unfatigued.popularityDelta-full.popularityDelta*.5f)<.001f,"Fatigue must halve positive popularity");
        s.currentDay=6;
        Check(!meals.Preview(Plate("U3","C")).recipeFatigued,"Servings outside the window must not count");
        // Daily Dozen novelty: a group this guest already ate adds no extra health when the option is on.
        b.recipeFatigueThreshold=0;s.currentDay=4;
        var u1=s.customers.Find(c=>c.customerId=="U1");u1.lastServedDay=0;
        b.healthPerGreenDailyDozenCategory=2;
        b.dailyDozenNewGroupsOnly=false;float repeatOff=meals.Preview(Plate("U1","O")).dishHealthEffect;
        b.dailyDozenNewGroupsOnly=true;float repeatOn=meals.Preview(Plate("U1","O")).dishHealthEffect;
        float freshGuest=meals.Preview(Plate("U4","O")).dishHealthEffect;
        // dishHealthEffect became a float (2026-10-28); compare with a tolerance. This line kept the whole suite from compiling.
        Check(Math.Abs(repeatOff-repeatOn-2)<.001f&&Math.Abs(freshGuest-repeatOff)<.001f,"Already-eaten group must not add Daily Dozen health; new guests still get it");
        // Crowd popularity: plate appeal above the pivot adds popularity; plain plates get none.
        b.popularityPerAppealPoint=0;float noCrowd=meals.Preview(Plate("U4","C")).popularityDelta;float plainNo=meals.Preview(Plate("U4","O")).popularityDelta;
        b.popularityPerAppealPoint=10;b.popularityAppealPivot=7;float crowd=meals.Preview(Plate("U4","C")).popularityDelta;float plainYes=meals.Preview(Plate("U4","O")).popularityDelta;
        Check(Math.Abs(crowd-noCrowd-20f)<.001f&&Math.Abs(plainYes-plainNo)<.001f,"Appeal 9 plate must gain (9-7)×10 popularity; appeal 3 plate none");
        return "PASS: Comfort request, recipe fatigue, serve history, guest Daily Dozen novelty and crowd popularity.";
    }
}
