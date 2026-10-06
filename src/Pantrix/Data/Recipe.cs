namespace Pantrix.Data;

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int Servings { get; set; } = 4;
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public string? Instructions { get; set; }
    public string? SourceUrl { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
    public string? Note { get; set; }

    /// <summary>Ingredients that can stand in for <see cref="Ingredient"/>, in the same quantity and unit.</summary>
    public List<RecipeIngredientAlternative> Alternatives { get; set; } = [];
}

public class RecipeIngredientAlternative
{
    public int Id { get; set; }
    public int RecipeIngredientId { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
}
