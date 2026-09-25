// File responsibility: Ends a day, ages pantry batches, records health changes, and applies due finance deltas.
// Call through GameLoop so phase guards prevent accidental repeated day advancement.

using System;
using System.Collections.Generic;

public class DayService : IDayService
{
    private readonly GameSessionState state;
    private readonly BalanceConfig balance;
    private readonly CustomerDatabase customers;

    public DayService(GameSessionState state, BalanceConfig balance, CustomerDatabase customers = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
        this.customers = customers;
    }

    public DayResult FinishDay()
    {
        if (state.currentDay < 1 || balance.totalDays < 1 || state.currentDay > balance.totalDays || balance.financeHealthLagDays < 1)
            return new DayResult { error = "Invalid day configuration." };
        if (state.currentDay == balance.totalDays)
            return new DayResult { success = true, currentDay = state.currentDay, gameCompleted = true };

        var expired = new List<string>();
        foreach (var entry in state.inventory)
        {
            if (entry.daysRemaining > 0) entry.daysRemaining--;
            if (entry.daysRemaining == 0) expired.Add(entry.inventoryEntryId);
        }
        state.inventory.RemoveAll(entry => entry.daysRemaining == 0 || entry.servings <= 0);
        // Snapshot every unlocked customer, including absent or skipped customers.
        if (customers != null)
        foreach (var data in customers.Customers)
            if (data != null && data.unlockDay <= state.currentDay && !state.customers.Exists(c =>
                string.Equals(c.customerId, data.id, StringComparison.OrdinalIgnoreCase)))
                state.customers.Add(CustomerState.Create(data, balance));
        foreach (var customer in state.customers)
        {
            customer.healthHistory.RemoveAll(h => h.day == state.currentDay);
            customer.healthHistory.Add(new HealthSnapshot { day = state.currentDay, health = customer.health,
                healthDelta = customer.health - customer.healthAtLastDayEnd });
            customer.healthAtLastDayEnd = customer.health;
            int targetDay = state.currentDay + 1 - balance.financeHealthLagDays;
            var source = customer.healthHistory.Find(h => h.day == targetDay);
            if (source != null) customer.finance = (int)Math.Max(0L, Math.Min(100L,
                (long)customer.finance + source.healthDelta));
        }
        state.currentDay++;
        state.currentCustomerId = null;
        state.selectedRecipeId = null;
        return new DayResult { success = true, currentDay = state.currentDay,
            expiredInventoryEntryIds = expired.ToArray() };
    }
}
