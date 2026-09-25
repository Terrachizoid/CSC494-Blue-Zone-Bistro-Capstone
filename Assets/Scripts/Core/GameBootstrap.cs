// File responsibility: Composition root: creates one shared session service graph and connects the greybox UI.
// Editor setup assigns default assets; player builds use serialized references, never editor file paths.

using UnityEngine;

// The editor fills missing references from the standard imported asset locations.
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private FoodDatabase foods;
    [SerializeField] private StoreListingDatabase listings;
    [SerializeField] private RecipeDatabase recipes;
    [SerializeField] private CustomerDatabase customers;
    [SerializeField] private DialogueDatabase dialogues;
    [SerializeField] private BalanceConfig balance;
    [SerializeField] private GameSessionState state = new GameSessionState();

    public GameSessionState State => state;
    public GameLoop Loop { get; private set; }
    public GameContentQueries Queries { get; private set; }

    private void Awake()
    {
        if (foods == null || listings == null || recipes == null || customers == null || balance == null || dialogues == null)
        {
            Debug.LogError("GameBootstrap is missing content. Run Tools > Blue Zone Bistro > Import All CSV Content. " +
                "Defaults are connected automatically; inspect the Console for import errors if references remain missing.", this);
            enabled = false;
            return;
        }
        foods.RebuildMinimumPrices(listings.Listings);
        Loop = new GameLoop(new PurchaseService(state, listings, balance),
            new MealService(state, foods, recipes, customers, balance), new DayService(state, balance, customers),
            new CustomerVisitService(state, customers, balance), new EncounterScheduler(state, dialogues, customers, recipes));
        Queries = new GameContentQueries(state, foods, listings, recipes, customers);
        var ui = GetComponent<GreyboxUIController>();
        if (ui == null) ui = gameObject.AddComponent<GreyboxUIController>();
        ui.Initialize(this);
    }
}
