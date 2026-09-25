// File responsibility: Builds a priority-ordered story queue followed by attending customers.
// Dialogue contexts read the shared session and the actual committed dish outcome.

using System;
using System.Collections.Generic;
using System.Linq;

public class EncounterScheduler
{
    private readonly GameSessionState state;
    private readonly DialogueDatabase dialogues;
    private readonly CustomerDatabase customers;
    private readonly RecipeDatabase recipes;
    public DialogueService Dialogue { get; }
    public EncounterScheduler(GameSessionState state, DialogueDatabase dialogues,
        CustomerDatabase customers, RecipeDatabase recipes)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.dialogues = dialogues ?? throw new ArgumentNullException(nameof(dialogues));
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        Dialogue = new DialogueService(dialogues, state.flags, state.dialogueHistory);
    }
    public List<EncounterEntry> Build(List<string> customerIds)
    {
        var queue = new List<EncounterEntry>();
        // Story encounters do not depend on the customer attendance roll.
        foreach (var sequence in dialogues.Dialogues.Where(s => s != null &&
            Dialogue.IsEligible(s, DialogueTrigger.ServiceEncounter, Context(s.customer?.id)))
            .OrderByDescending(s => s.priority).ThenBy(s => s.id, StringComparer.OrdinalIgnoreCase))
            queue.Add(new EncounterEntry { kind = EncounterKind.Story, dialogue = sequence });
        foreach (string id in customerIds) queue.Add(new EncounterEntry { kind = EncounterKind.Customer, customerId = id });
        return queue;
    }
    public DialogueRuntimeContext Context(string customerId, string recipeId = null, ServeResult result = null)
    {
        var data = customers.GetById(customerId);
        var progress = state.customers.Find(c => string.Equals(c.customerId, customerId, StringComparison.OrdinalIgnoreCase));
        return new DialogueRuntimeContext { currentDay = state.currentDay, customerId = customerId,
            customerName = data?.displayName ?? "", customerHealth = progress?.health ?? data?.startingHealth ?? -1,
            customerDelight = progress?.delight ?? -1, servedRecipe = recipes.GetById(recipeId),
            servedMealDelight = result?.delightDelta ?? -1, hasServedMeal = result != null,
            servedRecipeBuilt = result != null && result.recipeBuilt,
            servedMealHealthDelta = result?.healthDelta ?? 0 };
    }
}
