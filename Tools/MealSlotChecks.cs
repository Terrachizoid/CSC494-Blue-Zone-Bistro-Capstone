// Regression coverage for food aggregation allocation and explicit recipe slot semantics.
using System;
using System.Collections.Generic;
public static class MealSlotChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static string Run()
    {
        var state=new GameSessionState();
        var late=new InventoryEntry{foodId="A",servings=3,daysRemaining=3,purchaseCostPerServing=1};
        var early=new InventoryEntry{foodId="A",servings=8,daysRemaining=1,purchaseCostPerServing=1};
        var infinite=new InventoryEntry{foodId="A",servings=2,daysRemaining=-1,purchaseCostPerServing=1};
        state.inventory.AddRange(new[]{late,infinite,early,new InventoryEntry{foodId="A",servings=99,daysRemaining=0}});
        var recipe=new RecipeData{id="R",baseSalePrice=10,allowNonGreenChoices=true,slots=new[]{new RecipeSlot{category=FoodCategory.Fruit,servings=9},new RecipeSlot{category=FoodCategory.Beverage,servings=2,optional=true}}};
        var choices=new List<MealSlotSelection>{new MealSlotSelection{foodId="a",slotIndex=0,servings=9}};
        Check(MealAllocation.TryResolve(state,recipe,choices,out var batches,out _) && batches.Count==2 && batches[0].inventoryEntryId==early.inventoryEntryId && batches[0].servingsUsed==8 && batches[1].inventoryEntryId==late.inventoryEntryId,"Expiry allocation failed");
        Check(early.servings==8 && late.servings==3,"Allocation mutated pantry");
        choices[0].servings=14;
        Check(!MealAllocation.TryResolve(state,recipe,choices,out _,out _),"Expired stock was available");choices[0].servings=9;
        var food=new FoodData{id="A",recipeCategory=FoodCategory.Fruit,trafficLight=TrafficLight.Green};
        var foods=new FoodDatabase();foods.SetItems(new List<FoodData>{food});
        foods.RebuildMinimumPrices(new[]{new StoreListingData{store=new StoreData{id="S"},food=food,gamePrice=1,servingsPerPackage=1}});
        var recipes=new RecipeDatabase();recipes.SetItems(new List<RecipeData>{recipe});
        var customers=new CustomerDatabase();customers.SetItems(new List<CustomerData>{new CustomerData{id="U",startingHealth=50}});
        var meals=new MealService(state,foods,recipes,customers,new BalanceConfig());
        var input=new ServeInput{recipeId="R",customerId="U",slotSelections=choices};
        Check(meals.Preview(input).recipeBuilt && early.servings==8,"Preview mutated or rejected exact slots");
        choices[0].slotIndex=1;
        Check(!meals.Preview(input).recipeBuilt,"Misplaced ingredients passed aggregated validation");
        choices[0].slotIndex=0;
        choices.Add(new MealSlotSelection{foodId="A",slotIndex=-1});
        Check(!meals.Preview(input).recipeBuilt,"Extra ingredients allowed");choices.RemoveAt(1);
        Check(MealAllocation.SlotWarning(recipe,1,choices,foods)==null,"Empty optional slot warned");
        choices.Add(new MealSlotSelection{foodId="A",slotIndex=1});
        Check(MealAllocation.SlotWarning(recipe,1,choices,foods)!=null,"Wrong optional ingredient accepted");choices.RemoveAt(1);
        early.servings=7;
        var committed=meals.Serve(input);
        Check(committed.success&&committed.recipeBuilt&&late.servings==1&&infinite.servings==2,"Commit failed to reallocate changed stock");
        Check(!meals.Serve(input).success,"Repeated serve allowed");
        return "PASS: earliest-expiry allocation, expired exclusions, non-mutating previews, slot mismatches, optional slots, extras, and commit revalidation.";
    }

    // Multi-category foods (Foods.csv also_categories, 2026-11-20): tofu fills Beans or Meat substitute, one serving once.
    public static string RunMultiCategory()
    {
        var tofu=new FoodData{id="T",recipeCategory=FoodCategory.Beans,alsoCategories=new[]{FoodCategory.MeatSubstitute},trafficLight=TrafficLight.Green,ingredientAppeal=5};
        var beans=new FoodData{id="B",recipeCategory=FoodCategory.Beans,trafficLight=TrafficLight.Green,ingredientAppeal=4};
        Check(tofu.Fits(FoodCategory.Beans)&&tofu.Fits(FoodCategory.MeatSubstitute)&&!beans.Fits(FoodCategory.MeatSubstitute)&&!tofu.Fits(FoodCategory.Unassigned),"Fits is wrong");
        var burger=new RecipeData{id="R",baseSalePrice=10,allowNonGreenChoices=true,slots=new[]{new RecipeSlot{category=FoodCategory.Beans,servings=1},new RecipeSlot{category=FoodCategory.MeatSubstitute,servings=1,optional=true}}};
        var nuggets=new RecipeData{id="N",baseSalePrice=10,allowNonGreenChoices=true,slots=new[]{new RecipeSlot{category=FoodCategory.MeatSubstitute,servings=1}}};
        var state=new GameSessionState();
        state.inventory.Add(new InventoryEntry{foodId="T",servings=2,daysRemaining=3,purchaseCostPerServing=1});
        state.inventory.Add(new InventoryEntry{foodId="B",servings=2,daysRemaining=3,purchaseCostPerServing=1});
        var foods=new FoodDatabase();foods.SetItems(new List<FoodData>{tofu,beans});
        foods.RebuildMinimumPrices(new[]{new StoreListingData{store=new StoreData{id="S"},food=tofu,gamePrice=1,servingsPerPackage=1},
            new StoreListingData{store=new StoreData{id="S"},food=beans,gamePrice=1,servingsPerPackage=1}});
        Check(foods.InCategory(FoodCategory.MeatSubstitute).Count==1&&foods.InCategory(FoodCategory.Beans).Count==2,"Category index ignores also_categories");
        var recipes=new RecipeDatabase();recipes.SetItems(new List<RecipeData>{burger,nuggets});
        var customers=new CustomerDatabase();customers.SetItems(new List<CustomerData>{new CustomerData{id="U",startingHealth=50}});
        var meals=new MealService(state,foods,recipes,customers,new BalanceConfig());
        var choices=new List<MealSlotSelection>{new MealSlotSelection{foodId="B",slotIndex=0},new MealSlotSelection{foodId="T",slotIndex=1}};
        var input=new ServeInput{recipeId="R",customerId="U",slotSelections=choices};
        var preview=meals.Preview(input);
        Check(preview.success&&preview.recipeBuilt,"Tofu as a meat substitute rejected: "+string.Join("; ",preview.recipeWarnings??new string[0]));
        choices[0].foodId="T";choices[1].foodId="B";
        Check(!meals.Preview(input).recipeBuilt,"Beans accepted as a meat substitute");
        choices.RemoveAt(1);
        Check(meals.Preview(input).recipeBuilt,"Tofu in the beans slot rejected");
        Check(new RecipeCheck(foods).CanMake(nuggets,state),"Tofu cannot cover a meat-substitute recipe");
        var both=new Dictionary<FoodCategory,long>{{FoodCategory.Beans,1},{FoodCategory.MeatSubstitute,1}};
        var one=RecipeRequirements.Assign(new Dictionary<FoodData,long>{{tofu,1}},both,null,true);
        long b,m;one.TryGetValue(FoodCategory.Beans,out b);one.TryGetValue(FoodCategory.MeatSubstitute,out m);
        Check(b+m==1,"One tofu serving counted twice");
        var two=RecipeRequirements.Assign(new Dictionary<FoodData,long>{{tofu,1},{beans,1}},both,null,true);
        two.TryGetValue(FoodCategory.Beans,out b);two.TryGetValue(FoodCategory.MeatSubstitute,out m);
        Check(b==1&&m==1,"Tofu not moved to the category that needs it");
        return "PASS: multi-category foods fit either slot, count once, and cover recipes, checks and the category index.";
    }
}
