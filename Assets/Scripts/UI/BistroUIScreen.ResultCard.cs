// The dish result as a card beside the guest (Owen, 2026-11-23). It replaces the text result panel: once a dish is
// served the guest's reaction dialogue starts straight away, and the card stays up beside the guest until that
// dialogue ends. Its bars move at the guest's colour-grade moment (visibleSince + guestGradeDelay), so the card and
// the guest change together. Animation: BistroResultCard.cs.
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private static readonly Color CardHealth = new Color(.33f, .58f, .36f), CardHealthDrop = new Color(.70f, .30f, .27f),
        CardDelight = new Color(.86f, .64f, .20f), CardMuted = new Color(.30f, .36f, .33f), CardTrack = new Color(.86f, .84f, .78f);

    // Shown from the dish result until the reaction dialogue that follows it ends.
    private bool ResultCardVisible()
    {
        if (game?.Loop == null || game.Loop.Phase != GamePhase.Service || game.Loop.Service == null) return false;
        var service = game.Loop.Service;
        if (service.lastDishResult == null || !service.lastDishResult.success) return false;
        return service.awaitingDishAcknowledgement || service.step == ServiceStep.AfterDialogue;
    }

    private void ResultCard(RectTransform scene)
    {
        if (!ResultCardVisible()) return;
        var service = game.Loop.Service;
        var r = service.lastDishResult;
        var cs = game.State.customers.Find(c => string.Equals(c.customerId, service.CurrentCustomerId, StringComparison.OrdinalIgnoreCase));
        var recipe = game.Queries.GetUnlockedRecipes().FirstOrDefault(x => string.Equals(x.id, service.lastRecipeId, StringComparison.OrdinalIgnoreCase));
        int healthNow = cs?.health ?? 50, delightNow = cs?.delight ?? game.Balance.startingDelight;
        float delightMax = Mathf.Max(1, game.Balance.maxDelight);

        var card = BistroShelfStyle.PaperPanel(scene, "Dish result card", theme, new Vector2(.67f, .37f), new Vector2(.965f, .87f));
        card.GetComponent<Image>().raycastTarget = false;
        BistroShelfStyle.Text(card, recipe?.displayName ?? "Your dish", theme, new Vector2(.08f, .85f), new Vector2(.92f, .96f), 24, BistroShelfStyle.Ink, true);
        BistroShelfStyle.Text(card, (r.recipeBuilt ? "" : "Recipe mismatch · ") + $"Ingredient appeal {r.ingredientAppeal:0.0} / 10",
            theme, new Vector2(.08f, .77f), new Vector2(.92f, .85f), 16, r.recipeBuilt ? CardMuted : CardHealthDrop);

        var motion = card.gameObject.AddComponent<BistroResultCard>();
        StatRow(card, "Health", .64f, out motion.healthValue, out motion.healthFill, out motion.healthGlow);
        StatRow(card, "Delight", .47f, out motion.delightValue, out motion.delightFill, out motion.delightGlow);
        motion.healthColor = r.healthDelta < 0 ? CardHealthDrop : CardHealth; motion.delightColor = CardDelight;
        motion.healthValue.color = motion.healthColor; motion.delightValue.color = new Color(.62f, .44f, .08f);
        motion.healthTo = healthNow / 100f; motion.healthFrom = (healthNow - r.healthDelta) / 100f;
        motion.delightTo = delightNow / delightMax; motion.delightFrom = (delightNow - r.delightDelta) / delightMax;
        motion.healthDelta = r.healthDelta; motion.delightDelta = r.delightDelta;
        motion.popularityDelta = r.popularityDelta; motion.paid = r.moneyEarned;

        motion.popularityValue = BistroShelfStyle.Text(card, "", theme, new Vector2(.08f, .30f), new Vector2(.55f, .39f), 18, BistroShelfStyle.Green, true);
        motion.paidValue = BistroShelfStyle.Text(card, "", theme, new Vector2(.50f, .30f), new Vector2(.92f, .39f), 18, BistroShelfStyle.Green, true, TextAnchor.MiddleRight);
        string requests = r.activeCravings.Length == 0 ? "No request today"
            : !r.recipeBuilt ? $"Requests met: none (0/{r.activeCravings.Length})"
            : $"Requests met: {r.cravingsResolved.Length}/{r.activeCravings.Length}" + (r.cravingsResolved.Length == 0 ? "" : " (" + string.Join(", ", r.cravingsResolved) + ")");
        BistroShelfStyle.Text(card, requests, theme, new Vector2(.08f, .21f), new Vector2(.92f, .30f), 15, BistroShelfStyle.Ink);
        var notes = BistroShelfStyle.Text(card, CardNotes(r), theme, new Vector2(.08f, .04f), new Vector2(.92f, .21f), 14, CardMuted, false, TextAnchor.UpperLeft);
        notes.resizeTextMinSize = 11;

        motion.appearAt = visibleSince;
        motion.startAt = visibleSince + theme.guestGradeDelay;
        motion.instant = theme.reducedMotion;
    }

    private void StatRow(RectTransform card, string label, float top, out Text value, out RectTransform fill, out Image glow)
    {
        BistroShelfStyle.Text(card, label, theme, new Vector2(.08f, top), new Vector2(.60f, top + .09f), 18, BistroShelfStyle.Ink, true);
        value = BistroShelfStyle.Text(card, "", theme, new Vector2(.60f, top), new Vector2(.92f, top + .09f), 18, BistroShelfStyle.Ink, true, TextAnchor.MiddleRight);
        var track = BistroShelfStyle.Panel(card, label + " track", CardTrack, new Vector2(.08f, top - .055f), new Vector2(.92f, top - .01f));
        track.GetComponent<Image>().raycastTarget = false;
        fill = BistroShelfStyle.Panel(track, label + " fill", CardHealth, Vector2.zero, new Vector2(.5f, 1));
        fill.GetComponent<Image>().raycastTarget = false;
        var g = BistroShelfStyle.Panel(track, label + " glow", new Color(1, 1, 1, 0), Vector2.zero, Vector2.one);
        glow = g.GetComponent<Image>(); glow.raycastTarget = false;
    }

    // Short notes that used to sit in the text result: mismatch, repetition, full delight, Daily Dozen, payment detail.
    private static string CardNotes(ServeResult r)
    {
        var notes = new System.Collections.Generic.List<string>();
        if (!r.recipeBuilt) notes.Add("The plate didn't match the recipe: reduced payment, no tip.");
        if (r.recipeFatigued) notes.Add("Guests are used to this recipe, so it earned less popularity.");
        if (r.delightDelta == 0 && r.dishDelight > 0) notes.Add("The guest was already fully delighted.");
        if (r.dailyDozenCovered.Length > 0) notes.Add("Daily Dozen: " + IntroMealPlan.ReadableList(r.dailyDozenCovered));
        notes.Add($"Payment ${r.basePayment:0.00} + tip ${r.tip:0.00} · ingredients ${r.ingredientCost:0.00}");
        return string.Join("\n", notes);
    }
}
