// File responsibility: Authored store identity and access rules; purchases enforce day and flag conditions.

using System;
using UnityEngine;

[Serializable]
public class StoreShelfDefinition
{
    public string id;
    public string displayName;
    public Sprite artwork;
    // Shelf background colour as HTML hex (Stores.csv: shelf_colours). Blank uses the default sage.
    public string colour;
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

    public PriceDisplayStyle defaultPriceDisplayStyle =
        PriceDisplayStyle.Standard;

    [Header("Shelves")]
    public StoreShelfDefinition[] shelves =
        Array.Empty<StoreShelfDefinition>();

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    // Art comes from Stores.csv (logo_path, shop_background_path); a blank path keeps an Inspector-assigned sprite.
    [Header("Presentation")]
    [Tooltip("White silhouette logo, tinted by the UI. Stores.csv: logo_path.")]
    public Sprite logo;
    [Tooltip("Store interior shown when entering the store. Stores.csv: shop_background_path.")]
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
