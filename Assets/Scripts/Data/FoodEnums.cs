// File responsibility: Shared CSV vocabulary for food classification, category, storage, and Daily Dozen tags.
// Renaming enum values requires migrating CSVs and checking serialized Unity assets.

public enum FoodCategory
{
    Unassigned,
    Beans,
    Fruit,
    Vegetable,
    Grain,
    StarchyVegetable,
    Meat,
    MeatSubstitute,
    Dairy,
    Egg,
    Fat,
    Beverage,
    Other
}

public enum TrafficLight
{
    Unassigned,
    Green,
    Yellow,
    Red
}

public enum DailyDozenCategory
{
    None,
    Beans,
    Berries,
    OtherFruit,
    CruciferousVegetables,
    Greens,
    OtherVegetables,
    Flaxseed,
    NutsAndSeeds,
    HerbsAndSpices,
    WholeGrains,
    Beverages
}

public enum StorageType
{
    Unassigned,
    Fresh,
    Frozen,
    Canned,
    Dry,
    ShelfStable
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    NeedsRevision
}