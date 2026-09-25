// File responsibility: Authored customer identity, initial health, availability, and preference metadata.
// Mutable health/delight/finance belong to CustomerState. Starting prosperity is legacy metadata.

using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewCustomer",
    menuName = "Blue Zone Bistro/Customer")]
public class CustomerData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    public CustomerType customerType = CustomerType.Regular;

    [Header("Availability and Starting State")]
    [Min(1)]
    public int unlockDay = 1;

    [Range(0, 100)]
    public int startingHealth = 50;

    [Min(0)]
    public int startingProsperity;

    [TextArea]
    public string themeFromPlan;

    [Header("Preferences and Evidence")]
    public RecipeData[] preferredRecipes = Array.Empty<RecipeData>();
    public CravingTag[] cravingTags = { CravingTag.None };
    public ClaimData[] dialogueClaims = Array.Empty<ClaimData>();

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    [Header("Presentation")]
    public Sprite portrait;
    public Sprite customerSprite;
    public TextAsset dialogueAsset;
}
