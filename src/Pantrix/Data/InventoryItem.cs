namespace Pantrix.Data;

public enum StorageLocation
{
    Refrigerator,
    Freezer,
    Pantry
}

public class InventoryItem
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public StorageLocation Location { get; set; }
    public decimal Quantity { get; set; } = 1;
    public Unit Unit { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}
