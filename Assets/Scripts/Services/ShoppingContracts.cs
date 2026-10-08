// Cart commands contain listing quantities; sorting commands contain choices only.
// Receipts snapshot the confirmed transaction. Runtime ownership belongs to services,
// not UI; these serializable records are not a complete save/load implementation.
using System;
using System.Collections.Generic;

public interface ICartPurchaseService
{
    CartResult Quote(List<CartEntry> cart);
    CartResult Checkout(List<CartEntry> cart);
}
[Serializable] public class CartEntry { public string listingId; public int packageQuantity = 1; }
[Serializable] public class CartResult : OperationResult { public float total; public int packages; }
[Serializable] public class SortingChoice { public string foodId; public TrafficLight answer; }
[Serializable] public class SortingQuestion
{
    public string listingId; // Representative offer; different offers of the same food form one question.
    public string foodId;
    public TrafficLight correctAnswer;
}
[Serializable] public class SortingFeedback
{
    public string foodId;
    public TrafficLight chosen;
    public TrafficLight correct;
    public bool isCorrect;
    public string explanation;
}
[Serializable] public class SortingResult : OperationResult
{
    public int correctCount;
    public int questionCount;
    public float accuracy;
    public float discountRate;
    public float refundAmount;
    public List<SortingFeedback> feedback = new List<SortingFeedback>();
}
[Serializable] public class ShoppingReceipt
{
    public int day;
    public float total;
    public float maximumDiscount;
    public List<string> batchIds = new List<string>();
    public List<SortingQuestion> questions = new List<SortingQuestion>();
    public bool sortingSubmitted;
    public SortingResult sortingResult;
}
