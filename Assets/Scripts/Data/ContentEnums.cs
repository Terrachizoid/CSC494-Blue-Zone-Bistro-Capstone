// File responsibility: Shared content vocabulary for stores, recipes, customers, cravings, and claims.
// CSV import uses these enum names; preserve serialized values when extending them.

public enum PriceDisplayStyle
{
    Standard,
    Premium,
    Sale,
    Bulk,
    Membership
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
    Hearty
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
