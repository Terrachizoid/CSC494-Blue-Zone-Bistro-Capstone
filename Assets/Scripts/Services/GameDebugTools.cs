// File responsibility: Developer shortcuts for reproducing situations (debug panel, F2 in editor/development builds).
// Everything changes the same shared session state the game uses, so the shortcuts never drift out of date with the
// rules: events are added as normal EventRecords, pantry food as normal batches, days end through GameLoop.
// Not reachable in release builds (BistroDebugPanel only exists when Debug.isDebugBuild).
using System;
using System.Collections.Generic;
using System.Linq;

public class GameDebugTools
{
    private readonly GameSessionState state;
    private readonly EventDatabase events;
    private readonly StoreListingDatabase listings;
    private readonly BalanceConfig balance;

    public GameDebugTools(GameSessionState state, EventDatabase events, StoreListingDatabase listings, BalanceConfig balance)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.events = events;
        this.listings = listings;
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
    }

    public IEnumerable<EventDefinition> Events =>
        events == null ? Enumerable.Empty<EventDefinition>() : events.events.Where(e => e != null);

    public List<EventRecord> ActiveEvents =>
        (state.events ?? new List<EventRecord>()).Where(r => r != null && r.Active(state.currentDay)).ToList();

    public void AddMoney(float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
        state.money = (float)Math.Round(state.money + amount, 2, MidpointRounding.AwayFromZero);
    }

    // Popularity now; today's guest count was planned this morning, so it changes from tomorrow.
    public void AddPopularity(float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
        state.popularity = CommunityProgress.Clamp(state.popularity + (double)amount, balance.maximumPopularity);
    }

    // Starts an event today for its normal duration, ignoring its day range, weight and easy-mode setting.
    // An already running copy is replaced, so the effects never stack.
    public string StartEvent(string eventId)
    {
        var definition = Events.FirstOrDefault(e => string.Equals(e.id, eventId, StringComparison.OrdinalIgnoreCase));
        if (definition == null) return $"No event '{eventId}'.";
        if (definition.effects == null || definition.effects.Count == 0) return $"{definition.id} has no effects.";
        if (!definition.effects.All(MarketEffects.ValidEffect)) return $"{definition.id} has an invalid effect.";
        if (state.events == null) state.events = new List<EventRecord>();
        foreach (var running in ActiveEvents.Where(r => string.Equals(r.eventId, definition.id, StringComparison.OrdinalIgnoreCase)))
            running.expiresOnDay = state.currentDay;
        state.events.Add(new EventRecord
        {
            eventId = definition.id,
            startDay = state.currentDay,
            expiresOnDay = state.currentDay + Math.Max(1, definition.durationDays),
            effects = definition.effects.Select(e => e.Copy()).ToList()
        });
        return null;
    }

    // Ends every running event (it stays in the news history as ended today).
    public int EndEvents()
    {
        var running = ActiveEvents;
        foreach (var record in running) record.expiresOnDay = state.currentDay;
        return running.Count;
    }

    // Adds `servings` of the cheapest open, unlocked green food for every food type sold today (as if bought,
    // already sorted). Purchase cost is the current price, so selling it back behaves normally.
    public int StockPantry(int servings)
    {
        if (servings <= 0 || listings == null) return 0;
        var effects = new MarketEffects(state);
        var open = listings.Listings.Where(l => l != null && l.food != null && l.store != null && l.servingsPerPackage > 0 &&
            l.unlockDay <= state.currentDay && l.store.unlockDay <= state.currentDay &&
            (string.IsNullOrWhiteSpace(l.requiredFlag) || state.flags.HasFlag(l.requiredFlag)) &&
            l.food.trafficLight == TrafficLight.Green && effects.UnavailableReason(l) == null && effects.Quote(l).valid)
            .ToList();
        int added = 0;
        foreach (var category in open.Select(l => l.food.recipeCategory).Where(c => c != FoodCategory.Unassigned).Distinct())
        {
            var cheapest = open.Where(l => l.food.recipeCategory == category)
                .OrderBy(l => effects.Quote(l).effectivePrice / l.servingsPerPackage).ThenBy(l => l.id, StringComparer.Ordinal).First();
            int days = balance.GetShelfLife(cheapest.food.storageType);
            state.inventory.Add(new InventoryEntry
            {
                foodId = cheapest.food.id,
                listingId = cheapest.id,
                servings = servings,
                purchaseCostPerServing = effects.Quote(cheapest).effectivePrice / cheapest.servingsPerPackage,
                daysRemaining = days <= 0 ? -1 : days
            });
            if (!state.boughtFoodIds.Contains(cheapest.food.id)) state.boughtFoodIds.Add(cheapest.food.id);
            added++;
        }
        return added;
    }

    // Colour grade checks (2026-11-22): community wellbeing drives the scenery grade (and the endings).
    public void SetCommunityWellbeing(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        state.communityWellbeing = CommunityProgress.Clamp(value, balance.maximumCommunityWellbeing);
    }

    public CustomerState Customer(string customerId) =>
        state.customers.Find(c => string.Equals(c.customerId, customerId, StringComparison.OrdinalIgnoreCase));

    // A guest's health (0-100); their portrait grade follows it. Health history and finance are left alone.
    public bool SetCustomerHealth(string customerId, int health)
    {
        var customer = Customer(customerId);
        if (customer == null) return false;
        customer.health = Math.Max(0, Math.Min(100, health));
        return true;
    }

    // The rush/crash visual offset, as RushCrashProgress sets it: rush = served red food today (+rushVisualBoost),
    // crash = the day after (-crashVisualDrop, and the crash conversation plays at their next arrival), clear = neither.
    public bool SetRushCrash(string customerId, int mode)
    {
        var customer = Customer(customerId);
        if (customer == null) return false;
        if (mode > 0) { customer.rushDay = state.currentDay; customer.visualScore = balance.rushVisualBoost; customer.crashDialoguePending = false; }
        else if (mode < 0) { customer.rushDay = state.currentDay - 1; customer.visualScore = -balance.crashVisualDrop; customer.crashDialoguePending = true; }
        else { customer.rushDay = 0; customer.visualScore = 0; customer.crashDialoguePending = false; }
        return true;
    }

    public int ClearPantry()
    {
        int n = state.inventory.Count;
        state.inventory.Clear();
        return n;
    }
}
