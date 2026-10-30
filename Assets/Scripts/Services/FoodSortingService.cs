// Evaluates choices against the checkout snapshot, applies one refund, and records
// correct food IDs only. The refund is maximumSortingDiscount of what was paid for each
// correctly sorted new food; known foods and wrong answers earn nothing. UI never supplies scores or money. Wrong answers can return
// after a later purchase, but the same checkout cannot be submitted twice.
using System;
using System.Collections.Generic;
using System.Linq;

public class FoodSortingService
{
    private readonly GameSessionState state;
    private readonly FoodDatabase foods;
    public FoodSortingService(GameSessionState state, FoodDatabase foods)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.foods = foods ?? throw new ArgumentNullException(nameof(foods));
    }
    public bool HasCheckout => state.shoppingReceipt != null && state.shoppingReceipt.day == state.currentDay;
    public bool IsComplete => HasCheckout && state.shoppingReceipt.sortingSubmitted;
    // Return copies so presentation cannot change the transaction's answer key.
    public List<SortingQuestion> GetQuestions() => !HasCheckout ? new List<SortingQuestion>() :
        state.shoppingReceipt.questions.Select(q => new SortingQuestion {
            listingId = q.listingId, foodId = q.foodId, correctAnswer = q.correctAnswer, cost = q.cost }).ToList();

    public string GetExplanation(string foodId)
    {
        var food = foods.GetById(foodId);
        if (food == null) return "Food definition unavailable.";
        return string.IsNullOrWhiteSpace(food.sortingExplanation)
            ? $"This game's food catalogue classifies {food.displayName ?? food.id} as {food.trafficLight}. Detailed explanation and research are pending."
            : food.sortingExplanation;
    }

    public SortingResult Submit(List<SortingChoice> choices)
    {
        if (!HasCheckout || IsComplete) return Fail("No unsubmitted sorting round for today.");
        var receipt = state.shoppingReceipt;
        if (choices == null || choices.Count != receipt.questions.Count) return Fail("Answer every food exactly once.");
        var answers = new Dictionary<string, TrafficLight>(StringComparer.OrdinalIgnoreCase);
        foreach (var choice in choices)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.foodId) || answers.ContainsKey(choice.foodId) ||
                (choice.answer != TrafficLight.Green && choice.answer != TrafficLight.Yellow && choice.answer != TrafficLight.Red))
                return Fail("Each food needs one Green, Yellow, or Red answer.");
            answers.Add(choice.foodId, choice.answer);
        }
        var result = new SortingResult { questionCount = receipt.questions.Count };
        var refundedFoods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double refund = 0;
        foreach (var question in receipt.questions)
        {
            if (!answers.TryGetValue(question.foodId, out var answer)) return Fail("An expected food answer is missing.");
            bool correct = answer == question.correctAnswer;
            if (correct) result.correctCount++;
            float share = 0;
            if (correct && question.cost > 0 && !float.IsNaN(question.cost) && !float.IsInfinity(question.cost))
            {
                share = (float)Math.Round(question.cost * (double)receipt.maximumDiscount, 2, MidpointRounding.AwayFromZero);
                refund += share; refundedFoods.Add(question.foodId);
            }
            result.feedback.Add(new SortingFeedback { foodId = question.foodId, chosen = answer,
                correct = question.correctAnswer, isCorrect = correct, explanation = GetExplanation(question.foodId), refund = share });
        }
        result.accuracy = result.questionCount == 0 ? 0 : result.correctCount / (float)result.questionCount;
        result.refundAmount = Math.Min(receipt.total, (float)refund);
        // Share of the whole checkout that came back (display only).
        result.discountRate = receipt.total > 0 ? result.refundAmount / receipt.total : 0;
        if (float.IsNaN(state.money) || float.IsInfinity(state.money + result.refundAmount)) return Fail("Invalid funds.");
        var batches = new List<InventoryEntry>();
        foreach (var id in receipt.batchIds)
        {
            var matches = state.inventory.FindAll(e => e != null && string.Equals(e.inventoryEntryId, id, StringComparison.OrdinalIgnoreCase));
            if (matches.Count != 1) return Fail("A purchased batch is missing or duplicated.");
            batches.Add(matches[0]);
        }
        // Only the correctly sorted foods became cheaper; their batches record the discounted cost.
        float paidRatio = 1 - receipt.maximumDiscount;
        foreach (var batch in batches)
            if (refundedFoods.Contains(batch.foodId)) batch.purchaseCostPerServing *= paidRatio;
        if (state.correctlySortedFoodIds == null) state.correctlySortedFoodIds = new List<string>();
        foreach (var feedback in result.feedback.Where(f => f.isCorrect))
            if (!state.correctlySortedFoodIds.Exists(id => string.Equals(id, feedback.foodId, StringComparison.OrdinalIgnoreCase)))
                state.correctlySortedFoodIds.Add(feedback.foodId);
        state.money += result.refundAmount;
        result.success = true;
        receipt.sortingSubmitted = true;
        receipt.sortingResult = result;
        return result;
    }
    private static SortingResult Fail(string error) => new SortingResult { error = error };
}
