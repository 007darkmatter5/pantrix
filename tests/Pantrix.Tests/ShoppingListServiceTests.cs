using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public sealed class ShoppingListServiceTests : IDisposable
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly TestDatabase _database = new();
    private readonly IDbContextFactory<PantrixDbContext> _factory;
    private readonly ShoppingListService _service;

    public ShoppingListServiceTests()
    {
        _factory = _database.InKitchen(1);
        _service = new ShoppingListService(_factory);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Scales_recipes_by_servings_and_subtracts_inventory()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var flour = new Ingredient { Name = "Flour", Category = IngredientCategory.DryGoods, DefaultUnit = Unit.Cup };
            var eggs = new Ingredient { Name = "Eggs", Category = IngredientCategory.Dairy };
            var milk = new Ingredient { Name = "Milk", Category = IngredientCategory.Dairy, DefaultUnit = Unit.Cup };

            var pancakes = new Recipe
            {
                Name = "Pancakes",
                Servings = 4,
                Ingredients =
                [
                    new RecipeIngredient { Ingredient = flour, Quantity = 2, Unit = Unit.Cup },
                    new RecipeIngredient { Ingredient = eggs, Quantity = 2, Unit = Unit.Each },
                    new RecipeIngredient { Ingredient = milk, Quantity = 1.5m, Unit = Unit.Cup }
                ]
            };

            db.AddRange(
                // Eight servings of a four-serving recipe doubles every quantity.
                Plan(new MealPlanEntry { Recipe = pancakes, Date = Today, MealType = MealType.Breakfast, Servings = 8 }),
                new InventoryItem { Ingredient = eggs, Quantity = 1, Unit = Unit.Each, Location = StorageLocation.Refrigerator },
                new InventoryItem { Ingredient = milk, Quantity = 1, Unit = Unit.Gallon, Location = StorageLocation.Refrigerator });
            await db.SaveChangesAsync();
        }

        await _service.SyncAutomaticItemsAsync();

        var list = Assert.Single(await ListsAsync());
        Assert.Equal(["Eggs: 3 (auto)", "Flour: 4 cup (auto)"], list);
    }

    [Fact]
    public async Task The_same_recipe_planned_twice_needs_twice_the_ingredients()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var beef = new Ingredient { Name = "Ground beef", DefaultUnit = Unit.Pound, MinimumQuantity = 1 };
            var casserole = new Recipe
            {
                Name = "Casserole",
                Servings = 6,
                Ingredients = [new RecipeIngredient { Ingredient = beef, Quantity = 1, Unit = Unit.Pound }]
            };

            db.AddRange(
                Plan(
                    new MealPlanEntry { Recipe = casserole, Date = Today.AddDays(2), Servings = 6 },
                    new MealPlanEntry { Recipe = casserole, Date = Today.AddDays(6), Servings = 6 }),
                new InventoryItem { Ingredient = beef, Quantity = 1, Unit = Unit.Pound, Location = StorageLocation.Freezer });
            await db.SaveChangesAsync();
        }

        await _service.SyncAutomaticItemsAsync();

        Assert.Equal(["Ground beef: 1 lb (auto)"], Assert.Single(await ListsAsync()));
    }

    [Fact]
    public async Task Meals_before_today_are_ignored()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var beef = new Ingredient { Name = "Ground beef", DefaultUnit = Unit.Pound };
            var casserole = new Recipe
            {
                Name = "Casserole",
                Ingredients = [new RecipeIngredient { Ingredient = beef, Quantity = 1, Unit = Unit.Pound }]
            };

            db.Add(Plan(new MealPlanEntry { Recipe = casserole, Date = Today.AddDays(-1), Servings = 4 }));
            await db.SaveChangesAsync();
        }

        Assert.Null(await _service.SyncAutomaticItemsAsync());
        Assert.Empty(await ListsAsync());
    }

    [Fact]
    public async Task An_ingredient_planned_as_a_meal_is_needed_like_a_recipe_ingredient()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var beefBits = new Ingredient { Name = "Beef Bits", DefaultUnit = Unit.Package };

            db.AddRange(
                Plan(
                    new MealPlanEntry { Ingredient = beefBits, Date = Today, Quantity = 1, Unit = Unit.Package },
                    new MealPlanEntry { Ingredient = beefBits, Date = Today.AddDays(3), Quantity = 2, Unit = Unit.Package }),
                new InventoryItem { Ingredient = beefBits, Quantity = 1, Unit = Unit.Package, Location = StorageLocation.Freezer });
            await db.SaveChangesAsync();
        }

        await _service.SyncAutomaticItemsAsync();

        Assert.Equal(["Beef Bits: 2 pkg (auto)"], Assert.Single(await ListsAsync()));
    }

    [Fact]
    public async Task Eating_out_needs_no_groceries()
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Add(Plan(
                new MealPlanEntry { Date = Today, Note = "Order pizza" },
                new MealPlanEntry { Date = Today.AddDays(1) }));
            await db.SaveChangesAsync();
        }

        Assert.Null(await _service.SyncAutomaticItemsAsync());
        Assert.Empty(await ListsAsync());
    }

    [Fact]
    public async Task Deleting_a_recipe_removes_its_planned_meals_but_not_eating_out()
    {
        await using (var db = _factory.CreateDbContext())
        {
            db.Add(Plan(
                new MealPlanEntry { Date = Today, Recipe = new Recipe { Name = "Casserole" }, Servings = 4 },
                new MealPlanEntry { Date = Today, Note = "Order pizza" }));
            await db.SaveChangesAsync();
            await db.Recipes.ExecuteDeleteAsync();
        }

        await using var verify = _factory.CreateDbContext();
        Assert.Equal("Order pizza", Assert.Single(await verify.MealPlanEntries.ToListAsync()).Note);
    }

    [Fact]
    public async Task Automatic_items_follow_the_newest_list_and_track_inventory()
    {
        await using (var db = _factory.CreateDbContext())
        {
            var penne = new Ingredient { Name = "Penne pasta", DefaultUnit = Unit.Box };
            var elbow = new Ingredient { Name = "Elbow pasta", DefaultUnit = Unit.Box };
            var sauce = new Ingredient { Name = "Marinara", DefaultUnit = Unit.Can };
            var salt = new Ingredient { Name = "Salt", DefaultUnit = Unit.Box, MinimumQuantity = 1 };
            var rice = new Ingredient { Name = "Rice", DefaultUnit = Unit.Pound, MinimumQuantity = 2 };

            var bake = new Recipe
            {
                Name = "Pasta bake",
                Servings = 4,
                Ingredients =
                [
                    new RecipeIngredient
                    {
                        Ingredient = penne, Quantity = 1, Unit = Unit.Box,
                        Alternatives = [new RecipeIngredientAlternative { Ingredient = elbow }]
                    },
                    new RecipeIngredient { Ingredient = sauce, Quantity = 1, Unit = Unit.Can }
                ]
            };

            db.AddRange(
                Plan(new MealPlanEntry { Recipe = bake, Date = Today, Servings = 4 }),
                salt,
                // Elbow covers the penne line; a full pound of rice still leaves it one short of its minimum.
                new InventoryItem { Ingredient = elbow, Quantity = 1, Unit = Unit.Box, Location = StorageLocation.Pantry },
                new InventoryItem { Ingredient = rice, Quantity = 16, Unit = Unit.Ounce, Location = StorageLocation.Pantry },
                new ShoppingList { Name = "Mine", Items = [new ShoppingListItem { Name = "Birthday candles" }] });
            await db.SaveChangesAsync();
        }

        // Syncing twice must not duplicate anything.
        await _service.SyncAutomaticItemsAsync();
        await _service.SyncAutomaticItemsAsync();
        Assert.Equal(
            ["Birthday candles: 1", "Marinara: 1 can (auto)", "Rice: 1 lb (auto)", "Salt: 1 box (auto)"],
            Assert.Single(await ListsAsync()));

        await using (var db = _factory.CreateDbContext())
        {
            // Rice is bought and put away; salt is checked off in the store but not in inventory yet.
            var rice = await db.Ingredients.SingleAsync(i => i.Name == "Rice");
            db.Add(new InventoryItem { IngredientId = rice.Id, Quantity = 1, Unit = Unit.Pound, Location = StorageLocation.Pantry });
            (await db.ShoppingListItems.SingleAsync(i => i.Name == "Salt")).IsChecked = true;
            await db.SaveChangesAsync();
        }

        await _service.SyncAutomaticItemsAsync();
        Assert.Equal(
            ["Birthday candles: 1", "Marinara: 1 can (auto)", "Salt: 1 box (auto)"],
            Assert.Single(await ListsAsync()));

        await using (var db = _factory.CreateDbContext())
        {
            db.Add(new ShoppingList { Name = "Newer", CreatedUtc = DateTime.UtcNow.AddMinutes(1) });
            await db.SaveChangesAsync();
        }

        // What is still to buy moves to the newer list; hand-added and checked-off items stay where they were.
        await _service.SyncAutomaticItemsAsync();
        var lists = await ListsAsync();
        Assert.Equal(["Birthday candles: 1", "Salt: 1 box (auto)"], lists[0]);
        Assert.Equal(["Marinara: 1 can (auto)", "Salt: 1 box (auto)"], lists[1]);
    }

    private static MealPlan Plan(params MealPlanEntry[] entries) => new()
    {
        Name = "Plan",
        StartDate = Today.AddDays(-7),
        EndDate = Today.AddDays(7),
        Entries = [.. entries]
    };

    private async Task<List<List<string>>> ListsAsync()
    {
        await using var db = _factory.CreateDbContext();
        var lists = await db.ShoppingLists.AsNoTracking().Include(l => l.Items).OrderBy(l => l.Id).ToListAsync();
        return lists
            .Select(l => l.Items
                .OrderBy(i => i.Name)
                .Select(i => $"{i.Name}: {Units.Format(i.Quantity, i.Unit)}{(i.IsAutomatic ? " (auto)" : "")}")
                .ToList())
            .ToList();
    }
}
