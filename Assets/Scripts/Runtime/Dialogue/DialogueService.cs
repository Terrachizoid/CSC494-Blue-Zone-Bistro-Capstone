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

        List<DialogueSequenceData> mandatory = eligible
            .Where(sequence => sequence.mode == DialogueMode.Mandatory)
            .OrderByDescending(sequence => sequence.priority)
            .ThenBy(sequence => sequence.id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mandatory.Count > 0)
        {
            // Variants (2026-11-22): mandatory dialogues of the same variant group (variant_of) that tie on the top
            // priority are versions of one moment. One is picked by random_weight, skipping the one played most
            // recently so a guest never repeats the same lines twice in a row. Ties outside that group never mix in:
            // the first by id wins, as before (the importer and the outcome test flag such ties).
            string group = mandatory[0].VariantGroup;
            List<DialogueSequenceData> tied = mandatory.Where(sequence => sequence.priority == mandatory[0].priority &&
                string.Equals(sequence.VariantGroup, group, StringComparison.OrdinalIgnoreCase)).ToList();
            if (tied.Count == 1) return tied[0];
            int latest = tied.Max(sequence => history.LastPlayedDay(sequence.id));
            if (latest >= 0)
            {
                List<DialogueSequenceData> fresh = tied.Where(sequence => history.LastPlayedDay(sequence.id) < latest).ToList();
                if (fresh.Count > 0) tied = fresh;
            }
            return SelectWeightedAmbient(tied);
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
        // Special reactions must belong to this exact character; never borrow another character's lines.
        if ((trigger == DialogueTrigger.RushAfterService || trigger == DialogueTrigger.CrashArrival) &&
            (sequence.customer == null || !string.Equals(sequence.customer.id, context.customerId, StringComparison.OrdinalIgnoreCase)))
            return false;

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

        if (!MatchesRequests(sequence.requestCondition, context) || !MatchesDish(sequence, context))
        {
            return false;
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

    private static bool MatchesRequests(RequestCondition condition, DialogueRuntimeContext context)
    {
        if (condition == RequestCondition.Any) return true;
        if (!context.hasServedMeal || context.requestCount < 0) return false;
        int asked = context.requestCount, met = Math.Max(0, context.requestsMet);
        switch (condition)
        {
            case RequestCondition.NoRequest: return asked == 0;
            case RequestCondition.All: return asked > 0 && met >= asked;
            case RequestCondition.Some: return asked > 0 && met > 0 && met < asked;
            case RequestCondition.None: return asked > 0 && met == 0;
            case RequestCondition.Missed: return asked > 0 && met < asked;
            default: return true;
        }
    }

    // The served-dish switches (2026-11-22). Each one unused (Any/empty) always matches.
    private static bool MatchesDish(DialogueSequenceData sequence, DialogueRuntimeContext context)
    {
        bool usesDish = (sequence.missedRequests != null && sequence.missedRequests.Length > 0) ||
            sequence.recipeFatigued != DialogueSwitch.Any || sequence.minIngredientAppeal >= 0 || sequence.maxIngredientAppeal >= 0 ||
            sequence.delightFull != DialogueSwitch.Any || sequence.redServed != DialogueSwitch.Any;
        if (!usesDish) return true;
        if (!context.hasServedMeal) return false;
        if (sequence.missedRequests != null && sequence.missedRequests.Length > 0 &&
            !sequence.missedRequests.Any(tag => context.missedRequestTags != null && context.missedRequestTags.Contains(tag)))
            return false;
        if (!Switch(sequence.recipeFatigued, context.recipeFatigued) || !Switch(sequence.delightFull, context.delightFull) ||
            !Switch(sequence.redServed, context.redServed)) return false;
        if (sequence.minIngredientAppeal >= 0 || sequence.maxIngredientAppeal >= 0)
        {
            float appeal = context.ingredientAppeal;
            if (appeal < 0 || (sequence.minIngredientAppeal >= 0 && appeal < sequence.minIngredientAppeal) ||
                (sequence.maxIngredientAppeal >= 0 && appeal > sequence.maxIngredientAppeal)) return false;
        }
        return true;
    }

    private static bool Switch(DialogueSwitch wanted, bool actual) =>
        wanted == DialogueSwitch.Any || actual == (wanted == DialogueSwitch.Yes);

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

            case "metrequest":
                value = context.metRequests ?? string.Empty;
                return true;

            case "missedrequest":
                value = context.missedRequests ?? string.Empty;
                return true;

            case "requestsmet":
                value = Math.Max(0, context.requestsMet).ToString();
                return true;

            case "ingredientappeal":
                value = context.ingredientAppeal < 0 ? string.Empty : context.ingredientAppeal.ToString("0.0");
                return true;

            case "appealtarget":
                value = context.appealTarget.ToString("0.#");
                return true;

            case "reciperecentserves":
                value = context.recipeRecentServes.ToString();
                return true;

            case "requestcount":
                value = Math.Max(0, context.requestCount).ToString();
                return true;

            case "servedmealdelight":
                value = context.servedMealDelight.ToString();
                return true;

            default:
                return false;
        }
    }
}
