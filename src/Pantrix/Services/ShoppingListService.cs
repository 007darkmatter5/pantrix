using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

public class ShoppingListService(IDbContextFactory<PantrixDbContext> dbFactory)
{
    /// <summary>
    /// Brings the newest shopping list's automatic items in line with what today's and later planned meals
    /// need and what is below its minimum, given the current inventory. A list is created if there isn't
    /// one and something is needed. Automatic items that are no longer needed, or that sit on older lists,
    /// are removed unless they have been checked off. Items added by hand are never touched.
    /// </summary>
    /// <returns>The id of the newest list, or null when there are no lists.</returns>
    public async Task<int?> SyncAutomaticItemsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var ingredients = await db.Ingredients.AsNoTracking().ToDictionaryAsync(i => i.Id);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var entries = await db.MealPlanEntries.AsNoTracking()
            .Where(e => e.Date >= today && (e.RecipeId != null || e.IngredientId != null))
            .Include(e => e.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(ri => ri.Alternatives)
            .AsSplitQuery()
            .ToListAsync();

        // A need is already met when the ingredient, or anything that can stand in for it, is always on hand.
        bool AlwaysOnHand(IngredientNeed need) =>
            need.Alternatives.Prepend(need.IngredientId).Any(id => ingredients[id].AlwaysOnHand);

        var needed = entries.SelectMany(entry => entry.Recipe is { } recipe
                ? recipe.Ingredients.Select(ri => new IngredientNeed(
                    ri.IngredientId,
                    ri.Quantity * entry.Servings / Math.Max(1, recipe.Servings),
                    ri.Unit,
                    ri.Alternatives.Select(a => a.IngredientId).ToList()))
                : [new IngredientNeed(entry.IngredientId!.Value, entry.Quantity, entry.Unit)])
            .Where(need => !AlwaysOnHand(need));

        var onHand = (await db.InventoryItems.AsNoTracking().ToListAsync())
            .Select(i => new IngredientAmount(i.IngredientId, i.Quantity, i.Unit));

        var minimums = ingredients.Values
            .Where(i => i.MinimumQuantity > 0 && !i.AlwaysOnHand)
            .Select(i => new IngredientAmount(i.Id, i.MinimumQuantity!.Value, i.DefaultUnit));

        var wanted = ShoppingListBuilder.ComputeShortfall(needed, onHand, minimums)
            .Select(need => new ShoppingListItem
            {
                IngredientId = need.IngredientId,
                Name = string.Join(" or ", need.Alternatives.Prepend(need.IngredientId).Select(id => ingredients[id].Name)),
                Category = ingredients[need.IngredientId].Category,
                Quantity = need.Quantity,
                Unit = need.Unit,
                IsAutomatic = true
            })
            .ToDictionary(i => (i.Name, i.Unit));

        var list = await db.ShoppingLists
            .OrderByDescending(l => l.CreatedUtc).ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync();
        var automaticItems = await db.ShoppingListItems.Where(i => i.IsAutomatic).ToListAsync();

        // A checked-off item has been bought but isn't in inventory yet, so it still counts towards what's wanted.
        foreach (var bought in automaticItems.Where(i => i.IsChecked && i.ShoppingListId == list?.Id))
        {
            if (wanted.TryGetValue((bought.Name, bought.Unit), out var item))
            {
                item.Quantity -= bought.Quantity;
                if (item.Quantity <= 0)
                {
                    wanted.Remove((bought.Name, bought.Unit));
                }
            }
        }

        foreach (var existing in automaticItems.Where(i => !i.IsChecked))
        {
            if (existing.ShoppingListId == list?.Id && wanted.Remove((existing.Name, existing.Unit), out var item))
            {
                existing.IngredientId = item.IngredientId;
                existing.Category = item.Category;
                existing.Quantity = item.Quantity;
            }
            else
            {
                db.Remove(existing);
            }
        }

        if (list is null && wanted.Count > 0)
        {
            list = new ShoppingList { Name = "Shopping list" };
            db.ShoppingLists.Add(list);
        }

        list?.Items.AddRange(wanted.Values);

        await db.SaveChangesAsync();
        return list?.Id;
    }
}
