using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public sealed class AccountServiceTests : IDisposable
{
    // A fresh install: no accounts and no kitchens. Nobody is signed in while signing up, so no kitchen is in scope.
    private readonly TestDatabase _database = new(kitchens: 0);
    private readonly Clock _clock = new();
    private readonly AccountService _accounts;

    public AccountServiceTests()
    {
        _accounts = new AccountService(_database.InKitchen(0), _clock);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task The_first_account_is_the_admin_and_later_ones_are_not()
    {
        var (first, _) = await _accounts.CreateAsync("mark", "correct horse");
        var (second, _) = await _accounts.CreateAsync("sam", "battery staple");

        Assert.True(first!.IsAdmin);
        Assert.False(second!.IsAdmin);
    }

    [Fact]
    public async Task Each_account_gets_a_kitchen_of_its_own_that_it_owns_and_works_in()
    {
        var (mark, _) = await _accounts.CreateAsync("mark", "correct horse");
        var (sam, _) = await _accounts.CreateAsync("sam", "battery staple");

        await using var db = _database.Open();
        var kitchens = await db.Kitchens.OrderBy(k => k.Id).ToListAsync();
        Assert.Equal(["mark's kitchen", "sam's kitchen"], kitchens.Select(k => k.Name));
        Assert.Equal([mark!.Id, sam!.Id], kitchens.Select(k => k.OwnerId!.Value));
        Assert.Equal([mark.KitchenId, sam.KitchenId], kitchens.Select(k => k.Id));
        Assert.NotEqual(kitchens[0].JoinCode, kitchens[1].JoinCode);
    }

    [Fact]
    public async Task The_first_account_takes_over_a_kitchen_left_from_before_accounts_existed()
    {
        await using (var db = _database.Open())
        {
            db.Kitchens.Add(new Kitchen { Id = 1, Name = "My kitchen", JoinCode = "OLDDATA1" });
            await db.SaveChangesAsync();
        }

        await using (var db = _database.Open(1))
        {
            db.Ingredients.Add(new Ingredient { Name = "Flour" });
            await db.SaveChangesAsync();
        }

        var (mark, _) = await _accounts.CreateAsync("mark", "correct horse");
        var (sam, _) = await _accounts.CreateAsync("sam", "battery staple");

        Assert.Equal(1, mark!.KitchenId);
        Assert.NotEqual(1, sam!.KitchenId);
        await using var marks = _database.Open(mark.KitchenId);
        await using var sams = _database.Open(sam.KitchenId);
        Assert.Equal("Flour", (await marks.Ingredients.SingleAsync()).Name);
        Assert.Empty(await sams.Ingredients.ToListAsync());
    }

    [Fact]
    public async Task Sign_ups_are_open_until_an_admin_closes_them()
    {
        Assert.True(await _accounts.IsRegistrationOpenAsync());
        await _accounts.CreateAsync("mark", "correct horse");
        Assert.True(await _accounts.IsRegistrationOpenAsync());

        await _accounts.SetRegistrationOpenAsync(false);

        Assert.False(await _accounts.IsRegistrationOpenAsync());
        var (user, error) = await _accounts.CreateAsync("sam", "battery staple");
        Assert.Null(user);
        Assert.Equal("New accounts aren't being accepted.", error);

        await _accounts.SetRegistrationOpenAsync(true);
        Assert.NotNull((await _accounts.CreateAsync("sam", "battery staple")).User);
    }

    [Fact]
    public async Task Closing_sign_ups_cannot_lock_everyone_out_of_a_fresh_install()
    {
        await _accounts.SetRegistrationOpenAsync(false);

        Assert.True(await _accounts.IsRegistrationOpenAsync());
        Assert.NotNull((await _accounts.CreateAsync("mark", "correct horse")).User);
    }

    [Fact]
    public async Task A_fresh_install_has_no_users_until_one_is_created()
    {
        Assert.False(await _accounts.AnyUsersAsync());

        var (user, error) = await _accounts.CreateAsync(" mark ", "correct horse");

        Assert.Null(error);
        Assert.Equal("mark", user!.UserName);
        Assert.True(await _accounts.AnyUsersAsync());
    }

    [Fact]
    public async Task The_password_is_stored_hashed()
    {
        await _accounts.CreateAsync("mark", "correct horse");

        await using var db = _database.Open();
        Assert.DoesNotContain("correct horse", (await db.Users.SingleAsync()).PasswordHash);
    }

    [Theory]
    [InlineData("", "correct horse", "Enter a username.")]
    [InlineData("mark", "short", "Use a password of at least 8 characters.")]
    [InlineData("MARK", "correct horse", "That username is already taken.")]
    public async Task Bad_sign_up_details_are_refused(string userName, string password, string expectedError)
    {
        await _accounts.CreateAsync("mark", "correct horse");

        var (user, error) = await _accounts.CreateAsync(userName, password);

        Assert.Null(user);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task The_right_password_signs_in_whatever_the_case_of_the_username()
    {
        await _accounts.CreateAsync("mark", "correct horse");

        var (outcome, user) = await _accounts.CheckPasswordAsync("Mark", "correct horse");

        Assert.Equal(SignInOutcome.Succeeded, outcome);
        Assert.Equal("mark", user!.UserName);
    }

    [Theory]
    [InlineData("mark", "wrong horse")]
    [InlineData("mark", "Correct Horse")]
    [InlineData("nobody", "correct horse")]
    [InlineData(null, null)]
    public async Task Anything_else_does_not(string? userName, string? password)
    {
        await _accounts.CreateAsync("mark", "correct horse");

        var (outcome, user) = await _accounts.CheckPasswordAsync(userName, password);

        Assert.Equal(SignInOutcome.WrongCredentials, outcome);
        Assert.Null(user);
    }

    [Fact]
    public async Task Repeated_wrong_passwords_lock_the_account_even_against_the_right_one()
    {
        await _accounts.CreateAsync("mark", "correct horse");
        for (var attempt = 1; attempt < AccountService.MaxFailedSignIns; attempt++)
        {
            Assert.Equal(SignInOutcome.WrongCredentials, (await _accounts.CheckPasswordAsync("mark", "wrong")).Outcome);
        }

        Assert.Equal(SignInOutcome.LockedOut, (await _accounts.CheckPasswordAsync("mark", "wrong")).Outcome);
        Assert.Equal(SignInOutcome.LockedOut, (await _accounts.CheckPasswordAsync("mark", "correct horse")).Outcome);

        _clock.Advance(AccountService.LockoutDuration + TimeSpan.FromSeconds(1));
        Assert.Equal(SignInOutcome.Succeeded, (await _accounts.CheckPasswordAsync("mark", "correct horse")).Outcome);
    }

    [Fact]
    public async Task A_successful_sign_in_clears_earlier_wrong_attempts()
    {
        await _accounts.CreateAsync("mark", "correct horse");
        for (var round = 0; round < 3; round++)
        {
            for (var attempt = 1; attempt < AccountService.MaxFailedSignIns; attempt++)
            {
                await _accounts.CheckPasswordAsync("mark", "wrong");
            }

            Assert.Equal(SignInOutcome.Succeeded, (await _accounts.CheckPasswordAsync("mark", "correct horse")).Outcome);
        }
    }

    [Theory]
    [InlineData("/meal-plans/3", "/meal-plans/3")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example/", "/")]
    [InlineData("recipes", "/")]
    public void After_signing_in_people_only_go_to_pages_of_this_app(string? returnUrl, string expected)
    {
        Assert.Equal(expected, AccountService.SafeReturnUrl(returnUrl));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
