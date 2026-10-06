namespace Pantrix.Data;

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

public class MealPlan : IKitchenOwned
{
    public int Id { get; set; }
    public int KitchenId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<MealPlanEntry> Entries { get; set; } = [];

    /// <summary>Dates the user has marked as fully planned.</summary>
    public List<DateOnly> ClosedDates { get; set; } = [];
}

public class MealPlanEntry
{
    public int Id { get; set; }
    public int MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; } = MealType.Dinner;

    // A meal is a recipe, a single ingredient served as it comes (e.g. a packaged meal), or, when both
    // ids are null, eaten out or ordered in. Only the first two need groceries.
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int Servings { get; set; }

    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; } = 1;
    public Unit Unit { get; set; }

    /// <summary>For a meal that isn't cooked, what it is (e.g. "Order pizza"). Null when undecided.</summary>
    public string? Note { get; set; }
}
