// File responsibility: Imported claim catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ClaimDatabase",
    menuName = "Blue Zone Bistro/Databases/Claims")]
public class ClaimDatabase : ScriptableObject
{
    [SerializeField]
    private List<ClaimData> claims = new List<ClaimData>();

    private Dictionary<string, ClaimData> byId;

    public IReadOnlyList<ClaimData> Claims => claims;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<ClaimData> items)
    {
        claims = items ?? new List<ClaimData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out ClaimData claim)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out claim);
    }

    public ClaimData GetById(string id)
    {
        return TryGetById(id, out ClaimData claim) ? claim : null;
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
        byId = new Dictionary<string, ClaimData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (ClaimData claim in claims)
        {
            if (claim == null || string.IsNullOrWhiteSpace(claim.id))
            {
                continue;
            }

            if (!byId.TryAdd(claim.id, claim))
            {
                Debug.LogError($"Duplicate ClaimData ID '{claim.id}'.", this);
            }
        }
    }
}
