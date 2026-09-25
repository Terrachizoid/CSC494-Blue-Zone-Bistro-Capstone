// File responsibility: One store offer: food reference, package servings, price, and access restrictions.
// Multiple offers for one food are allowed and contribute to its minimum price per serving.

using UnityEngine;

[CreateAssetMenu(
    fileName = "NewStoreListing",
    menuName = "Blue Zone Bistro/Store Listing")]
public class StoreListingData : ScriptableObject
{
    [Header("Identity and Foreign References")]
    public string id;
    public FoodData food;
    public StoreData store;
    public string shelfId;

    [Header("Availability")]
    [Min(1)]
    public int unlockDay = 1;
    public string requiredFlag;

    [Header("Price")]
    [Min(0f)]
    public float observedPriceCad;

    [Min(0)]
    public int servingsPerPackage;

    [Min(0f)]
    public float gamePrice;

    [TextArea]
    public string priceSource;

    public PriceDisplayStyle priceDisplayStyle =
        PriceDisplayStyle.Standard;

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    public float ObservedCostPerServing =>
        servingsPerPackage > 0
            ? observedPriceCad / servingsPerPackage
            : 0f;

    public float GameCostPerServing =>
        servingsPerPackage > 0
            ? gamePrice / servingsPerPackage
            : 0f;
}
