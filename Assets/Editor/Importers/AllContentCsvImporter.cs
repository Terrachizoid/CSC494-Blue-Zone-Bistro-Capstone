// File responsibility: Editor entry point importing content in dependency order so references resolve.
// Run after changing non-dialogue CSVs; inspect Console errors before testing the game.

using UnityEditor;
using UnityEngine;

public static class AllContentCsvImporter
{
    [MenuItem("Tools/Blue Zone Bistro/Import All CSV Content")]
    public static void ImportAll()
    {
        Debug.Log("Starting Blue Zone Bistro content import...");

        // Dependency order matters. Dialogue can reference Claims, Recipes,
        // and Customers, so it is imported last.
        FoodCsvImporter.ImportFoods();
        StoreCsvImporter.ImportStores();
        ClaimCsvImporter.ImportClaims();
        StoreListingCsvImporter.ImportPrices();
        RecipeCsvImporter.ImportRecipes();
        CustomerCsvImporter.ImportCustomers();
        DialogueCsvImporter.ImportDialogues();
        GameBootstrapSetup.SetupLoadedScenes();
        BistroContentValidator.Validate();

        Debug.Log("Blue Zone Bistro content import sequence finished.");
    }
}
