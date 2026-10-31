// End-to-end tutorial regression checks through public GameLoop commands (no UI bypass assumptions).
using System;
using System.Collections.Generic;
using System.Linq;
public static class TutorialChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static TutorialDefinition Def(string id,string flag,string behaviour,string phase,params string[] steps)=>new TutorialDefinition{
        id=id,triggerFlag=flag,behaviourId=behaviour,activationPhase=phase,completionFlags=new[]{id+".done"},
        steps=steps.Select(s=>new TutorialStepText{stepId=s,text="{recipe} {requirements} {ingredients} {healthDelta} {dailyDozen}"}).ToList()};
    public static TutorialDatabase Content()
    {
        var result=new TutorialDatabase();result.tutorials.AddRange(new[]{
            Def("shop","tutorial.shopping_available","Shopping","Market","shop"),
            Def("sort","tutorial.sorting_available","TextOnly","FoodSorting","explain"),
            Def("cook","tutorial.cooking_available","FirstMeal","Service","choose","build","feedback"),
            Def("crash",TutorialFlags.CrashExplained,"TextOnly","Service","explain")});return result;
    }
    public static string Run()
    {
        var state=new GameSessionState();var balance=new BalanceConfig();
        var grain=new FoodData{id="G",displayName="Oats",recipeCategory=FoodCategory.Grain,trafficLight=TrafficLight.Green,storageType=StorageType.Dry,dailyDozenCategories=new[]{DailyDozenCategory.WholeGrains}};
        var veg=new FoodData{id="V",displayName="Carrot",recipeCategory=FoodCategory.Vegetable,trafficLight=TrafficLight.Green,storageType=StorageType.Fresh};
        var foods=new FoodDatabase();foods.SetItems(new List<FoodData>{grain,veg});
        var store=new StoreData{id="S"};var listings=new StoreListingDatabase();
        listings.SetItems(new List<StoreListingData>{new StoreListingData{id="LG",food=grain,store=store,servingsPerPackage=2,gamePrice=2},new StoreListingData{id="LV",food=veg,store=store,servingsPerPackage=1,gamePrice=2}});
        foods.RebuildMinimumPrices(listings.Listings);
        var recipe=new RecipeData{id="R001",displayName="Sandwich",slots=new[]{new RecipeSlot{category=FoodCategory.Grain,servings=2},new RecipeSlot{category=FoodCategory.Vegetable,servings=1}},baseSalePrice=7};
        var recipes=new RecipeDatabase();recipes.SetItems(new List<RecipeData>{recipe});
        var customer=new CustomerData{id="U",startingHealth=50};var customers=new CustomerDatabase();customers.SetItems(new List<CustomerData>{customer});
        var dialogues=new DialogueDatabase();dialogues.SetItems(new List<DialogueSequenceData>{new DialogueSequenceData{id="crash",customer=customer,trigger=DialogueTrigger.CrashArrival,lines=new[]{new DialogueLineData{speakerId="U",textTemplate="The lift faded."}}}});
        var plan=new IntroMealPlan(state,foods,recipes,listings,balance);
        var lessons=new TutorialController(state,Content(),plan);
        var loop=new GameLoop(new PurchaseService(state,listings,balance),new MealService(state,foods,recipes,customers,balance),new DayService(state,balance,customers),new CustomerVisitService(state,customers,balance,()=>0),new EncounterScheduler(state,dialogues,customers,recipes),new FoodSortingService(state,foods));
        loop.AttachTutorials(lessons);
        Check(lessons.Active.id=="shop"&&!loop.ContinueTutorial(),"Shopping start/text bypass failed");
        Check(!loop.Checkout(new List<CartEntry>()).success&&state.money==30,"Empty checkout bypassed tutorial");
        var cart=new List<CartEntry>{new CartEntry{listingId="LG"},new CartEntry{listingId="LV"}};
        Check(loop.Checkout(cart).success&&state.flags.HasFlag("shop.done")&&lessons.Active.id=="sort","Shopping to sorting chain failed");
        var answers=new List<SortingChoice>{new SortingChoice{foodId="G",answer=TrafficLight.Green},new SortingChoice{foodId="V",answer=TrafficLight.Green}};
        Check(!loop.SubmitSorting(answers).success&&!loop.BeginService(),"Sorting tutorial lock bypassed");
        Check(loop.ContinueTutorial()&&loop.SubmitSorting(answers).success&&loop.BeginService(),"Sorting lesson continuation failed");
        Check(lessons.Active.id=="cook"&&lessons.Record.stepId=="choose","Cooking start failed");
        Check(!loop.SelectRecipe("OTHER").success&&!loop.SkipCustomer(),"Unknown recipe or skip was not blocked");
        Check(loop.SelectRecipe("R001").success&&lessons.Record.stepId=="build","Recipe step did not advance");
        var input=new ServeInput{customerId="U",recipeId="R001",slotSelections=new List<MealSlotSelection>{new MealSlotSelection{slotIndex=0,foodId="G",servings=1},new MealSlotSelection{slotIndex=1,foodId="V",servings=1}}};
        Check(loop.PreviewServe(input).success&&!loop.PreviewServe(input).recipeBuilt,"Mismatched preview was hidden");
        int before=state.inventory.Sum(e=>e.servings);
        Check(!loop.Serve(input).success&&state.inventory.Sum(e=>e.servings)==before,"Tutorial rejection consumed stock");
        input.slotSelections[0].servings=2;
        Check(loop.Serve(input).success&&lessons.IsText&&lessons.Record.stepId=="feedback","Successful serve did not activate feedback");
        Check(lessons.Instruction.Contains("Oats")&&!lessons.Instruction.Contains("{ingredients}"),"Actual meal feedback missing");
        Check(!loop.ContinueAfterDish()&&!loop.SkipCustomer(),"Meal feedback skipped");
        Check(loop.ContinueTutorial()&&state.flags.HasFlag("cook.done")&&loop.ContinueAfterDish(),"Cooking completion failed");
        RushCrashProgress.Served(state.customers[0],1,true);
        Check(loop.FinishDay().success&&loop.Checkout(new List<CartEntry>()).success&&loop.Dialogue.IsActive,"Crash setup failed");
        Check(lessons.Active==null,"Crash tutorial started before dialogue completion");
        Check(loop.AdvanceDialogue()&&lessons.Active.id=="crash"&&!loop.SkipCustomer(),"Crash tutorial did not pause the next action");
        Check(loop.ContinueTutorial()&&state.flags.HasFlag("crash.done")&&loop.SkipCustomer(),"Crash lesson did not release restrictions");
        lessons.Dispose();
        // Recreate around saved persistent records: handled lessons must not repeat.
        var resumed=new TutorialController(state,Content(),plan);loop.AttachTutorials(resumed);
        Check(resumed.Active==null,"Completed tutorial restarted on resume");resumed.Dispose();
        var poor=new GameSessionState{money=0};var poorPlan=new IntroMealPlan(poor,foods,recipes,listings,balance);
        var skipped=new TutorialController(poor,Content(),poorPlan);loop.AttachTutorials(skipped);
        // A new market loop lets the infeasible shopping lesson be evaluated at its proper boundary.
        var emptyCustomers=new CustomerDatabase();var poorLoop=new GameLoop(new PurchaseService(poor,listings,balance),new MealService(poor,foods,recipes,customers,balance),new DayService(poor,balance),new CustomerVisitService(poor,emptyCustomers,balance),null,new FoodSortingService(poor,foods));
        poorLoop.AttachTutorials(skipped);
        Check(skipped.Active==null&&poor.tutorials.Any(r=>r.tutorialId=="shop"&&r.status=="skipped")&&!poor.flags.HasFlag("shop.done"),"Infeasible lesson did not skip safely");
        Check(poorLoop.Checkout(new List<CartEntry>()).success,"Skipped lesson retained lock");skipped.Dispose();
        // Regression (2026-10-30): a checkout that leaves money below rent must still move the day on, and the
        // money-low lesson (sell pantry food) must not start after checkout. It used to block the move to service
        // after the purchase had already gone through, leaving the market stuck.
        var rentBalance=new BalanceConfig{rentPerDay=10,rentStartDay=1};
        var tight=new GameSessionState{money=11};tight.correctlySortedFoodIds.Add("G");
        var sellLesson=new TutorialDatabase();sellLesson.tutorials.Add(Def("sell",TutorialFlags.MoneyLow,"TextOnly","Market","explain"));
        var tightLessons=new TutorialController(tight,sellLesson,new IntroMealPlan(tight,foods,recipes,listings,rentBalance));
        var tightLoop=new GameLoop(new PurchaseService(tight,listings,rentBalance),new MealService(tight,foods,recipes,customers,rentBalance),new DayService(tight,rentBalance),new CustomerVisitService(tight,emptyCustomers,rentBalance),null,new FoodSortingService(tight,foods));
        tightLoop.AttachTutorials(tightLessons);
        Check(tightLessons.Active==null&&tightLoop.Checkout(new List<CartEntry>{new CartEntry{listingId="LG"}}).success,"Known-food checkout failed");
        Check(tight.money<10&&tightLoop.Phase!=GamePhase.Market,"Checkout left the day stuck at the market");
        Check(tightLessons.Active==null&&!tight.flags.HasFlag(TutorialFlags.MoneyLow),"Money-low lesson started after checkout");
        tightLessons.Dispose();
        return "PASS: all four tutorial flows, progress guards, preview freedom, no partial mutations, actual dish feedback, crash timing, resume, infeasible skips and post-checkout transitions.";
    }
}
