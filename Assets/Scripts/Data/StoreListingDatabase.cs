// File responsibility: Imported storelisting catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StoreListingDatabase",
    menuName = "Blue Zone Bistro/Databases/Store Listings")]
public class StoreListingDatabase : ScriptableObject
{
    [SerializeField]
    private List<StoreListingData> listings =
        new List<StoreListingData>();

    private Dictionary<string, StoreListingData> byId;

    public IReadOnlyList<StoreListingData> Listings => listings;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<StoreListingData> items)
    {
        listings = items ?? new List<StoreListingData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out StoreListingData listing)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out listing);
    }

    public StoreListingData GetById(string id)
    {
        return TryGetById(id, out StoreListingData listing)
            ? listing
            : null;
    }

    public List<StoreListingData> GetForStore(StoreData store)
    {
        List<StoreListingData> result = new List<StoreListingData>();

        foreach (StoreListingData listing in listings)
        {
            if (listing != null && listing.store == store)
            {
                result.Add(listing);
            }
        }

        return result;
    }

    private void EnsureIndex()
    {
        if (byId == null)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        byId = new Dictionary<string, StoreListingData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (StoreListingData listing in listings)
        {
            if (listing == null || string.IsNullOrWhiteSpace(listing.id))
            {
                continue;
            }

            if (!byId.TryAdd(listing.id, listing))
            {
                Debug.LogError(
                    $"Duplicate StoreListingData ID '{listing.id}'.",
                    this);
            }
        }
    }
}
