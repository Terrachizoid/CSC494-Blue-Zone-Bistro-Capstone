// File responsibility: Authored food definition: classification, recipe category, labels, claims, and optional visuals.
// Keep runtime stock and expiry in InventoryEntry; CSV import owns content fields.

using System.Collections.Generic;
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
    // Foods.csv also_categories (| separated, optional): other recipe slots this food can fill, e.g. tofu as a
    // meat substitute. One serving still counts toward one category only (RecipeRequirements.Assign).
    [Tooltip("Foods.csv also_categories: other recipe categories this food can fill (e.g. Tofu: MeatSubstitute).")]
    public FoodCategory[] alsoCategories = new FoodCategory[0];
    public DailyDozenCategory[] dailyDozenCategories =
        { DailyDozenCategory.None };
    public TrafficLight trafficLight = TrafficLight.Unassigned;
    [Range(0, 10)] public float ingredientAppeal;

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

    // Art comes from Foods.csv (icon_path, back_path). A blank path keeps whatever sprite
    // was dropped into these slots in the Inspector, so either workflow works.
    [Header("Presentation")]
    [Tooltip("Card front art (food and its background). Foods.csv: icon_path.")]
    public Sprite icon;
    [Tooltip("Card back art. Foods.csv: back_path. Blank uses the front art.")]
    public Sprite cardBack;
    [Tooltip("Foods.csv card_style: light_logo. Use a pale store logo on dark art.")]
    public bool lightLogo;
    [Tooltip("Foods.csv card_style: dark_plaque. Use the dark name plaque on light art.")]
    public bool darkPlaque;
    [Tooltip("Foods.csv card_style: sprig. Adds the leaf sprig beside the name plaque.")]
    public bool cardSprig;

    // Can this food go in a slot of this category (its main category or one of also_categories)?
    public bool Fits(FoodCategory category) =>
        category != FoodCategory.Unassigned &&
        (recipeCategory == category || (alsoCategories != null && System.Array.IndexOf(alsoCategories, category) >= 0));

    // Main category first, then the extra ones (no duplicates, no Unassigned).
    public IEnumerable<FoodCategory> Categories
    {
        get
        {
            yield return recipeCategory;
            if (alsoCategories == null) yield break;
            var seen = new HashSet<FoodCategory> { recipeCategory, FoodCategory.Unassigned };
            foreach (var c in alsoCategories) if (seen.Add(c)) yield return c;
        }
    }

    public bool HasShortListStamp =>
        approvalStatus == ApprovalStatus.Approved &&
        trafficLight == TrafficLight.Green &&
        ingredientCount == 1;
}
