// File responsibility: Imported recipe catalogue with case-insensitive stable-ID lookup.
// Use SetItems during import/tests to rebuild the index; mutable session state belongs elsewhere.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RecipeDatabase",
    menuName = "Blue Zone Bistro/Databases/Recipes")]
public class RecipeDatabase : ScriptableObject
{
    [SerializeField]
    private List<RecipeData> recipes = new List<RecipeData>();

    private Dictionary<string, RecipeData> byId;

    public IReadOnlyList<RecipeData> Recipes => recipes;

    private void OnEnable()
    {
        RebuildIndex();
    }

    public void SetItems(List<RecipeData> items)
    {
        recipes = items ?? new List<RecipeData>();
        RebuildIndex();
    }

    public bool TryGetById(string id, out RecipeData recipe)
    {
        EnsureIndex();
        return byId.TryGetValue(id ?? string.Empty, out recipe);
    }

    public RecipeData GetById(string id)
    {
        return TryGetById(id, out RecipeData recipe) ? recipe : null;
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
        byId = new Dictionary<string, RecipeData>(
            StringComparer.OrdinalIgnoreCase);

        foreach (RecipeData recipe in recipes)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.id))
            {
                continue;
            }

            if (!byId.TryAdd(recipe.id, recipe))
            {
                Debug.LogError($"Duplicate RecipeData ID '{recipe.id}'.", this);
            }
        }
    }
}
