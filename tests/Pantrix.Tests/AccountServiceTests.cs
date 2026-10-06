using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public sealed class AccountServiceTests : IDisposable
{
    // The in-memory database lives only as long as this connection stays open.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<PantrixDbContext> _options;
    private readonly Clock _clock = new();
    private readonly AccountService _accounts;

    public AccountServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<PantrixDbContext>().UseSqlite(_connection).Options;
        _accounts = new AccountService(new Factory(_options), _clock);

        using var db = new PantrixDbContext(_options);
        db.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

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

        await using var db = new PantrixDbContext(_options);
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

    private sealed class Factory(DbContextOptions<PantrixDbContext> options) : IDbContextFactory<PantrixDbContext>
    {
        public PantrixDbContext CreateDbContext() => new(options);
    }
}
