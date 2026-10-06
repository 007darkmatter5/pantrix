using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

public enum SignInOutcome
{
    Succeeded,
    WrongCredentials,
    LockedOut
}

public class AccountService(IDbContextFactory<PantrixDbContext> dbFactory, TimeProvider time)
{
    public const int MinimumPasswordLength = 8;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private const string RegistrationOpenKey = "RegistrationOpen";

    private readonly PasswordHasher<AppUser> _hasher = new();

    /// <summary>False on a fresh install, when the first visitor is asked to create the account.</summary>
    public async Task<bool> AnyUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AnyAsync();
    }

    /// <summary>
    /// Whether someone can create an account for themselves. Open until an admin closes it, and always open
    /// while there are no accounts, or a fresh install could never be set up.
    /// </summary>
    public async Task<bool> IsRegistrationOpenAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return !await db.Users.AnyAsync() || (await db.AppSettings.FindAsync(RegistrationOpenKey))?.Value != "false";
    }

    public async Task SetRegistrationOpenAsync(bool open)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(RegistrationOpenKey)
            ?? db.Add(new AppSetting { Key = RegistrationOpenKey }).Entity;
        setting.Value = open ? "true" : "false";
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates an account with a kitchen of its own, or returns a message saying why it couldn't.
    /// The first account on an install is the admin, and takes over the kitchen left by a version from before
    /// accounts existed, if there is one.
    /// </summary>
    public async Task<(AppUser? User, string? Error)> CreateAsync(string? userName, string? password)
    {
        userName = userName?.Trim() ?? "";
        if (userName.Length == 0)
        {
            return (null, "Enter a username.");
        }

        if ((password?.Length ?? 0) < MinimumPasswordLength)
        {
            return (null, $"Use a password of at least {MinimumPasswordLength} characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await IsRegistrationOpenAsync())
        {
            return (null, "New accounts aren't being accepted.");
        }

        if (await db.Users.AnyAsync(u => u.UserName == userName))
        {
            return (null, "That username is already taken.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var isFirst = !await db.Users.AnyAsync();
        var kitchen = isFirst ? await db.Kitchens.FirstOrDefaultAsync(k => k.OwnerId == null) : null;
        kitchen ??= db.Add(new Kitchen { JoinCode = NewJoinCode() }).Entity;
        kitchen.Name = $"{userName}'s kitchen";

        var user = new AppUser { UserName = userName, IsAdmin = isFirst, Kitchen = kitchen };
        user.PasswordHash = _hasher.HashPassword(user, password!);
        db.Users.Add(user);

        // The account and its kitchen point at each other, so the kitchen's owner can only be set once the account has an id.
        await db.SaveChangesAsync();
        kitchen.Owner = user;
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        return (user, null);
    }

    /// <summary>
    /// Checks a username and password. Too many wrong passwords in a row lock the account for a few minutes,
    /// so a password can't be found by trying thousands of them.
    /// </summary>
    public async Task<(SignInOutcome Outcome, AppUser? User)> CheckPasswordAsync(string? userName, string? password)
    {
        userName = userName?.Trim() ?? "";
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == userName);
        if (user is null)
        {
            return (SignInOutcome.WrongCredentials, null);
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (user.LockedUntilUtc > now)
        {
            return (SignInOutcome.LockedOut, null);
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? "");
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedSignIns++;
            if (user.FailedSignIns >= MaxFailedSignIns)
            {
                user.FailedSignIns = 0;
                user.LockedUntilUtc = now + LockoutDuration;
            }

            await db.SaveChangesAsync();
            return (user.LockedUntilUtc > now ? SignInOutcome.LockedOut : SignInOutcome.WrongCredentials, null);
        }

        user.FailedSignIns = 0;
        user.LockedUntilUtc = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password!);
        }

        await db.SaveChangesAsync();
        return (SignInOutcome.Succeeded, user);
    }

    /// <summary>Everyone with an account, for the admin's list.</summary>
    public async Task<List<AppUser>> GetUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Include(u => u.Kitchen).OrderBy(u => u.UserName).ToListAsync();
    }

    // Letters and digits that can't be mistaken for each other when read aloud or copied by hand.
    public static string NewJoinCode() => RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 8);

    /// <summary>The identity stored in the sign-in cookie.</summary>
    public static ClaimsPrincipal ToPrincipal(AppUser user) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName)
        ],
        CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>
    /// Where to send someone after signing in: the page they originally asked for, but only if it is a page of
    /// this app. Anything else could be a link crafted to bounce them to another site.
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is ['/', ..] && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\") ? returnUrl : "/";
}
