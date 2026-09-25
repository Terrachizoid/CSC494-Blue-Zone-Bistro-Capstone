// File responsibility: Replaceable IMGUI presentation/controller for market, service, dialogue, and daily results.
// Buttons call GameLoop; labels query content/state. Keep economy and eligibility rules in services.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Built-in immediate-mode GUI for the playable greybox; no Canvas or input module wiring.
public class GreyboxUIController : MonoBehaviour
{
    private GameBootstrap game;
    private StoreData selectedStore;
    private RecipeData selectedRecipe;
    private readonly Dictionary<string, int> selected = new Dictionary<string, int>();
    private Vector2 scroll;
    private string message = "Choose a store to buy ingredients.";
    private ServeInput pendingMeal;
    private ServeResult preview;
    private AudioSource voiceSource;
    private DialogueLineData voicedLine;

    private void Update()
    {
        var line = game?.Loop?.Dialogue?.CurrentLine;
        if (ReferenceEquals(line, voicedLine)) return;
        voicedLine = line;
        if (voiceSource != null) voiceSource.Stop();
        if (line?.voiceOver == null) return;
        if (voiceSource == null) { voiceSource = gameObject.AddComponent<AudioSource>(); voiceSource.playOnAwake = false; }
        voiceSource.clip = line.voiceOver;
        voiceSource.Play();
    }
    private void OnDisable() { if (voiceSource != null) voiceSource.Stop(); voicedLine = null; }

    public void Initialize(GameBootstrap bootstrap)
    {
        if (game != null) game.Loop.PhaseChanged -= OnPhaseChanged;
        game = bootstrap;
        game.Loop.PhaseChanged += OnPhaseChanged;
    }
    private void OnDestroy()
    {
        if (game != null && game.Loop != null) game.Loop.PhaseChanged -= OnPhaseChanged;
    }
    private void OnPhaseChanged(GamePhase phase)
    {
        selectedStore = null;
        ResetSelection();
        scroll = Vector2.zero;
        message = "";
    }
    private void ResetSelection() { selectedRecipe = null; selected.Clear(); pendingMeal = null; preview = null; }

    private void OnGUI()
    {
        if (game == null || game.Loop == null) return;
        // Cover scene placeholders so they cannot obscure the text-only greybox panel.
        var previousColor = GUI.color;
        GUI.color = new Color(0.12f, 0.15f, 0.15f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUILayout.BeginArea(new Rect(10, 10, Mathf.Max(280, Screen.width - 20),
            Mathf.Max(200, Screen.height - 20)), GUI.skin.box);
        GUILayout.Label($"BLUE ZONE BISTRO — Day {game.State.currentDay} — {game.Loop.Phase} — ${game.State.money:0.00}");
        if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);
        scroll = GUILayout.BeginScrollView(scroll);
        switch (game.Loop.Phase)
        {
            case GamePhase.Market: DrawMarket(); break;
            case GamePhase.Service: DrawService(); break;
            case GamePhase.Results: DrawResults(false); break;
            case GamePhase.Completed: DrawResults(true); break;
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawMarket()
    {
        if (selectedStore == null)
        {
            GUILayout.Label("MARKET — Choose a store");
            var stores = game.Queries.GetStores();
            if (stores.Count == 0) GUILayout.Label("No stores available today.");
            foreach (var store in stores)
                if (GUILayout.Button(Label(store.displayName, store.id)))
                { selectedStore = store; message = ""; GUIUtility.ExitGUI(); }
        }
        else
        {
            GUILayout.Label(Label(selectedStore.displayName, selectedStore.id));
            if (GUILayout.Button("Back to stores")) { selectedStore = null; GUIUtility.ExitGUI(); }
            bool member = !selectedStore.requiresMembership || game.State.storeMembershipIds.Any(id =>
                string.Equals(id, selectedStore.id, System.StringComparison.OrdinalIgnoreCase));
            if (!member) GUILayout.Label("Membership required. Membership purchasing is not part of this greybox yet.");
            var listings = game.Queries.GetListings(selectedStore);
            if (listings.Count == 0) GUILayout.Label("No listings unlocked here today.");
            foreach (var listing in listings)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Label(listing.food.displayName, listing.food.id)} | {listing.food.trafficLight} | " +
                    $"{listing.food.recipeCategory} | {listing.servingsPerPackage} servings | ${listing.gamePrice:0.00}");
                GUI.enabled = member && game.State.money >= listing.gamePrice;
                if (GUILayout.Button("Buy", GUILayout.Width(90)))
                {
                    var result = game.Loop.Buy(listing.id);
                    message = result.success ? $"Bought {result.servingsAdded} servings. Shelf life: {ShelfLife(result.daysRemaining)}." : result.error;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(12);
        DrawInventory(false);
        if (GUILayout.Button("Continue to service"))
        { game.Loop.BeginService(); GUIUtility.ExitGUI(); }
    }

    private void DrawService()
    {
        var session = game.Loop.Service;
        if (game.Loop.Dialogue != null && game.Loop.Dialogue.IsActive)
        {
            DrawDialogue();
            return;
        }
        var customer = game.Queries.GetCustomer(session.CurrentCustomerId);
        var progress = game.State.customers.Find(c => string.Equals(c.customerId,
            session.CurrentCustomerId, System.StringComparison.OrdinalIgnoreCase));
        GUILayout.Label($"Encounter {session.encounterIndex + 1}/{session.encounters.Count}: " +
            (customer == null ? session.CurrentCustomerId : Label(customer.displayName, customer.id)));
        GUILayout.Label($"Health: {(progress != null ? progress.health : customer != null ? customer.startingHealth : 0)}");
        GUILayout.Label($"Delight: {(progress != null ? progress.delight : 50)} | Finance: {(progress != null ? progress.finance : 20)} (tip %)");
        if (session.awaitingDishAcknowledgement)
        {
            DrawDish(session.lastDishResult);
            if (GUILayout.Button("Continue to next customer / daily results"))
            { game.Loop.ContinueAfterDish(); ResetSelection(); GUIUtility.ExitGUI(); }
            return;
        }
        if (GUILayout.Button("Skip this customer"))
        { game.Loop.SkipCustomer(); ResetSelection(); message = ""; GUIUtility.ExitGUI(); }

        if (pendingMeal != null)
        {
            GUILayout.Label("CONFIRM DISH — nothing has been consumed yet");
            DrawDish(preview);
            if (!preview.recipeBuilt)
                GUILayout.Label("This dish does not build the recipe. No tip or resolution rewards; reduced payment uses minimum ingredient value.");
            if (GUILayout.Button("Back to reconsider"))
            { pendingMeal = null; preview = null; GUIUtility.ExitGUI(); }
            if (GUILayout.Button("Confirm and serve"))
            {
                // Revalidates stock and current customer rather than trusting the preview result.
                var result = game.Loop.Serve(pendingMeal);
                pendingMeal = null; preview = null;
                message = result.success ? "Meal served." : result.error;
                if (result.success) ResetSelection();
                GUIUtility.ExitGUI();
            }
            return;
        }

        if (selectedRecipe == null)
        {
            GUILayout.Label("Choose a recipe — substitutions are allowed with reduced payment");
            var recipes = game.Queries.GetUnlockedRecipes();
            if (recipes.Count == 0) GUILayout.Label("No unlocked recipes. You can skip this customer.");
            foreach (var recipe in recipes)
                if (GUILayout.Button($"{Label(recipe.displayName, recipe.id)} — base ${recipe.baseSalePrice:0.00} + tip — " +
                    (game.Queries.CanMake(recipe) ? "Can make" : "Substitution needed")))
                { selectedRecipe = recipe; selected.Clear(); message = ""; GUIUtility.ExitGUI(); }
        }
        else
        {
            GUILayout.Label("Recipe: " + Label(selectedRecipe.displayName, selectedRecipe.id));
            foreach (var slot in selectedRecipe.slots)
                GUILayout.Label($"{slot.category}: {slot.servings} servings ({(slot.optional ? "optional" : "required")})");
            GUILayout.Label(selectedRecipe.allowNonGreenChoices ? "All Traffic Light choices allowed." : "Green ingredients only.");
            if (GUILayout.Button("Back to recipe selection")) { ResetSelection(); GUIUtility.ExitGUI(); }
            DrawInventory(true);
            if (GUILayout.Button("Submit / Serve"))
            {
                var input = new ServeInput { customerId = session.CurrentCustomerId, recipeId = selectedRecipe.id };
                foreach (var pair in selected)
                    if (pair.Value > 0) input.ingredients.Add(new IngredientSelection
                        { inventoryEntryId = pair.Key, servingsUsed = pair.Value });
                var result = game.Loop.PreviewServe(input);
                message = result.success ? "Review the dish before confirming." : result.error;
                if (result.success) { pendingMeal = input; preview = result; }
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawDialogue()
    {
        var playback = game.Loop.Dialogue;
        var line = playback.CurrentLine;
        var speaker = game.Queries.GetCustomer(line.speakerId);
        GUILayout.Label(playback.Sequence.displayName);
        GUILayout.Label(speaker == null ? line.speakerId : Label(speaker.displayName, speaker.id));
        if (line.portrait != null)
        {
            var area = GUILayoutUtility.GetRect(96, 96, GUILayout.Width(96));
            var rect = line.portrait.textureRect;
            var texture = line.portrait.texture;
            GUI.DrawTextureWithTexCoords(area, texture, new Rect(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height));
        }
        GUILayout.Label(playback.CurrentText, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18 });
        GUILayout.Label($"Line {playback.LineIndex + 1} of {playback.LineCount}");
        if (GUILayout.Button(playback.LineIndex + 1 == playback.LineCount ? "Continue" : "Next"))
        {
            if (voiceSource != null) voiceSource.Stop();
            voicedLine = null;
            message = "";
            game.Loop.AdvanceDialogue();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawInventory(bool allowSelection)
    {
        GUILayout.Label("PANTRY — each row is a separate purchase batch");
        if (game.State.inventory.Count == 0) GUILayout.Label("Empty pantry.");
        foreach (var entry in game.State.inventory)
        {
            var food = game.Queries.Foods.GetById(entry.foodId);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{(food == null ? entry.foodId : Label(food.displayName, food.id))} | " +
                $"{(food == null ? "Unknown" : food.recipeCategory + " / " + food.trafficLight)} | " +
                $"{entry.servings} servings | {ShelfLife(entry.daysRemaining)}");
            if (allowSelection)
            {
                selected.TryGetValue(entry.inventoryEntryId, out int count);
                GUI.enabled = count > 0;
                if (GUILayout.Button("−", GUILayout.Width(40))) selected[entry.inventoryEntryId] = --count;
                GUI.enabled = true;
                GUILayout.Label(count.ToString(), GUILayout.Width(35));
                GUI.enabled = count < entry.servings && entry.daysRemaining != 0 && entry.daysRemaining >= -1;
                if (GUILayout.Button("+", GUILayout.Width(40))) selected[entry.inventoryEntryId] = count + 1;
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawResults(bool completed)
    {
        var summary = game.Loop.Summary;
        GUILayout.Label(completed ? "GAME COMPLETE" : "DAILY RESULTS");
        GUILayout.Label($"Spent: ${summary.moneySpent:0.00} | Earned: ${summary.moneyEarned:0.00} | " +
            $"Served: {summary.dishes.Count} | Skipped: {summary.skippedCustomerIds.Count}");
        foreach (var dish in summary.dishes)
        {
            var customer = game.Queries.GetCustomer(dish.customerId);
            GUILayout.Label($"{(customer == null ? dish.customerId : Label(customer.displayName, customer.id))} — {dish.recipeId}");
            DrawDish(dish.result);
        }
        DrawInventory(false);
        if (!completed && GUILayout.Button("Finish day / Continue"))
        {
            var result = game.Loop.FinishDay();
            message = result.success ? $"Expired batches removed: {result.expiredInventoryEntryIds.Length}" : result.error;
            GUIUtility.ExitGUI();
        }
    }
    private static void DrawDish(ServeResult result)
    {
        GUILayout.Label(result.recipeBuilt ? "Recipe built successfully" : "Recipe not built");
        foreach (var warning in result.recipeWarnings) GUILayout.Label(warning);
        GUILayout.Label($"Paid ingredient cost ${result.ingredientCost:0.00} | Minimum value ${result.minimumIngredientValue:0.00}");
        GUILayout.Label($"Payment ${result.basePayment:0.00} + tip ${result.tip:0.00} | Customer finance {result.customerFinance}");
        GUILayout.Label($"Health {result.healthDelta:+0;-0;0} | Delight {result.delightDelta:+0;-0;0} | Earned ${result.moneyEarned:0.00}");
        GUILayout.Label($"Green {result.greenServings} / Yellow {result.yellowServings} / Red {result.redServings} servings");
        GUILayout.Label("Daily Dozen: " + string.Join(", ", result.dailyDozenCovered));
        if (result.rushCrashTriggered) GUILayout.Label("Rush/crash triggered (visual effect pending).");
    }
    private static string Label(string name, string id) => string.IsNullOrWhiteSpace(name) ? id : name;
    private static string ShelfLife(int days) => days < 0 ? "no expiry" : days == 0 ? "expired" : days + " day(s) left";
}
