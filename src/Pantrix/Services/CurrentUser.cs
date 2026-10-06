using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>The signed-in account, read from the database each time so a change of kitchen takes effect at once.</summary>
public class CurrentUser(AuthenticationStateProvider authentication, DbContextOptions<PantrixDbContext> options)
{
    public async Task<int?> GetIdAsync()
    {
        try
        {
            var user = (await authentication.GetAuthenticationStateAsync()).User;
            return int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        }
        catch (InvalidOperationException)
        {
            // Asked outside a page or circuit, where nobody is signed in.
            return null;
        }
    }

    /// <summary>The account with its kitchen, or null when nobody is signed in or the account is gone.</summary>
    public async Task<AppUser?> GetAsync()
    {
        if (await GetIdAsync() is not { } id)
        {
            return null;
        }

        await using var db = new PantrixDbContext(options);
        return await db.Users.AsNoTracking().Include(u => u.Kitchen).SingleOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>The kitchen the account is working in; 0 (no kitchen, so no data) when nobody is signed in.</summary>
    public async Task<int> GetKitchenIdAsync() => (await GetAsync())?.KitchenId ?? 0;
}

/// <summary>
/// Hands out database contexts that only see the signed-in account's kitchen. Registered as the app's
/// <see cref="IDbContextFactory{TContext}"/>, so pages and services get this without asking for it.
/// </summary>
public class KitchenDbContextFactory(DbContextOptions<PantrixDbContext> options, CurrentUser currentUser)
    : IDbContextFactory<PantrixDbContext>
{
    public PantrixDbContext CreateDbContext() =>
        new(options) { KitchenId = currentUser.GetKitchenIdAsync().GetAwaiter().GetResult() };

    public async Task<PantrixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        new(options) { KitchenId = await currentUser.GetKitchenIdAsync() };
}
