// File responsibility: Per-run customer progress, cached daily attendance, and lagged health-change history.
// Use Create to initialize the health baseline correctly; finance follows deltas, not absolute health.

using System;
using System.Collections.Generic;

[Serializable]
public class CustomerState
{
    public string customerId;
    public int health;
    public int delight = 50;
    public int finance = 20;
    public int healthAtLastDayEnd = 50;
    public int lastServedDay;
    public int attendanceDay;
    public bool attending;
    public List<HealthSnapshot> healthHistory = new List<HealthSnapshot>();

    public static CustomerState Create(CustomerData data, BalanceConfig balance)
    {
        return new CustomerState { customerId = data.id, health = data.startingHealth,
            healthAtLastDayEnd = data.startingHealth,
            delight = Math.Max(0, Math.Min(balance.maxDelight, balance.startingDelight)),
            finance = Math.Max(0, Math.Min(100, balance.startingFinance)) };
    }
}

[Serializable]
public class HealthSnapshot
{
    public int day;
    public int health;
    public int healthDelta;
}
