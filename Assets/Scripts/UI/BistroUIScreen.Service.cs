// Canvas service presentation: grouped pantry cards, recipe slots, dialogue and results.
// UI holds food/slot intent only; MealService allocates batches and validates again on confirmation.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private RecipeData mealRecipe;
    private readonly List<MealSlotSelection> mealSelections=new List<MealSlotSelection>();
    private ServeResult mealPreview;
    private AudioSource dialogueAudio;
    private DialogueLineData lastVoicedLine;
    private void ResetMeal(){mealRecipe=null;mealSelections.Clear();mealPreview=null;}
    private ServeInput MealInput()=>new ServeInput{customerId=game.Loop.Service.CurrentCustomerId,recipeId=mealRecipe.id,
        slotSelections=mealSelections.Select(s=>new MealSlotSelection{slotIndex=s.slotIndex,foodId=s.foodId,servings=s.servings}).ToList()};
    private void SyncVoice()
    {
        var line=game?.Loop?.Tutorials?.IsText==true?null:game?.Loop?.Dialogue?.CurrentLine;if(ReferenceEquals(line,lastVoicedLine))return;lastVoicedLine=line;
        if(dialogueAudio!=null)dialogueAudio.Stop();
        if(line?.voiceOver==null)return;
        if(dialogueAudio==null){dialogueAudio=gameObject.AddComponent<AudioSource>();dialogueAudio.playOnAwake=false;}
        dialogueAudio.clip=line.voiceOver;dialogueAudio.Play();
    }
    private void ServiceScreen()
    {
        var scene=BistroWidgets.Box(screen,"Bistro interior",theme.panel,Vector2.zero,new Vector2(1,.91f));
        var background=scene.GetComponent<Image>();background.sprite=theme.serviceBackground;background.color=Color.white;background.raycastTarget=false;
        var playback=game.Loop.Dialogue;
        var guest=game.Queries.GetCustomer(playback?.IsActive==true?playback.CurrentLine.speakerId:game.Loop.Service.CurrentCustomerId);
        var portraitArt=playback?.IsActive==true&&playback.CurrentLine.portrait!=null?playback.CurrentLine.portrait:guest?.portrait;
        var person=BistroWidgets.Box(scene,"Customer layer",Color.clear,new Vector2(.34f,.13f),new Vector2(.66f,.88f));
        person.GetComponent<Image>().raycastTarget=false;
        if(portraitArt!=null){person.GetComponent<Image>().sprite=portraitArt;person.GetComponent<Image>().color=Color.white;person.GetComponent<Image>().preserveAspect=true;}
        else BistroShelfIcon.Add(person,"Customer",Vector2.zero,Vector2.one,new Color(.30f,.39f,.36f));
        BistroShelfStyle.Artwork(scene,"Service counter",theme.serviceCounter,Vector2.zero,new Vector2(1,.30f),Color.white);
        if(playback!=null && playback.IsActive)
        {
            var speaker=game.Queries.GetCustomer(playback.CurrentLine.speakerId);
            DialoguePanel(speaker?.displayName??playback.CurrentLine.speakerId,playback.CurrentText,"Continue",()=>game.Loop.AdvanceDialogue());return;
        }
        var room=scene;
        if(!game.Loop.Service.awaitingDishAcknowledgement){
            BistroWidgets.Box(room,"Cooking dim",new Color(0,0,0,.24f),Vector2.zero,Vector2.one);
            scene=BistroShelfStyle.Panel(room,"Cooking workspace",new Color(.94f,.92f,.86f,.96f),new Vector2(.025f,.025f),new Vector2(.975f,.97f));
        }
        var customer=game.Queries.GetCustomer(game.Loop.Service.CurrentCustomerId);
        if(game.Loop.Service.awaitingDishAcknowledgement)
        {
            DialoguePanel(customer?.displayName??"Your guest",DishText(game.Loop.Service.lastDishResult),"Continue",()=>{ResetMeal();game.Loop.ContinueAfterDish();});return;
        }
        // Header, left-aligned in two bands: who the guest is (name, request, stats), then the dish being built.
        var muted=new Color(.30f,.36f,.33f);
        BistroShelfStyle.Text(scene,customer?.displayName??"Service",theme,new Vector2(.03f,.935f),new Vector2(.74f,.99f),26,BistroShelfStyle.Ink,true);
        var cravings=game.Queries.GetActiveCravings(customer?.id);
        BistroShelfStyle.Text(scene,cravings.Length==0?"No special request today":"Hoping for: "+string.Join(", ",cravings),theme,new Vector2(.03f,.898f),new Vector2(.74f,.935f),18,BistroShelfStyle.Ink);
        BistroShelfStyle.Text(scene,GuestInfo(customer),theme,new Vector2(.03f,.865f),new Vector2(.74f,.898f),15,muted);
        ServiceButton(scene,"Skip customer",new Vector2(.78f,.89f),new Vector2(.97f,.97f),BistroActionRole.Caution,()=>Change(()=>{
            if(game.Loop.SkipCustomer())ResetMeal();else notice=game.Loop.Tutorials?.LastError??"Cannot skip right now.";}));
        if(mealRecipe==null)
        {
            bool aid=!string.IsNullOrEmpty(game.Loop.Service.foodAidMessage);
            if(aid)BistroWidgets.Label(scene,game.Loop.Service.foodAidMessage,theme,new Vector2(.03f,.78f),new Vector2(.97f,.85f),18);
            var recipes=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.04f),new Vector2(.97f,aid?.77f:.84f));
            recipes.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(290,370));
            ((RectTransform)recipes.parent).GetComponent<Image>().color=Color.clear;
            foreach(var recipe in game.Queries.GetUnlockedRecipes()){
                var card=BistroRecipeCard.Create(recipes,recipe,theme,game.Queries.Foods);
                card.Selected=()=>Change(()=>{
                    var result=game.Loop.SelectRecipe(recipe.id);
                    if(result.success){mealRecipe=recipe;mealSelections.Clear();notice="";}else notice=result.error;});
                bool ready=game.Queries.CanMake(recipe);if(!ready)card.MarkUnavailable();
                string history=RecipeHistory(recipe);
                string footer=!ready?"Not enough in your pantry":history==""?"Choose recipe":history;
                foreach(var label in card.GetComponentsInChildren<Text>())if(label.text=="Click to flip")label.text=footer;
            }
            BistroWidgets.EmptyShelf(recipes,"No recipes are unlocked yet.",theme);
            return;
        }
        BistroWidgets.Box(scene,"Header rule",new Color(.73f,.73f,.67f),new Vector2(.03f,.856f),new Vector2(.97f,.858f)).GetComponent<Image>().raycastTarget=false;
        ServiceButton(scene,"← Recipes",new Vector2(.03f,.775f),new Vector2(.17f,.845f),BistroActionRole.Secondary,()=>Change(ResetMeal));
        BistroShelfStyle.Text(scene,mealRecipe.displayName,theme,new Vector2(.19f,.81f),new Vector2(.70f,.85f),22,BistroShelfStyle.Ink,true);
        BistroShelfStyle.Text(scene,$"Base price ${mealRecipe.baseSalePrice:0.00} · "+PlateAppealText(),theme,new Vector2(.19f,.775f),new Vector2(.70f,.81f),16,muted);
        ServiceButton(scene,"Review dish",new Vector2(.75f,.775f),new Vector2(.97f,.845f),BistroActionRole.Primary,()=>Change(()=>{
            mealPreview=game.Loop.PreviewServe(MealInput());if(mealPreview.success)modal="meal-preview";else notice=mealPreview.error;}));
        var slots=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.43f),new Vector2(.97f,.76f));
        slots.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(230,185));
        for(int i=0;i<=mealRecipe.slots.Length;i++)
        {
            int index=i==mealRecipe.slots.Length?-1:i;
            var requirement=index<0?null:mealRecipe.slots[index];
            string title=requirement==null?"Extra ingredients":$"{IntroMealPlan.CategoryName(requirement.category)} ×{requirement.servings}"+(requirement.optional?" (optional)":"");
            string warning=MealAllocation.SlotWarning(mealRecipe,index,mealSelections,game.Queries.Foods);
            var slotView=BistroDropSlot.Create(slots,title,warning??"Empty · optional",theme,Vector2.zero,Vector2.one,warning!=null,
                (id,kind)=>{if(kind=="meal-food")Change(()=>AddServing(index,id));else if(kind=="meal-move")Change(()=>MoveServing(id,index));},null);
            slotView.ApplyIllustratedContents(null,requirement==null?null:game.Queries.Foods.CategoryIcon(requirement.category));
            // One row per food in the slot. Drag a row to another slot to move one serving; drag it out to put one back.
            slotView.ShowRows(mealSelections.Where(s=>s.slotIndex==index).Select(s=>new BistroDropSlot.Row{key=index+"|"+s.foodId,
                label=$"{game.Queries.Foods.GetById(s.foodId)?.displayName??s.foodId} ×{s.servings}",art=game.Queries.Foods.GetById(s.foodId)?.icon}).ToList(),
                "meal-move",key=>Change(()=>RemoveServing(key)),theme);
        }
        var pantry=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.025f),new Vector2(.97f,.40f));
        ((RectTransform)pantry.parent).GetComponent<Image>().color=Color.clear;
        ((RectTransform)slots.parent).GetComponent<Image>().color=Color.clear;
        foreach(var group in game.State.inventory.Where(MealAllocation.Usable).GroupBy(e=>e.foodId,StringComparer.OrdinalIgnoreCase))
        {
            int available=group.Sum(e=>e.servings)-mealSelections.Where(s=>string.Equals(s.foodId,group.Key,StringComparison.OrdinalIgnoreCase)).Sum(s=>s.servings);
            int expiry=group.Where(e=>e.daysRemaining>=0).Select(e=>e.daysRemaining).DefaultIfEmpty(-1).Min();
            var foodCard=BistroFoodCard.Create(pantry,game.Queries.Foods.GetById(group.Key),group.Key,"meal-food",$"{available} available servings\nEarliest: {Expiry(expiry)}",theme,available>0);
            foodCard.ApplyShelfArtwork(game.Queries.Foods.GetById(group.Key),"",available);
        }
        BistroWidgets.EmptyShelf(pantry,"No usable food remains in your pantry.",theme);
    }
    // Global recipe fatigue, from the same helper MealService uses. Shown on the service recipe card.
    private string RecipeHistory(RecipeData recipe)
    {
        if(!RecipeFatigue.Enabled(game.Balance))return "";
        int n=RecipeFatigue.RecentServes(game.State,game.Balance,recipe.id);
        if(n==0)return "";
        return RecipeFatigue.IsFatigued(game.State,game.Balance,recipe.id)
            ?$"Served {n}× in {game.Balance.recipeFatigueWindowDays} days · popularity ×{game.Balance.recipeFatigueMultiplier:0.##}"
            :$"Served {n}× in {game.Balance.recipeFatigueWindowDays} days";
    }
    // What the player can know about a guest before cooking: health, delight and Daily Dozen groups eaten so far.
    private string GuestInfo(CustomerData customer)
    {
        if(customer==null)return "";
        var cs=game.State.customers.Find(c=>string.Equals(c.customerId,customer.id,StringComparison.OrdinalIgnoreCase));
        int health=cs?.health??customer.startingHealth, delight=cs?.delight??game.Balance.startingDelight;
        var eaten=cs?.dailyDozenEaten??new List<DailyDozenCategory>();
        int groups=Enum.GetValues(typeof(DailyDozenCategory)).Length-1;
        return $"Health {health} · Delight {delight} · Daily Dozen eaten {eaten.Count}/{groups}"+(eaten.Count==0?"":": "+IntroMealPlan.ReadableList(eaten.OrderBy(g=>g)));
    }
    // Mean appeal of the servings placed so far (cards show each food's appeal, so this is visible information).
    private string PlateAppealText()
    {
        int n=mealSelections.Sum(x=>x.servings);

        if(n==0)return "Plate appeal —";
        double appeal=mealSelections.Sum(x=>(game.Queries.Foods.GetById(x.foodId)?.ingredientAppeal??0)*(double)x.servings)/n;
        return $"Plate appeal {appeal:0.0}";
    }
    private void AddServing(int index,string foodId)
    {
        int available=game.State.inventory.Where(e=>MealAllocation.Usable(e)&&string.Equals(e.foodId,foodId,StringComparison.OrdinalIgnoreCase)).Sum(e=>e.servings);
        if(mealSelections.Where(s=>s.foodId==foodId).Sum(s=>s.servings)>=available){notice="No more available servings.";return;}
        var item=mealSelections.Find(s=>s.slotIndex==index&&s.foodId==foodId);
        if(item==null)mealSelections.Add(new MealSlotSelection{slotIndex=index,foodId=foodId});else item.servings++;
        notice="";
    }
    // Row keys are "slotIndex|foodId".
    private static bool ParseServingKey(string key,out int index,out string foodId)
    {
        index=-2;foodId=null;int bar=key==null?-1:key.IndexOf('|');
        if(bar<1||!int.TryParse(key.Substring(0,bar),out index))return false;
        foodId=key.Substring(bar+1);return foodId.Length>0;
    }
    private void RemoveServing(string key)
    {
        if(!ParseServingKey(key,out int index,out string foodId))return;
        var item=mealSelections.Find(s=>s.slotIndex==index&&s.foodId==foodId);
        if(item!=null&&--item.servings<=0)mealSelections.Remove(item);
    }
    private void MoveServing(string key,int toIndex)
    {
        if(!ParseServingKey(key,out int fromIndex,out string foodId)||fromIndex==toIndex)return;
        RemoveServing(key);AddServing(toIndex,foodId);
    }
    // Pre-serve review shows only what a cook can see: whether the plate matches the recipe and what is on it.
    // Health, delight, popularity, payment, tips and cost are revealed after serving (DishText).
    private string ReviewText(ServeResult r)=>
        (r.recipeBuilt?"The plate matches the recipe.":"The plate does not match the recipe. A mismatched dish pays less, earns no tip and disappoints the guest.")+
        (r.recipeWarnings.Length==0?"":"\n\n"+string.Join("\n",r.recipeWarnings))+
        "\n\nOn the plate: "+string.Join(", ",mealSelections.GroupBy(s=>s.foodId).Select(g=>$"{game.Queries.Foods.GetById(g.Key)?.displayName??g.Key} ×{g.Sum(s=>s.servings)}"))+
        (r.activeCravings.Length==0?"\nThis guest has no request today.":!r.recipeBuilt?"\nRequests answered: none until the plate matches the recipe.":$"\nRequests answered: {r.cravingsResolved.Length} of {r.activeCravings.Length}"+
            (r.cravingsResolved.Length==0?"":$" ({string.Join(", ",r.cravingsResolved)})")+
            (r.cravingsResolved.Length==r.activeCravings.Length?"":$" · not answered: {string.Join(", ",r.activeCravings.Except(r.cravingsResolved))}"))+
        "\n\nHow the guest reacts, and what they pay, is revealed after serving.";
    private static string DishText(ServeResult r)=>
        (r.recipeBuilt?"Recipe built successfully":"Recipe mismatch — reduced payment; no tip")+"\n\n"+string.Join("\n",r.recipeWarnings)+
        $"\nHealth {r.healthDelta:+0;-0;0} · Delight {r.delightDelta:+0;-0;0}"+(r.delightDelta==0&&r.dishDelight>0?" (guest was already fully delighted)":"")+$"\nIngredient appeal {r.ingredientAppeal:0.0} · Popularity {r.popularityDelta:+0.#;-0.#;0}"+(r.recipeFatigued?" (guests are used to this recipe)":"")+"\nCravings resolved: "+(r.activeCravings.Length==0?"No request today":$"{string.Join(", ",r.cravingsResolved)} ({r.cravingsResolved.Length}/{r.activeCravings.Length})")+
        $"\nPayment ${r.basePayment:0.00} + tip ${r.tip:0.00}\nPaid ingredient cost ${r.ingredientCost:0.00}\nDaily Dozen: "+IntroMealPlan.ReadableList(r.dailyDozenCovered);
    private void ResultsScreen()
    {
        var summary=game.Loop.Summary;bool complete=game.Loop.Phase==GamePhase.Completed;
        if(complete && game.State.completedDaySummary!=null)summary=game.State.completedDaySummary;
        BistroShelfStyle.Artwork(screen,"Service room",theme.serviceBackground,Vector2.zero,new Vector2(1,.91f),Color.white);
        string totals=$"Spent ${summary.moneySpent:0.00}\nEarned ${summary.moneyEarned:0.00}\nServed {summary.dishes.Count} · Skipped {summary.skippedCustomerIds.Count}";
        totals+=$"\nPopularity {game.State.popularity:0} ({summary.dishes.Sum(d=>d.result.popularityDelta):+0.#;-0.#;0} today)";
        if(summary.walkInIncome>0)totals+=$"\nWalk-in takeaway ${summary.walkInIncome:0.00} (included in earnings)";
        else if(game.Balance.walkInIncomePerPopularity>0)totals+=$"\nNo walk-ins yet: passers-by start coming once popularity passes {game.Balance.walkInPopularityThreshold:0}.";
        if(!string.IsNullOrEmpty(summary.foodAidMessage))totals+="\n"+summary.foodAidMessage;
        if(summary.pantrySales>0)totals+=$"\nSold pantry food ${summary.pantrySales:0.00}";
        if(summary.rentPaid>0)totals+=$"\nRent paid this morning ${summary.rentPaid:0.00}";
        if(!complete&&game.Balance.rentPerDay>0&&game.State.currentDay+1>=game.Balance.rentStartDay)
            totals+=game.State.currentDay+1==game.Balance.rentStartDay?$"\nNotice from the landlord: rent of ${game.Balance.rentPerDay:0.00} is due every morning from tomorrow.":$"\nRent due tomorrow morning ${game.Balance.rentPerDay:0.00}";
        totals+=$"\nMoney now ${game.State.money:0.00}";
        string dishes=string.Join("\n\n",summary.dishes.Select(d=>
            $"{game.Queries.GetCustomer(d.customerId)?.displayName} · {d.recipeId}\n"+
            $"{(d.result.recipeBuilt?"Recipe built":"Recipe mismatch")} · Health {d.result.healthDelta:+0;-0;0} · Delight {d.result.delightDelta:+0;-0;0} · Earned ${d.result.moneyEarned:0.00}"));
        DialoguePanel("Day complete",totals+"\n\n"+dishes,"Next day",()=>{
            if(!complete){var result=game.Loop.FinishDay();notice=result.success?"":result.error;}
        });
    }
}
