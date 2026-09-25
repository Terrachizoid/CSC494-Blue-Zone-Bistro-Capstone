// File responsibility: Shared mutable state for one run: day, money, pantry, customers, flags, and dialogue history.
// Services receive this same instance. Serialization alone does not implement saving or restoring a run.

using System;
using System.Collections.Generic;

// Mutable data for one playthrough. Game flow and save/load belong in separate systems.
[Serializable]
public class GameSessionState
{
    public int currentDay = 1;
    public float money = 30f;
    public List<InventoryEntry> inventory = new List<InventoryEntry>();
    public string currentCustomerId;
    public string selectedRecipeId;
    public List<CustomerState> customers = new List<CustomerState>();
    public GameFlagState flags = new GameFlagState();
    public DialogueHistoryState dialogueHistory = new DialogueHistoryState();
    public List<string> storeMembershipIds = new List<string>();
}
