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
        var line=game?.Loop?.Dialogue?.CurrentLine;if(ReferenceEquals(line,lastVoicedLine))return;lastVoicedLine=line;
        if(dialogueAudio!=null)dialogueAudio.Stop();
        if(line?.voiceOver==null)return;
        if(dialogueAudio==null){dialogueAudio=gameObject.AddComponent<AudioSource>();dialogueAudio.playOnAwake=false;}
        dialogueAudio.clip=line.voiceOver;dialogueAudio.Play();
    }
    private void ServiceScreen()
    {
        var scene=BistroWidgets.Box(screen,"Bistro interior",theme.panel,new Vector2(.03f,.075f),new Vector2(.97f,.90f));
        if(theme.serviceBackground!=null){scene.GetComponent<Image>().sprite=theme.serviceBackground;scene.GetComponent<Image>().color=Color.white;}
        var playback=game.Loop.Dialogue;
        if(playback!=null && playback.IsActive)
        {
            var speaker=game.Queries.GetCustomer(playback.CurrentLine.speakerId);
            var portrait=BistroWidgets.Box(scene,"Speaker portrait",theme.background,new Vector2(.32f,.34f),new Vector2(.68f,.94f));
            var art=playback.CurrentLine.portrait!=null?playback.CurrentLine.portrait:speaker?.portrait;
            if(art!=null){portrait.GetComponent<Image>().sprite=art;portrait.GetComponent<Image>().preserveAspect=true;portrait.GetComponent<Image>().color=Color.white;}
            else BistroWidgets.Label(portrait,speaker?.displayName??playback.CurrentLine.speakerId,theme,Vector2.zero,Vector2.one,28);
            var box=BistroWidgets.Box(scene,"Dialogue",theme.background,new Vector2(.04f,.03f),new Vector2(.96f,.34f));
            BistroWidgets.Label(box,speaker?.displayName??playback.CurrentLine.speakerId,theme,new Vector2(.04f,.73f),new Vector2(.80f,.97f),24);
            BistroWidgets.Label(box,playback.CurrentText,theme,new Vector2(.04f,.20f),new Vector2(.96f,.72f),24);
            BistroWidgets.Button(box,"Continue",theme,new Vector2(.77f,.02f),new Vector2(.97f,.20f),()=>Change(()=>game.Loop.AdvanceDialogue()));return;
        }
        var customer=game.Queries.GetCustomer(game.Loop.Service.CurrentCustomerId);
        BistroWidgets.Label(scene,customer?.displayName??"Service",theme,new Vector2(.03f,.87f),new Vector2(.64f,.98f),28);
        if(game.Loop.Service.awaitingDishAcknowledgement)
        {
            BistroWidgets.ReadingPanel(scene,DishText(game.Loop.Service.lastDishResult),theme,new Vector2(.06f,.24f),new Vector2(.94f,.83f));
            BistroWidgets.Button(scene,"Continue",theme,new Vector2(.63f,.06f),new Vector2(.96f,.18f),()=>Change(()=>{ResetMeal();game.Loop.ContinueAfterDish();}));return;
        }
        BistroWidgets.Button(scene,"Skip customer",theme,new Vector2(.75f,.89f),new Vector2(.97f,.97f),()=>Change(()=>{ResetMeal();game.Loop.SkipCustomer();}));
        if(mealRecipe==null)
        {
            var recipes=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.04f),new Vector2(.97f,.84f));
            recipes.GetComponent<GridLayoutGroup>().cellSize=new Vector2(245,135);
            foreach(var recipe in game.Queries.GetUnlockedRecipes())
                BistroWidgets.Button(recipes,recipe.displayName+"\n"+(game.Queries.CanMake(recipe)?"Ingredients available":"Substitutions possible"),theme,Vector2.zero,Vector2.one,()=>Change(()=>{mealRecipe=recipe;mealSelections.Clear();}));
            return;
        }
        BistroWidgets.Button(scene,"← Recipes",theme,new Vector2(.03f,.78f),new Vector2(.20f,.86f),()=>Change(ResetMeal));
        BistroWidgets.Label(scene,mealRecipe.displayName,theme,new Vector2(.22f,.78f),new Vector2(.68f,.86f),23);
        BistroWidgets.Button(scene,"Review dish",theme,new Vector2(.72f,.78f),new Vector2(.97f,.86f),()=>Change(()=>{
            mealPreview=game.Loop.PreviewServe(MealInput());if(mealPreview.success)modal="meal-preview";else notice=mealPreview.error;}));
        var slots=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.43f),new Vector2(.97f,.76f));
        slots.GetComponent<GridLayoutGroup>().cellSize=new Vector2(230,185);
        for(int i=0;i<=mealRecipe.slots.Length;i++)
        {
            int index=i==mealRecipe.slots.Length?-1:i;
            var requirement=index<0?null:mealRecipe.slots[index];
            string title=requirement==null?"Extra ingredients":$"{requirement.category} ×{requirement.servings}"+(requirement.optional?" (optional)":"");
            string warning=MealAllocation.SlotWarning(mealRecipe,index,mealSelections,game.Queries.Foods);
            BistroDropSlot.Create(slots,title,warning??"Empty · optional",theme,Vector2.zero,Vector2.one,warning!=null,
                (id,kind)=>{if(kind=="meal-food")Change(()=>AddServing(index,id));},()=>Change(()=>modal="meal:"+index),
                mealSelections.Where(s=>s.slotIndex==index).Select(s=>$"{game.Queries.Foods.GetById(s.foodId)?.displayName} ×{s.servings}").ToArray());
        }
        var pantry=BistroWidgets.Grid(scene,theme,new Vector2(.03f,.025f),new Vector2(.97f,.40f));
        foreach(var group in game.State.inventory.Where(MealAllocation.Usable).GroupBy(e=>e.foodId,StringComparer.OrdinalIgnoreCase))
        {
            int available=group.Sum(e=>e.servings)-mealSelections.Where(s=>string.Equals(s.foodId,group.Key,StringComparison.OrdinalIgnoreCase)).Sum(s=>s.servings);
            int expiry=group.Where(e=>e.daysRemaining>=0).Select(e=>e.daysRemaining).DefaultIfEmpty(-1).Min();
            BistroFoodCard.Create(pantry,game.Queries.Foods.GetById(group.Key),group.Key,"meal-food",$"{available} available servings\nEarliest: {Expiry(expiry)}",theme,available>0);
        }
    }
    private void AddServing(int index,string foodId)
    {
        int available=game.State.inventory.Where(e=>MealAllocation.Usable(e)&&string.Equals(e.foodId,foodId,StringComparison.OrdinalIgnoreCase)).Sum(e=>e.servings);
        if(mealSelections.Where(s=>s.foodId==foodId).Sum(s=>s.servings)>=available){notice="No more available servings.";return;}
        var item=mealSelections.Find(s=>s.slotIndex==index&&s.foodId==foodId);
        if(item==null)mealSelections.Add(new MealSlotSelection{slotIndex=index,foodId=foodId});else item.servings++;
        notice="One serving selected. Pantry is consumed only after confirmation.";
    }
    private void MealModal(RectTransform panel)
    {
        if(modal=="meal-preview")
        {
            BistroWidgets.ReadingPanel(panel,DishText(mealPreview),theme,new Vector2(.05f,.24f),new Vector2(.95f,.85f));
            BistroWidgets.Button(panel,"Confirm and serve",theme,new Vector2(.56f,.06f),new Vector2(.95f,.18f),()=>Change(()=>{
                var result=game.Loop.Serve(MealInput());modal=null;if(result.success)ResetMeal();else notice=result.error;}));return;
        }
        int index=int.Parse(modal.Substring(5));
        var grid=BistroWidgets.Grid(panel,theme,new Vector2(.03f,.25f),new Vector2(.97f,.83f));
        foreach(var item in mealSelections.Where(s=>s.slotIndex==index))
            BistroFoodCard.Create(grid,game.Queries.Foods.GetById(item.foodId),item.foodId,"remove-meal",$"{item.servings} selected servings\nDrag to return one",theme);
        BistroDropSlot.Create(panel,"RETURN ONE SERVING",MealAllocation.SlotWarning(mealRecipe,index,mealSelections,game.Queries.Foods)??"Slot requirement satisfied",theme,
            new Vector2(.06f,.025f),new Vector2(.94f,.22f),false,(id,kind)=>{if(kind=="remove-meal")Change(()=>{
                var item=mealSelections.Find(s=>s.slotIndex==index&&s.foodId==id);if(item!=null&&--item.servings==0)mealSelections.Remove(item);});},null);
    }
    private static string DishText(ServeResult r)=>
        (r.recipeBuilt?"Recipe built successfully":"Recipe mismatch — reduced payment; no tip")+"\n\n"+string.Join("\n",r.recipeWarnings)+
        $"\nHealth {r.healthDelta:+0;-0;0} · Delight {r.delightDelta:+0;-0;0}\nPayment ${r.basePayment:0.00} + tip ${r.tip:0.00}\nPaid ingredient cost ${r.ingredientCost:0.00}\nDaily Dozen: "+string.Join(", ",r.dailyDozenCovered);
    private void ResultsScreen()
    {
        var summary=game.Loop.Summary;bool complete=game.Loop.Phase==GamePhase.Completed;
        BistroWidgets.Label(screen,complete?"YOUR BISTRO · COMPLETE":"DAY COMPLETE",theme,new Vector2(.05f,.72f),new Vector2(.95f,.87f),34);
        string totals=$"Spent ${summary.moneySpent:0.00}\nEarned ${summary.moneyEarned:0.00}\nServed {summary.dishes.Count} · Skipped {summary.skippedCustomerIds.Count}";
        string dishes=string.Join("\n\n",summary.dishes.Select(d=>
            $"{game.Queries.GetCustomer(d.customerId)?.displayName} · {d.recipeId}\n"+
            $"{(d.result.recipeBuilt?"Recipe built":"Recipe mismatch")} · Health {d.result.healthDelta:+0;-0;0} · Delight {d.result.delightDelta:+0;-0;0} · Earned ${d.result.moneyEarned:0.00}"));
        BistroWidgets.ReadingPanel(screen,totals+"\n\n"+dishes,theme,new Vector2(.08f,.27f),new Vector2(.92f,.70f));
        if(!complete)BistroWidgets.Button(screen,"Finish day / next market",theme,new Vector2(.55f,.10f),new Vector2(.94f,.22f),()=>Change(()=>{
            var result=game.Loop.FinishDay();notice=result.success?$"Expired batches removed: {result.expiredInventoryEntryIds.Length}":result.error;}));
    }
}

