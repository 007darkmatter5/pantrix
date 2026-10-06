namespace Pantrix.Data;

public enum IngredientCategory
{
    Produce,
    Dairy,
    Meat,
    Seafood,
    Bakery,
    Frozen,
    DryGoods,
    CannedGoods,
    Spices,
    Condiments,
    Beverages,
    Other
}

public class Ingredient : IKitchenOwned
{
    public int Id { get; set; }
    public int KitchenId { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The specific product usually bought, e.g. a brand and variety, when <see cref="Name"/> is just what it's called at home.</summary>
    public string? ProductName { get; set; }

    public IngredientCategory Category { get; set; } = IngredientCategory.Other;
    public Unit DefaultUnit { get; set; } = Unit.Each;

    /// <summary>Amount to keep on hand, in <see cref="DefaultUnit"/>. Null when the ingredient isn't restocked automatically.</summary>
    public decimal? MinimumQuantity { get; set; }
}
