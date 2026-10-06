using System.Security.Claims;
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

    private readonly PasswordHasher<AppUser> _hasher = new();

    /// <summary>False on a fresh install, when the first visitor is asked to create the account.</summary>
    public async Task<bool> AnyUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AnyAsync();
    }

    /// <summary>Creates a user, or returns a message saying why it couldn't.</summary>
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
        if (await db.Users.AnyAsync(u => u.UserName == userName))
        {
            return (null, "That username is already taken.");
        }

        var user = new AppUser { UserName = userName };
        user.PasswordHash = _hasher.HashPassword(user, password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();
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
