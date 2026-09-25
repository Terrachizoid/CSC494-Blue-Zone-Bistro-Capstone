// File responsibility: Selects eligible conversations, resolves text tokens, and applies completion flags/history.
// It shares session flags/history; contexts provide current gameplay values and actual dish deltas.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class DialogueService
{
    private static readonly Regex TokenPattern =
        new Regex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    private readonly DialogueDatabase database;
    private readonly GameFlagState flags;
    private readonly DialogueHistoryState history;
    private readonly bool approvedOnly;

    public DialogueService(
        DialogueDatabase database,
        GameFlagState flags,
        DialogueHistoryState history,
        bool approvedOnly = false)
    {
        this.database = database;
        this.flags = flags ?? throw new ArgumentNullException(nameof(flags));
        this.history = history ??
            throw new ArgumentNullException(nameof(history));
        this.approvedOnly = approvedOnly;
    }

    public DialogueSequenceData FindNext(
        DialogueTrigger trigger,
        DialogueRuntimeContext context)
    {
        if (database == null || context == null)
        {
            return null;
        }

        List<DialogueSequenceData> eligible = database.Dialogues
            .Where(sequence => IsEligible(sequence, trigger, context))
            .ToList();

        DialogueSequenceData mandatory = eligible
            .Where(sequence => sequence.mode == DialogueMode.Mandatory)
            .OrderByDescending(sequence => sequence.priority)
            .ThenBy(sequence => sequence.id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (mandatory != null)
        {
            return mandatory;
        }

        return SelectWeightedAmbient(
            eligible.Where(sequence => sequence.mode == DialogueMode.Ambient));
    }

    public bool IsEligible(
        DialogueSequenceData sequence,
        DialogueTrigger trigger,
        DialogueRuntimeContext context)
    {
        if (sequence == null || context == null || sequence.trigger != trigger ||
            sequence.lines == null || !sequence.lines.Any(line => line != null && !string.IsNullOrWhiteSpace(line.textTemplate)))
        {
            return false;
        }

        if (approvedOnly &&
            sequence.approvalStatus != ApprovalStatus.Approved)
        {
            return false;
        }
        if (sequence.recipeBuildCondition != RecipeBuildCondition.Any &&
            (!context.hasServedMeal || context.servedRecipeBuilt != (sequence.recipeBuildCondition == RecipeBuildCondition.Built)))
            return false;
        if (sequence.mealHealthCondition != MealHealthCondition.Any)
        {
            if (!context.hasServedMeal) return false;
            if (sequence.mealHealthCondition == MealHealthCondition.Gain && context.servedMealHealthDelta <= 0) return false;
            if (sequence.mealHealthCondition == MealHealthCondition.Loss && context.servedMealHealthDelta >= 0) return false;
            if (sequence.mealHealthCondition == MealHealthCondition.Unchanged && context.servedMealHealthDelta != 0) return false;
        }

        if (!history.CanPlay(sequence, context.currentDay))
        {
            return false;
        }

        if (context.currentDay < sequence.minDay ||
            (sequence.maxDay >= 0 && context.currentDay > sequence.maxDay))
        {
            return false;
        }

        if (sequence.customer != null &&
            !string.Equals(
                sequence.customer.id,
                context.customerId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!MatchesOptionalRange(
                context.customerHealth,
                sequence.minCustomerHealth,
                sequence.maxCustomerHealth) ||
            !MatchesOptionalRange(
                context.customerDelight,
                sequence.minCustomerDelight,
                sequence.maxCustomerDelight) ||
            !MatchesOptionalRange(
                context.servedMealDelight,
                sequence.minServedMealDelight,
                sequence.maxServedMealDelight))
        {
            return false;
        }

        if (sequence.requiredRecipe != null)
        {
            if (context.servedRecipe == null ||
                !string.Equals(
                    sequence.requiredRecipe.id,
                    context.servedRecipe.id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (sequence.requiredRecipeTiers != null &&
            sequence.requiredRecipeTiers.Length > 0)
        {
            if (context.servedRecipe == null ||
                !sequence.requiredRecipeTiers.Contains(
                    context.servedRecipe.tier))
            {
                return false;
            }
        }

        return flags.HasAll(sequence.requiredFlags) &&
               !flags.HasAny(sequence.forbiddenFlags);
    }

    public void Complete(
        DialogueSequenceData sequence,
        int currentDay)
    {
        if (sequence == null)
        {
            return;
        }

        foreach (string flagId in sequence.clearFlags ?? Array.Empty<string>())
        {
            flags.ClearFlag(flagId);
        }

        foreach (string flagId in sequence.setFlags ?? Array.Empty<string>())
        {
            flags.SetFlag(flagId);
        }

        history.MarkPlayed(sequence, currentDay);
    }

    public string ResolveText(
        DialogueLineData line,
        DialogueRuntimeContext context)
    {
        if (line == null)
        {
            return string.Empty;
        }

        string template = line.textTemplate ?? string.Empty;

        return TokenPattern.Replace(template, match =>
        {
            string token = match.Groups[1].Value;

            if (TryResolveBuiltInToken(token, context, out string value) ||
                (context != null && context.TryGetToken(token, out value)))
            {
                return value;
            }

            Debug.LogWarning(
                $"Dialogue token '{{{token}}}' has no runtime value.");
            return match.Value;
        });
    }

    private static bool MatchesOptionalRange(
        int actual,
        int minimum,
        int maximum)
    {
        bool hasCondition = minimum >= 0 || maximum >= 0;

        if (!hasCondition)
        {
            return true;
        }

        if (actual < 0)
        {
            return false;
        }

        return (minimum < 0 || actual >= minimum) &&
               (maximum < 0 || actual <= maximum);
    }

    private static DialogueSequenceData SelectWeightedAmbient(
        IEnumerable<DialogueSequenceData> candidates)
    {
        List<DialogueSequenceData> items = candidates.ToList();

        if (items.Count == 0)
        {
            return null;
        }

        float totalWeight = items.Sum(item => Mathf.Max(0f, item.randomWeight));

        if (totalWeight <= 0f)
        {
            return items[0];
        }

        float selection = UnityEngine.Random.Range(0f, totalWeight);

        foreach (DialogueSequenceData item in items)
        {
            selection -= Mathf.Max(0f, item.randomWeight);

            if (selection <= 0f)
            {
                return item;
            }
        }

        return items[items.Count - 1];
    }

    private static bool TryResolveBuiltInToken(
        string token,
        DialogueRuntimeContext context,
        out string value)
    {
        value = string.Empty;

        if (context == null)
        {
            return false;
        }

        switch (token.ToLowerInvariant())
        {
            case "currentday":
                value = context.currentDay.ToString();
                return true;

            case "customername":
                value = context.customerName ?? string.Empty;
                return true;

            case "customerhealth":
                value = context.customerHealth.ToString();
                return true;

            case "customerdelight":
                value = context.customerDelight.ToString();
                return true;

            case "servedrecipe":
                value = context.servedRecipe != null
                    ? context.servedRecipe.displayName
                    : string.Empty;
                return true;

            case "servedmealdelight":
                value = context.servedMealDelight.ToString();
                return true;

            default:
                return false;
        }
    }
}
