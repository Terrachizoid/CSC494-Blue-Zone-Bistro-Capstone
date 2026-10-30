// File responsibility: Shared content vocabulary for stores, recipes, customers, cravings, and claims.
// CSV import uses these enum names; preserve serialized values when extending them.

public enum PriceDisplayStyle
{
    Standard,
    Premium,
    Sale,
    Bulk
}

public enum RecipeTier
{
    Basic,
    Standard,
    Premium,
    Luxury
}

public enum CravingTag
{
    None,
    Savory,
    Sweet,
    Crunchy,
    Creamy,
    Fizzy,
    Hearty,
    // Satisfied by any built plate whose mean ingredient appeal reaches BalanceConfig.comfortMinimumAppeal,
    // not by recipe tags. Appended last so existing serialized values keep their numbers.
    Comfort
}

public enum ClaimUsageTag
{
    None,
    IngredientInspector,
    FoodCard,
    RecipeCard,
    Dialogue,
    DailyTip,
    EvidenceCodex
}

public enum CustomerType
{
    Regular,
    WalkIn,
    VisitingChef
}
