using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>What one account can see about the kitchen it is working in.</summary>
public record KitchenInfo(Kitchen Kitchen, bool IsOwner, IReadOnlyList<AppUser> Members);

/// <summary>
/// Sharing a kitchen. Each account owns one kitchen and works in one: its own unless it has joined another
/// with that kitchen's join code. Joining is always the joiner's own act, so nobody can be pulled into a
/// kitchen, and leaving returns them to their own, which is kept untouched meanwhile.
/// </summary>
public class KitchenService(IDbContextFactory<PantrixDbContext> dbFactory)
{
    public async Task<KitchenInfo?> GetAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().Include(u => u.Kitchen).SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return null;
        }

        var members = await db.Users.AsNoTracking().Where(u => u.KitchenId == user.KitchenId).OrderBy(u => u.UserName).ToListAsync();
        return new KitchenInfo(user.Kitchen, user.Kitchen.OwnerId == userId, members);
    }

    /// <summary>Renames the kitchen the account owns and is working in. Returns a message if it couldn't.</summary>
    public async Task<string?> RenameAsync(int userId, string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return "Enter a name for the kitchen.";
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await OwnedKitchenInUseAsync(db, userId) is not { } kitchen)
        {
            return "Only the kitchen's owner can rename it.";
        }

        kitchen.Name = name;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Replaces the join code, so one that has been passed around stops working.</summary>
    public async Task<string?> RegenerateJoinCodeAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await OwnedKitchenInUseAsync(db, userId) is not { } kitchen)
        {
            return "Only the kitchen's owner can change its join code.";
        }

        kitchen.JoinCode = AccountService.NewJoinCode();
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Moves the account into the kitchen with that join code. Returns a message if it couldn't.</summary>
    public async Task<string?> JoinAsync(int userId, string? joinCode)
    {
        joinCode = joinCode?.Trim().ToUpperInvariant() ?? "";
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        var kitchen = joinCode.Length == 0 ? null : await db.Kitchens.SingleOrDefaultAsync(k => k.JoinCode == joinCode);
        if (kitchen is null)
        {
            return "No kitchen has that join code. Check it with the kitchen's owner.";
        }

        if (kitchen.Id == user.KitchenId)
        {
            return "You're already in that kitchen.";
        }

        user.KitchenId = kitchen.Id;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Returns the account to the kitchen it owns.</summary>
    public async Task LeaveAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await SendHomeAsync(db, await db.Users.SingleAsync(u => u.Id == userId));
        await db.SaveChangesAsync();
    }

    /// <summary>Lets a kitchen's owner take someone else out of it; they go back to their own kitchen.</summary>
    public async Task<string?> RemoveMemberAsync(int ownerId, int memberId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await OwnedKitchenInUseAsync(db, ownerId) is not { } kitchen)
        {
            return "Only the kitchen's owner can remove people.";
        }

        var member = await db.Users.SingleOrDefaultAsync(u => u.Id == memberId && u.KitchenId == kitchen.Id);
        if (member is null || member.Id == ownerId)
        {
            return "That person isn't a guest in this kitchen.";
        }

        await SendHomeAsync(db, member);
        await db.SaveChangesAsync();
        return null;
    }

    // The kitchen the account both owns and is currently working in; owning a kitchen you've walked away
    // from to join another doesn't let you manage the one you are visiting.
    private static async Task<Kitchen?> OwnedKitchenInUseAsync(PantrixDbContext db, int userId)
    {
        var user = await db.Users.Include(u => u.Kitchen).SingleOrDefaultAsync(u => u.Id == userId);
        return user?.Kitchen.OwnerId == userId ? user.Kitchen : null;
    }

    private static async Task SendHomeAsync(PantrixDbContext db, AppUser user)
    {
        var own = await db.Kitchens.FirstAsync(k => k.OwnerId == user.Id);
        user.KitchenId = own.Id;
    }
}
