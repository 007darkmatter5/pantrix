using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

public class StoreAisleService(IDbContextFactory<PantrixDbContext> dbFactory)
{
    /// <summary>
    /// Records where an ingredient is in a store. Entering or correcting an aisle counts as confirming it today;
    /// blank text forgets it; text that matches what is already recorded changes nothing.
    /// </summary>
    public Task SetAsync(int storeId, int ingredientId, string? aisleText) =>
        ChangeAsync(storeId, ingredientId, record =>
        {
            var text = aisleText?.Trim() ?? "";
            if (record.Aisle != text)
            {
                record.Aisle = text;
                record.LastConfirmed = DateOnly.FromDateTime(DateTime.Today);
            }
        });

    /// <summary>Records the ingredient's page on the store's website; blank text forgets it.</summary>
    public Task SetProductUrlAsync(int storeId, int ingredientId, string? url) =>
        ChangeAsync(storeId, ingredientId, record => record.ProductUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim());

    /// <summary>True for a web address that is safe to render as a link.</summary>
    public static bool IsWebLink(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    // The record exists only while it says something: once both the aisle and the link are blank it is removed.
    private async Task ChangeAsync(int storeId, int ingredientId, Action<StoreAisle> change)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.StoreAisles.SingleOrDefaultAsync(a => a.StoreId == storeId && a.IngredientId == ingredientId)
            ?? db.Add(new StoreAisle { StoreId = storeId, IngredientId = ingredientId }).Entity;

        change(record);
        if (record.Aisle.Length == 0 && record.ProductUrl is null)
        {
            db.Remove(record);
        }

        await db.SaveChangesAsync();
    }
}
