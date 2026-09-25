// File responsibility: Rolls eligible regular/walk-in attendance from delight once per customer per day.
// An injected RNG supports deterministic tests. Visiting chefs are scheduled as story encounters.

using System;
using System.Collections.Generic;

public interface ICustomerVisitService { List<string> GetTodaysCustomers(); }

// Sample at most once per customer per day; repeated queries cannot reroll attendance.
public class CustomerVisitService : ICustomerVisitService
{
    private readonly GameSessionState state;
    private readonly CustomerDatabase customers;
    private readonly BalanceConfig balance;
    private readonly Func<double> random;
    public CustomerVisitService(GameSessionState state, CustomerDatabase customers, BalanceConfig balance,
        Func<double> random = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.customers = customers ?? throw new ArgumentNullException(nameof(customers));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
        var generator = new Random();
        this.random = random ?? generator.NextDouble;
    }
    public List<string> GetTodaysCustomers()
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var customer in customers.Customers)
        {
            if (customer == null || customer.customerType == CustomerType.VisitingChef || string.IsNullOrWhiteSpace(customer.id) ||
                customer.unlockDay > state.currentDay || !seen.Add(customer.id)) continue;
            var progress = state.customers.Find(c => string.Equals(c.customerId, customer.id,
                StringComparison.OrdinalIgnoreCase));
            if (progress == null)
            {
                progress = CustomerState.Create(customer, balance);
                state.customers.Add(progress);
            }
            progress.delight = Math.Max(0, Math.Min(balance.maxDelight, progress.delight));
            if (progress.attendanceDay != state.currentDay)
            {
                double probability = progress.delight / (double)Math.Max(1, balance.maxDelight);
                progress.attending = random() < probability;
                progress.attendanceDay = state.currentDay;
            }
            if (progress.attending && progress.lastServedDay != state.currentDay) ids.Add(customer.id);
        }
        return ids;
    }
}
