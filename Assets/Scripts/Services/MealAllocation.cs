// Food-level meal intent. Allocation is recalculated for preview and commit, never stored by UI.
using System;
using System.Collections.Generic;
using System.Linq;
[Serializable]
public class MealSlotSelection
{
    public int slotIndex; // Recipe slot index; -1 means extra ingredients.
    public string foodId;
    public int servings = 1;
}
public static class MealAllocation
{
    public static bool Usable(InventoryEntry e) => e != null && e.servings > 0 && (e.daysRemaining > 0 || e.daysRemaining == -1);
    public static string SlotWarning(RecipeData recipe, int index, IEnumerable<MealSlotSelection> selections, FoodDatabase foods)
    {
        var items=selections.Where(s=>s.slotIndex==index).ToList();
        long count=items.Sum(s=>(long)s.servings);
        if(index==-1)return count==0?null:"Extra ingredients do not belong to this recipe.";
        var slot=recipe.slots[index];
        if(slot.optional && count==0)return null;
        if(items.Any(s=>{var food=foods.GetById(s.foodId);return food==null||!food.Fits(slot.category);}))return $"{IntroMealPlan.CategoryName(slot.category)}: has a food from another category.";
        return count==slot.servings?null:$"{IntroMealPlan.CategoryName(slot.category)}: needs exactly {slot.servings} serving(s); selected {count}.";
    }
    public static bool TryResolve(GameSessionState state, RecipeData recipe, List<MealSlotSelection> selection,
        out List<IngredientSelection> batches, out string error)
    {
        batches=new List<IngredientSelection>();error=null;
        if(selection==null || selection.Count==0){error="Select ingredients first.";return false;}
        if(selection.Any(s=>s==null || string.IsNullOrWhiteSpace(s.foodId) || s.servings<=0 || s.slotIndex < -1 || s.slotIndex >= recipe.slots.Length))
        {error="Invalid food or recipe slot selection.";return false;}
        foreach(var group in selection.GroupBy(s=>s.foodId,StringComparer.OrdinalIgnoreCase))
        {
            long remaining=group.Sum(s=>(long)s.servings);
            foreach(var entry in state.inventory.Where(e=>Usable(e)&&string.Equals(e.foodId,group.Key,StringComparison.OrdinalIgnoreCase))
                .OrderBy(e=>e.daysRemaining==-1?int.MaxValue:e.daysRemaining).ThenBy(e=>e.inventoryEntryId,StringComparer.Ordinal))
            {
                int take=(int)Math.Min(remaining,entry.servings);
                if(take>0)batches.Add(new IngredientSelection{inventoryEntryId=entry.inventoryEntryId,servingsUsed=take});
                remaining-=take;if(remaining==0)break;
            }
            if(remaining>0){error=$"Insufficient usable servings of {group.Key}.";batches.Clear();return false;}
        }
        return true;
    }
}
