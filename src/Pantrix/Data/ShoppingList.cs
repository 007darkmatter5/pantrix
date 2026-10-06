namespace Pantrix.Data;

public class ShoppingList : IKitchenOwned
{
    public int Id { get; set; }
    public int KitchenId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int? MealPlanId { get; set; }
    public MealPlan? MealPlan { get; set; }

    // The store being shopped; when set, the list shows and sorts by that store's aisles.
    public int? StoreId { get; set; }
    public Store? Store { get; set; }
    public List<ShoppingListItem> Items { get; set; } = [];
}

public class ShoppingListItem
{
    public int Id { get; set; }
    public int ShoppingListId { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;

    // Null for items typed in by hand that don't correspond to a known ingredient.
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public string Name { get; set; } = "";
    public IngredientCategory Category { get; set; } = IngredientCategory.Other;
    public decimal Quantity { get; set; } = 1;
    public Unit Unit { get; set; }
    public bool IsChecked { get; set; }

    // True for items the app maintains itself from meal plans and ingredient minimums.
    public bool IsAutomatic { get; set; }
}
