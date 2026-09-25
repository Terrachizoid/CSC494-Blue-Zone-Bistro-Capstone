// File responsibility: Standalone dialogue demonstration for isolated Inspector testing; not the live game entry point.
// GameBootstrap uses EncounterScheduler with GameSessionState instead of this example state.

using System.Collections.Generic;
using UnityEngine;

// Small integration example. Your UI can ask this component for a sequence,
// render its lines, and call Complete after the player closes the sequence.
public class DialogueRuntimeExample : MonoBehaviour
{
    [SerializeField]
    private DialogueDatabase dialogueDatabase;

    [SerializeField]
    private GameProgressState progress = new GameProgressState();

    [SerializeField]
    private bool approvedOnly;

    private DialogueService service;

    public DialogueSequenceData FindCustomerArrival(
        CustomerData customer,
        int currentHealth,
        int currentDelight)
    {
        DialogueRuntimeContext context = CreateCustomerContext(
            customer,
            currentHealth,
            currentDelight);

        return GetService().FindNext(
            DialogueTrigger.CustomerArrival,
            context);
    }

    public DialogueSequenceData FindAfterService(
        CustomerData customer,
        int currentHealth,
        int currentDelight,
        RecipeData servedRecipe,
        int servedMealDelight)
    {
        DialogueRuntimeContext context = CreateAfterServiceContext(
            customer,
            currentHealth,
            currentDelight,
            servedRecipe,
            servedMealDelight);

        return GetService().FindNext(
            DialogueTrigger.AfterService,
            context);
    }

    public DialogueSequenceData FindDayStart()
    {
        DialogueRuntimeContext context = new DialogueRuntimeContext
        {
            currentDay = progress.currentDay
        };

        return GetService().FindNext(DialogueTrigger.DayStart, context);
    }

    public List<string> ResolveLines(
        DialogueSequenceData sequence,
        DialogueRuntimeContext context)
    {
        List<string> resolved = new List<string>();

        if (sequence == null)
        {
            return resolved;
        }

        foreach (DialogueLineData line in sequence.lines)
        {
            resolved.Add(GetService().ResolveText(line, context));
        }

        return resolved;
    }

    public void Complete(DialogueSequenceData sequence)
    {
        GetService().Complete(sequence, progress.currentDay);
    }

    public bool HasProgressFlag(string flagId)
    {
        return progress.flags.HasFlag(flagId);
    }

    public DialogueRuntimeContext CreateCustomerContext(
        CustomerData customer,
        int currentHealth,
        int currentDelight)
    {
        return new DialogueRuntimeContext
        {
            currentDay = progress.currentDay,
            customerId = customer != null ? customer.id : string.Empty,
            customerName = customer != null
                ? customer.displayName
                : string.Empty,
            customerHealth = currentHealth,
            customerDelight = currentDelight
        };
    }

    public DialogueRuntimeContext CreateAfterServiceContext(
        CustomerData customer,
        int currentHealth,
        int currentDelight,
        RecipeData servedRecipe,
        int servedMealDelight)
    {
        DialogueRuntimeContext context = CreateCustomerContext(
            customer,
            currentHealth,
            currentDelight);

        // These values describe this service interaction only.
        context.servedRecipe = servedRecipe;
        context.servedMealDelight = servedMealDelight;
        return context;
    }

    private DialogueService GetService()
    {
        if (service == null)
        {
            service = new DialogueService(
                dialogueDatabase,
                progress.flags,
                progress.dialogueHistory,
                approvedOnly);
        }

        return service;
    }
}
