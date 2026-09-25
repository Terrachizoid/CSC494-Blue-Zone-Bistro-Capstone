// File responsibility: Authored category-slot meal template, unlock day, payment, and optional presentation.
// Slots describe categories rather than exact foods; RecipeRequirements interprets their bounds.

using System;
using UnityEngine;

[Serializable]
public class RecipeSlot
{
    public FoodCategory category = FoodCategory.Unassigned;

    [Min(1)]
    public int servings = 1;

    public bool optional;
}

[CreateAssetMenu(
    fileName = "NewRecipe",
    menuName = "Blue Zone Bistro/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    public string regionLabel;

    [Header("Composition")]
    public RecipeSlot[] slots = Array.Empty<RecipeSlot>();
    public bool allowNonGreenChoices = true;

    [Header("Availability and Economy")]
    [Min(1)]
    public int unlockDay = 1;
    public RecipeTier tier = RecipeTier.Basic;

    [Min(0f)]
    public float baseSalePrice;

    [Min(0)]
    public int delightValue;

    [Header("Tags and Claims")]
    public CravingTag[] cravingTags = { CravingTag.None };
    public ClaimData[] claims = Array.Empty<ClaimData>();

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    [Header("Presentation")]
    public Sprite icon;
}
