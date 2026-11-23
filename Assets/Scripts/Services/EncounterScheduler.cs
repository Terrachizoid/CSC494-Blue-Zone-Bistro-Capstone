// File responsibility: Builds a priority-ordered story queue followed by attending customers.
// Dialogue contexts read the shared session and the actual committed dish outcome.

using System;
using System.Collections.Generic;
using System.Linq;

public class EncounterScheduler
{
    // Only select authored conversations. A missing special trigger has no fabricated fallback.
    public DialogueTrigger CustomerTrigger(DialogueTrigger requested, string customerId, ServeResult result)
    {
        var progress = state.customers.Find(c => string.Equals(c.customerId, customerId, StringComparison.OrdinalIgnoreCase));
        if (requested == DialogueTrigger.CustomerArrival && progress != null && progress.crashDialoguePending)
            return DialogueTrigger.CrashArrival;
        // The rush (red food served) is no longer a separate trigger: it is a rung of the AfterService ladder
        // (`red_served`, priorities 195/196), so a mismatched plate still says so and every rung can add a Guide tip.
        return requested;
    }
    public void OnDialogueCompleted(DialogueSequenceData sequence, string customerId)
    {
        if (sequence == null || sequence.trigger != DialogueTrigger.CrashArrival) return;
        var progress = state.customers.Find(c => string.Equals(c.customerId, customerId, StringComparison.OrdinalIgnoreCase));
        if (progress != null) RushCrashProgress.CompleteCrash(progress, state.flags);
    }
    private readonly GameSessionState state;
    private readonly DialogueDatabase dialogues;
    private readonly CustomerDatabase customers;
    private readonly RecipeDatabase recipes;
    public DialogueService Dialogue { get; }
    private readonly float appealTarget;
    // appealTarget: the appeal a dish needs to count as appealing in dialogue (BalanceConfig.comfortMinimumAppeal).
    public EncounterScheduler(GameSessionState state, DialogueDatabase dialogues,
        CustomerDatabase customers, RecipeDatabase recipes, float appealTarget = 7f)
    {
        this.appealTarget = appealTarget;
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
            servedMealHealthDelta = result?.healthDelta ?? 0,
            requestCount = result == null ? -1 : (result.activeCravings?.Length ?? 0),
            requestsMet = result == null ? -1 : (result.cravingsResolved?.Length ?? 0),
            metRequests = JoinTags(result?.cravingsResolved),
            missedRequests = JoinTags(Missed(result)), missedRequestTags = Missed(result),
            recipeFatigued = result != null && result.recipeFatigued, recipeRecentServes = result?.recipeRecentServes ?? 0,
            ingredientAppeal = result?.ingredientAppeal ?? -1f, appealTarget = appealTarget,
            // Same test as the dish result's "(guest was already fully delighted)".
            delightFull = result != null && result.recipeBuilt && result.delightDelta == 0 && result.dishDelight > 0,
            redServed = result != null && result.redServings > 0 };
    }
    private static CravingTag[] Missed(ServeResult result) => result?.activeCravings == null ? Array.Empty<CravingTag>()
        : result.activeCravings.Except(result.cravingsResolved ?? Array.Empty<CravingTag>()).ToArray();
    // "Crunchy", "Crunchy and Sweet", "Crunchy, Sweet and Savory": the same words the guest card shows.
    private static string JoinTags(CravingTag[] tags)
    {
        if (tags == null || tags.Length == 0) return string.Empty;
        var names = tags.Select(t => t.ToString()).ToArray();
        return names.Length == 1 ? names[0] : string.Join(", ", names.Take(names.Length - 1)) + " and " + names[names.Length - 1];
    }
}

