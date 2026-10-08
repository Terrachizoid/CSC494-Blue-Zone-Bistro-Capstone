// File responsibility: Top-level screens/stages used by GameLoop and UI. Service substeps live in ServiceSessionState.

// Explicit values preserve existing serialized phase values.
public enum GamePhase { Market = 0, Service = 1, Results = 2, Completed = 3, FoodSorting = 4 }
