// Closing-dialogue outcome table (2026-11-22). For every guest and every combination of dish situations (plate
// matched, health change, red food, fancy recipe, tired recipe, requests, appeal, delight full, dish delight), runs
// the real DialogueService and checks that exactly one planned dialogue wins: never nothing, never a tie between
// dialogues that are not variants of each other (variant_of). Writes the whole table to a CSV for people to scan.
// Called from Test-CsvContent.ps1 with the dialogue loaded from the CSVs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class DialogueOutcomeChecks
{
    private struct Requests { public string label; public int asked, met; public CravingTag[] missed; }

    public static string Run(DialogueService selector, DialogueDatabase database, IEnumerable<string> customerIds,
        RecipeData basic, RecipeData fancy, int day, string outCsv)
    {
        var requests = new[]
        {
            new Requests { label = "no request", asked = 0, met = 0, missed = new CravingTag[0] },
            new Requests { label = "all met", asked = 2, met = 2, missed = new CravingTag[0] },
            new Requests { label = "some met (missed Crunchy)", asked = 2, met = 1, missed = new[] { CravingTag.Crunchy } },
            new Requests { label = "none met (missed Crunchy)", asked = 1, met = 0, missed = new[] { CravingTag.Crunchy } },
            new Requests { label = "some met (missed Comfort)", asked = 2, met = 1, missed = new[] { CravingTag.Comfort } },
            new Requests { label = "none met (missed Comfort)", asked = 1, met = 0, missed = new[] { CravingTag.Comfort } },
        };
        var csv = new StringBuilder("guest,plate,health,red food,fancy,recipe,requests,appeal,delight,dish delight,plays\n");
        var problems = new List<string>();
        int rows = 0;
        var flags = new[] { false, true };
        foreach (string customer in customerIds)
        foreach (bool built in flags)
        foreach (int health in new[] { 5, -5, 0 })
        foreach (bool red in flags)
        foreach (bool isFancy in flags)
        foreach (bool tired in flags)
        foreach (var req in requests)
        foreach (float appeal in new[] { 3f, 6f, 8f })
        foreach (bool full in flags)
        foreach (int dishDelight in new[] { 0, 3, 9 })
        {
            if (!built && req.met > 0 && req.met < req.asked) continue; // a mismatched plate meets no request
            if (full && (dishDelight != 0 || !built)) continue;           // "already full" means the dish added 0
            int met = built ? req.met : 0;
            var missed = built ? req.missed : (req.asked > 0 && req.missed.Length == 0 ? new[] { CravingTag.Crunchy } : req.missed);
            var context = new DialogueRuntimeContext
            {
                currentDay = day, customerId = customer, hasServedMeal = true, servedRecipeBuilt = built,
                servedMealHealthDelta = health, servedMealDelight = dishDelight, servedRecipe = isFancy ? fancy : basic,
                requestCount = req.asked, requestsMet = met, missedRequestTags = missed,
                missedRequests = string.Join(" and ", missed.Select(t => t.ToString())),
                recipeFatigued = tired, ingredientAppeal = appeal, delightFull = full, redServed = red
            };
            var eligible = database.Dialogues.Where(s => s != null && s.mode == DialogueMode.Mandatory &&
                selector.IsEligible(s, DialogueTrigger.AfterService, context)).ToList();
            string plays;
            if (eligible.Count == 0)
            {
                var ambient = selector.FindNext(DialogueTrigger.AfterService, context);
                plays = ambient != null ? "(ambient) " + ambient.id : "(nothing)";
                if (ambient == null) problems.Add($"nothing plays: {customer} {Describe(built, health, red, isFancy, tired, req.label, appeal, full, dishDelight)}");
            }
            else
            {
                int top = eligible.Max(s => s.priority);
                var tied = eligible.Where(s => s.priority == top).ToList();
                if (tied.Select(s => s.VariantGroup).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                    problems.Add($"unplanned tie {string.Join("/", tied.Select(s => s.id))}: {customer} {Describe(built, health, red, isFancy, tired, req.label, appeal, full, dishDelight)}");
                plays = string.Join(" / ", tied.Select(s => s.id).OrderBy(id => id, StringComparer.Ordinal));
            }
            csv.Append(customer).Append(',').Append(built ? "matched" : "NOT matched").Append(',')
                .Append(health > 0 ? "up" : health < 0 ? "down" : "same").Append(',').Append(red ? "red" : "").Append(',')
                .Append(isFancy ? "fancy" : "").Append(',').Append(tired ? "tired" : "").Append(',').Append(req.label).Append(',')
                .Append(appeal).Append(',').Append(full ? "full" : "").Append(',').Append(dishDelight).Append(',').Append(plays).Append('\n');
            rows++;
        }
        if (!string.IsNullOrEmpty(outCsv)) File.WriteAllText(outCsv, csv.ToString());
        if (problems.Count > 0)
            throw new Exception($"Closing dialogue outcome table: {problems.Count} problem(s). First: {problems[0]}");
        return $"PASS: {rows} closing-dialogue situations each have exactly one planned winner (table: {outCsv}).";
    }

    private static string Describe(bool built, int health, bool red, bool fancy, bool tired, string requests, float appeal, bool full, int dishDelight) =>
        $"[{(built ? "matched" : "not matched")}, health {health}, red {red}, fancy {fancy}, tired {tired}, {requests}, appeal {appeal}, full {full}, dish delight {dishDelight}]";
}
