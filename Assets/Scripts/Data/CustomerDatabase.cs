// File responsibility: Imported customer catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CustomerDatabase",
    menuName = "Blue Zone Bistro/Databases/Customers")]
public class CustomerDatabase : ScriptableObject
{
    [SerializeField]
    private List<CustomerData> customers = new List<CustomerData>();

    private Dictionary<string, CustomerData> byId;

    public IReadOnlyList<CustomerData> Customers => customers;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<CustomerData> items)
    {
        customers = items ?? new List<CustomerData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out CustomerData customer)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out customer);
    }

    public CustomerData GetById(string id)
    {
        return TryGetById(id, out CustomerData customer) ? customer : null;
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
        byId = new Dictionary<string, CustomerData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (CustomerData customer in customers)
        {
            if (customer == null || string.IsNullOrWhiteSpace(customer.id))
            {
                continue;
            }

            if (!byId.TryAdd(customer.id, customer))
            {
                Debug.LogError(
                    $"Duplicate CustomerData ID '{customer.id}'.",
                    this);
            }
        }
    }
}
