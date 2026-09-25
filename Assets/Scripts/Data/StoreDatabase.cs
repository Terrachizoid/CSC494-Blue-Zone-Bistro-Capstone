// File responsibility: Imported store catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StoreDatabase",
    menuName = "Blue Zone Bistro/Databases/Stores")]
public class StoreDatabase : ScriptableObject
{
    [SerializeField]
    private List<StoreData> stores = new List<StoreData>();

    private Dictionary<string, StoreData> byId;

    public IReadOnlyList<StoreData> Stores => stores;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<StoreData> items)
    {
        stores = items ?? new List<StoreData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out StoreData store)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out store);
    }

    public StoreData GetById(string id)
    {
        return TryGetById(id, out StoreData store) ? store : null;
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
        byId = new Dictionary<string, StoreData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (StoreData store in stores)
        {
            if (store == null || string.IsNullOrWhiteSpace(store.id))
            {
                continue;
            }

            if (!byId.TryAdd(store.id, store))
            {
                Debug.LogError($"Duplicate StoreData ID '{store.id}'.", this);
            }
        }
    }
}
