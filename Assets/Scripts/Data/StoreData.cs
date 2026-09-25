// File responsibility: Authored store identity and access rules; purchases enforce day, flag, and membership conditions.
// Actual purchased memberships live in the session, not this shared asset.

using System;
using UnityEngine;

[Serializable]
public class StoreShelfDefinition
{
    public string id;
    public string displayName;
    public Sprite artwork;
}

[CreateAssetMenu(
    fileName = "NewStore",
    menuName = "Blue Zone Bistro/Store")]
public class StoreData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea]
    public string roleDescription;

    [TextArea]
    public string description;

    [Header("Availability and Economy")]
    [Min(1)]
    public int unlockDay = 1;
    public bool requiresMembership;

    [Min(0f)]
    public float membershipCost;

    public PriceDisplayStyle defaultPriceDisplayStyle =
        PriceDisplayStyle.Standard;

    [Header("Shelves")]
    public StoreShelfDefinition[] shelves =
        Array.Empty<StoreShelfDefinition>();

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    [Header("Presentation")]
    public Sprite exteriorIcon;
    public Sprite shopBackground;

    public bool HasShelf(string shelfId)
    {
        if (string.IsNullOrWhiteSpace(shelfId) || shelves == null)
        {
            return false;
        }

        foreach (StoreShelfDefinition shelf in shelves)
        {
            if (shelf != null && string.Equals(
                    shelf.id,
                    shelfId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
