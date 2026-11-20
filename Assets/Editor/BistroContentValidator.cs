using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only structural checks for the standard imported content library.
// Draft approval status and optional art/audio are allowed in this Week 3 greybox.
// This validates playable data, not scientific claims or a production release gate.
public static class BistroContentValidator
{
    [MenuItem("Tools/Blue Zone Bistro/Validate Imported Content")]
    public static void ValidateMenu() => Validate();

    public static bool Validate()
    {
        var errors = new List<string>();
        var foods = Load<FoodDatabase>("Foods/FoodDatabase", errors);
        var listings = Load<StoreListingDatabase>("Prices/StoreListingDatabase", errors);
        var recipes = Load<RecipeDatabase>("Recipes/RecipeDatabase", errors);
        var customers = Load<CustomerDatabase>("Customers/CustomerDatabase", errors);
        var dialogues = Load<DialogueDatabase>("Dialogues/DialogueDatabase", errors);
        var balance = Load<BalanceConfig>("BalanceConfig", errors);
        if (errors.Count > 0) return Report(errors);

        CheckIds(foods.Foods, f => f.id, "food", errors);
        CheckIds(listings.Listings, l => l.id, "listing", errors);
        CheckIds(recipes.Recipes, r => r.id, "recipe", errors);
        CheckIds(customers.Customers, c => c.id, "customer", errors);
        CheckIds(dialogues.Dialogues, d => d.id, "dialogue", errors);
        foreach (var food in foods.Foods.Where(f => f != null))
        {
            if (food.recipeCategory == FoodCategory.Unassigned || !Enum.IsDefined(typeof(FoodCategory), food.recipeCategory) ||
                food.trafficLight == TrafficLight.Unassigned || !Enum.IsDefined(typeof(TrafficLight), food.trafficLight))
                errors.Add($"Food {food.id}: invalid category or Traffic Light.");
            if ((food.alsoCategories ?? Array.Empty<FoodCategory>()).Any(c => c == FoodCategory.Unassigned || !Enum.IsDefined(typeof(FoodCategory), c)))
                errors.Add($"Food {food.id}: invalid also_categories value.");
            int shelfLife = balance.GetShelfLife(food.storageType);
            if (shelfLife == 0 || shelfLife < -1)
                errors.Add($"Food {food.id}: storage type or shelf-life tuning prevents purchase.");
            if (!listings.Listings.Any(l => l != null && l.food == food && l.store != null))
                errors.Add($"Food {food.id}: no store listing.");
        }
        foreach (var listing in listings.Listings.Where(l => l != null))
        {
            if (listing.food == null || !foods.Foods.Contains(listing.food) || listing.store == null ||
                !FiniteNonnegative(listing.gamePrice) || listing.servingsPerPackage <= 0 || listing.unlockDay < 1)
                errors.Add($"Listing {listing.id}: invalid references, price, servings, or unlock day.");
        }
        foreach (var recipe in recipes.Recipes.Where(r => r != null))
            if (!RecipeRequirements.TryGetBounds(recipe, out _, out _) ||
                !FiniteNonnegative(recipe.baseSalePrice) || recipe.delightValue < 0 || recipe.unlockDay < 1)
                errors.Add($"Recipe {recipe.id}: invalid slots, payment, delight, or unlock day.");
        foreach (var customer in customers.Customers.Where(c => c != null))
        {
            if (customer.startingHealth < 0 || customer.startingHealth > 100 || customer.unlockDay < 1)
                errors.Add($"Customer {customer.id}: invalid starting health or unlock day.");
            if ((customer.preferredRecipes ?? Array.Empty<RecipeData>()).Any(r => r == null || !recipes.Recipes.Contains(r)))
                errors.Add($"Customer {customer.id}: preferred recipe missing from database.");
        }
        foreach (var dialogue in dialogues.Dialogues.Where(d => d != null))
        {
            if (dialogue.customer != null && !customers.Customers.Contains(dialogue.customer))
                errors.Add($"Dialogue {dialogue.id}: customer missing from database.");
            if (dialogue.requiredRecipe != null && !recipes.Recipes.Contains(dialogue.requiredRecipe))
                errors.Add($"Dialogue {dialogue.id}: recipe missing from database.");
            var lines = dialogue.lines ?? Array.Empty<DialogueLineData>();
            if (lines.Length == 0 || lines.Any(l => l == null || string.IsNullOrWhiteSpace(l.textTemplate)) ||
                lines.Where(l => l != null).GroupBy(l => l.lineOrder).Any(g => g.Count() > 1))
                errors.Add($"Dialogue {dialogue.id}: blank lines or duplicate line order.");
            if (!RangeValid(dialogue.minDay, dialogue.maxDay) ||
                !RangeValid(dialogue.minCustomerHealth, dialogue.maxCustomerHealth) ||
                !RangeValid(dialogue.minCustomerDelight, dialogue.maxCustomerDelight) ||
                !RangeValid(dialogue.minServedMealDelight, dialogue.maxServedMealDelight))
                errors.Add($"Dialogue {dialogue.id}: inverted condition range.");
        }
        CheckFrameworkWording(dialogues);
        ReportMissingArt(foods, recipes);
        return Report(errors);
    }

    private static bool FiniteNonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    private static bool RangeValid(int minimum, int maximum) => minimum < 0 || maximum < 0 || minimum <= maximum;
    private static T Load<T>(string relativePath, List<string> errors) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>("Assets/GameData/" + relativePath + ".asset");
        if (value == null) errors.Add($"Missing {relativePath}. Run Tools > Blue Zone Bistro > Import All CSV Content.");
        return value;
    }
    private static void CheckIds<T>(IEnumerable<T> values, Func<T, string> id, string label, List<string> errors)
        where T : UnityEngine.Object
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool any = false;
        foreach (var value in values)
        {
            any = true;
            if (value == null || !CsvImportUtility.IsSafeId(id(value)) || !seen.Add(id(value)))
                errors.Add($"Invalid, missing, or duplicated {label} ID.");
        }
        if (!any) errors.Add($"The {label} database is empty.");
    }
    // Nutrition framework (instructor plan §2): player text must not present oils or animal foods as healthy,
    // or any food as "fine in moderation". Food marketing labels are deliberate traps and are not checked here;
    // a phrase directly inside quotation marks is treated as quoting a label, not endorsing it.
    // Warning only (Owen, 2026-10-28): the supervisor reviews wording by playing; this just points at candidates.
    private static readonly string[] ForbiddenWording =
        { "moderation", "heart-healthy", "heart healthy", "lean protein", "healthy fat", "good fat" };
    private static void CheckFrameworkWording(DialogueDatabase dialogues)
    {
        void Scan(string where, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string phrase in ForbiddenWording)
                for (int at = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase); at >= 0;
                     at = text.IndexOf(phrase, at + 1, StringComparison.OrdinalIgnoreCase))
                {
                    bool quoted = at > 0 && "\"'\u201C\u2018".IndexOf(text[at - 1]) >= 0;
                    if (!quoted) { Debug.LogWarning($"{where}: uses \"{phrase}\", which the nutrition framework avoids. Consider rephrasing, or quote it as a marketing label."); break; }
                }
        }
        foreach (var sequence in dialogues.Dialogues.Where(d => d != null))
            foreach (var line in sequence.lines ?? Array.Empty<DialogueLineData>())
                if (line != null) Scan("Dialogue " + sequence.id, line.textTemplate);
        var tutorials = AssetDatabase.LoadAssetAtPath<TutorialDatabase>(TutorialCsvImporter.AssetPath);
        if (tutorials != null)
            foreach (var tutorial in tutorials.tutorials.Where(t => t != null))
                foreach (var step in tutorial.steps.Where(s => s != null)) Scan($"Tutorial {tutorial.id}/{step.stepId}", step.text);
        var events = AssetDatabase.LoadAssetAtPath<EventDatabase>(EventCsvImporter.AssetPath);
        if (events != null)
            foreach (var e in events.events.Where(x => x != null))
            { Scan("Event " + e.id, e.headline); Scan("Event " + e.id, e.summary); Scan("Event " + e.id, e.details); }
    }

    // Missing art is allowed (cards fall back to plain text), but list it so gaps are easy to find.
    private static void ReportMissingArt(FoodDatabase foods, RecipeDatabase recipes)
    {
        var missing = new List<string>();
        missing.AddRange(foods.Foods.Where(f => f != null && f.icon == null).Select(f => "food " + f.id + " (icon_path)"));
        missing.AddRange(recipes.Recipes.Where(r => r != null && r.icon == null).Select(r => "recipe " + r.id + " (icon_path)"));
        missing.AddRange(recipes.Recipes.Where(r => r != null && r.slots != null).SelectMany(r => r.slots)
            .Where(s => s != null).Select(s => s.category).Distinct()
            .Where(c => foods.CategoryIcon(c) == null).Select(c => "category " + c + " (Categories.csv)"));
        if (missing.Count > 0)
            Debug.LogWarning("Art not assigned, plain cards will be shown for: " + string.Join(", ", missing) +
                ". Fill the path in the CSV or drop a sprite on the asset, then Import All CSV Content.");
    }

    private static bool Report(List<string> errors)
    {
        foreach (string error in errors) Debug.LogError("Bistro content: " + error);
        if (errors.Count == 0) Debug.Log("Bistro content structure passed. Draft approval and optional media still require separate review.");
        return errors.Count == 0;
    }
}
