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
}

