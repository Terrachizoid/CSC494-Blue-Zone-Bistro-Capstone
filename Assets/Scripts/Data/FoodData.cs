// File responsibility: Authored food definition: classification, recipe category, labels, claims, and optional visuals.
// Keep runtime stock and expiry in InventoryEntry; CSV import owns content fields.

using UnityEngine;

[CreateAssetMenu(
    fileName = "NewFood",
    menuName = "Blue Zone Bistro/Food")]
public class FoodData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Classification")]
    public FoodCategory recipeCategory = FoodCategory.Unassigned;
    public DailyDozenCategory[] dailyDozenCategories =
        { DailyDozenCategory.None };
    public TrafficLight trafficLight = TrafficLight.Unassigned;

    [Header("Card Text")]
    [TextArea]
    public string marketingLabel;

    [TextArea]
    public string ingredientList;

    // Optional authored Codex explanation. Blank means the educational text is still pending.
    [TextArea(3, 8)] public string sortingExplanation;

    [Min(0)]
    public int ingredientCount;

    [Header("Storage and References")]
    public StorageType storageType = StorageType.Unassigned;
    public string claimId;

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    [Header("Presentation")]
    public Sprite icon;

    public bool HasShortListStamp =>
        approvalStatus == ApprovalStatus.Approved &&
        trafficLight == TrafficLight.Green &&
        ingredientCount == 1;
}
