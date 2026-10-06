using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

/// <summary>One kitchen must never see or change another's data, whichever way the data is reached.</summary>
public sealed class KitchenIsolationTests : IDisposable
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly TestDatabase _database = new(kitchens: 2);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Everything_saved_in_one_kitchen_is_invisible_from_another()
    {
        await FillKitchenAsync(1);

        await using var other = _database.Open(2);
        Assert.Empty(await other.Ingredients.ToListAsync());
        Assert.Empty(await other.Recipes.ToListAsync());
        Assert.Empty(await other.RecipeIngredients.ToListAsync());
        Assert.Empty(await other.RecipeIngredientAlternatives.ToListAsync());
        Assert.Empty(await other.InventoryItems.ToListAsync());
        Assert.Empty(await other.MealPlans.ToListAsync());
        Assert.Empty(await other.MealPlanEntries.ToListAsync());
        Assert.Empty(await other.ShoppingLists.ToListAsync());
        Assert.Empty(await other.ShoppingListItems.ToListAsync());
        Assert.Empty(await other.Stores.ToListAsync());
        Assert.Empty(await other.StoreAisles.ToListAsync());
    }

    [Fact]
    public async Task The_kitchen_that_saved_it_sees_all_of_it()
    {
        await FillKitchenAsync(1);

        await using var own = _database.Open(1);
        Assert.Equal(2, await own.Ingredients.CountAsync());
        Assert.Single(await own.Recipes.ToListAsync());
        Assert.Single(await own.RecipeIngredients.ToListAsync());
        Assert.Single(await own.RecipeIngredientAlternatives.ToListAsync());
        Assert.Single(await own.InventoryItems.ToListAsync());
        Assert.Single(await own.MealPlans.ToListAsync());
        Assert.Single(await own.MealPlanEntries.ToListAsync());
        Assert.Single(await own.ShoppingLists.ToListAsync());
        Assert.Single(await own.ShoppingListItems.ToListAsync());
        Assert.Single(await own.Stores.ToListAsync());
        Assert.Single(await own.StoreAisles.ToListAsync());
    }

    [Fact]
    public async Task With_no_kitchen_nothing_is_visible_and_nothing_can_be_saved()
    {
        await FillKitchenAsync(1);

        await using var nobody = _database.Open();
        Assert.Empty(await nobody.Ingredients.ToListAsync());
        Assert.Empty(await nobody.ShoppingListItems.ToListAsync());

        nobody.Ingredients.Add(new Ingredient { Name = "Salt" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => nobody.SaveChangesAsync());
    }

    [Fact]
    public async Task Bulk_deletes_and_updates_only_reach_the_kitchen_they_run_in()
    {
        await FillKitchenAsync(1);
        await FillKitchenAsync(2);

        await using (var other = _database.Open(2))
        {
            await other.ShoppingListItems.ExecuteUpdateAsync(s => s.SetProperty(i => i.IsChecked, true));
            await other.MealPlanEntries.ExecuteDeleteAsync();
            await other.Recipes.ExecuteDeleteAsync();
        }

        await using var own = _database.Open(1);
        Assert.False((await own.ShoppingListItems.SingleAsync()).IsChecked);
        Assert.Single(await own.MealPlanEntries.ToListAsync());
        Assert.Single(await own.Recipes.ToListAsync());
    }

    [Fact]
    public async Task A_record_cannot_be_moved_into_or_saved_over_from_another_kitchen()
    {
        await FillKitchenAsync(1);
        int flourId;
        await using (var own = _database.Open(1))
        {
            flourId = (await own.Ingredients.SingleAsync(i => i.Name == "Flour")).Id;
        }

        await using var other = _database.Open(2);
        other.Update(new Ingredient { Id = flourId, KitchenId = 1, Name = "Stolen" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task Two_kitchens_can_each_have_an_ingredient_with_the_same_name_but_one_kitchen_cannot_have_two()
    {
        await FillKitchenAsync(1);
        await FillKitchenAsync(2);

        await using var own = _database.Open(1);
        own.Ingredients.Add(new Ingredient { Name = "flour" });
        await Assert.ThrowsAsync<DbUpdateException>(() => own.SaveChangesAsync());
    }

    [Fact]
    public async Task The_shopping_list_is_built_only_from_its_own_kitchens_meals_stock_and_minimums()
    {
        await FillKitchenAsync(1);
        await using (var other = _database.Open(2))
        {
            // Plenty of flour next door must not cover this kitchen's recipe, and next door's minimum is not ours.
            var flour = new Ingredient { Name = "Flour", DefaultUnit = Unit.Cup };
            other.AddRange(
                new InventoryItem { Ingredient = flour, Quantity = 100, Unit = Unit.Cup, Location = StorageLocation.Pantry },
                new Ingredient { Name = "Saffron", DefaultUnit = Unit.Pinch, MinimumQuantity = 5 });
            await other.SaveChangesAsync();
        }

        await new ShoppingListService(_database.InKitchen(1)).SyncAutomaticItemsAsync();
        await new ShoppingListService(_database.InKitchen(2)).SyncAutomaticItemsAsync();

        await using var own = _database.Open(1);
        await using var next = _database.Open(2);
        Assert.Equal(["Bread flour or Flour: 1 cup", "Candles: 1"], await ItemsAsync(own));
        Assert.Equal(["Saffron: 5 pinch"], await ItemsAsync(next));
    }

    private static async Task<List<string>> ItemsAsync(PantrixDbContext db) =>
        (await db.ShoppingListItems.AsNoTracking().ToListAsync())
        .Select(i => $"{i.Name}: {Units.Format(i.Quantity, i.Unit)}")
        .Order()
        .ToList();

    // One of everything: two ingredients, a recipe using one with the other as a substitute, stock, a planned
    // meal, a shopping list with an item, and a store with an aisle.
    private async Task FillKitchenAsync(int kitchenId)
    {
        await using var db = _database.Open(kitchenId);
        var flour = new Ingredient { Name = "Flour", DefaultUnit = Unit.Cup };
        var breadFlour = new Ingredient { Name = "Bread flour", DefaultUnit = Unit.Cup };
        var bread = new Recipe
        {
            Name = "Bread",
            Servings = 4,
            Ingredients =
            [
                new RecipeIngredient
                {
                    Ingredient = breadFlour, Quantity = 3, Unit = Unit.Cup,
                    Alternatives = [new RecipeIngredientAlternative { Ingredient = flour }]
                }
            ]
        };

        db.AddRange(
            new InventoryItem { Ingredient = flour, Quantity = 2, Unit = Unit.Cup, Location = StorageLocation.Pantry },
            new MealPlan
            {
                Name = "This week",
                StartDate = Today,
                EndDate = Today.AddDays(6),
                Entries = [new MealPlanEntry { Recipe = bread, Date = Today, Servings = 4 }]
            },
            new ShoppingList { Name = "List", Items = [new ShoppingListItem { Name = "Candles" }] },
            new Store { Name = "Corner shop", Aisles = [new StoreAisle { Ingredient = flour, Aisle = "3" }] });
        await db.SaveChangesAsync();
    }
}
