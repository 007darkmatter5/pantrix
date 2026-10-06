using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public sealed class KitchenServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new(kitchens: 0);
    private readonly KitchenService _kitchens;
    private AppUser _mark = null!;
    private AppUser _sam = null!;
    private AppUser _alex = null!;

    public KitchenServiceTests()
    {
        _kitchens = new KitchenService(_database.InKitchen(0));
    }

    public async Task InitializeAsync()
    {
        var accounts = new AccountService(_database.InKitchen(0), TimeProvider.System);
        _mark = (await accounts.CreateAsync("mark", "correct horse")).User!;
        _sam = (await accounts.CreateAsync("sam", "battery staple")).User!;
        _alex = (await accounts.CreateAsync("alex", "purple monkey")).User!;
    }

    public Task DisposeAsync()
    {
        _database.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Joining_with_the_code_moves_you_into_that_kitchen_and_its_data()
    {
        await using (var marks = _database.Open(_mark.KitchenId))
        {
            marks.Ingredients.Add(new Ingredient { Name = "Flour" });
            await marks.SaveChangesAsync();
        }

        var error = await _kitchens.JoinAsync(_sam.Id, (await JoinCodeAsync(_mark)).ToLowerInvariant() + " ");

        Assert.Null(error);
        var info = await _kitchens.GetAsync(_sam.Id);
        Assert.Equal("mark's kitchen", info!.Kitchen.Name);
        Assert.False(info.IsOwner);
        Assert.Equal(["mark", "sam"], info.Members.Select(m => m.UserName));
        await using var sams = _database.Open(await KitchenOfAsync(_sam));
        Assert.Equal("Flour", (await sams.Ingredients.SingleAsync()).Name);
    }

    [Theory]
    [InlineData("NOSUCHCD")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_wrong_code_joins_nothing(string? code)
    {
        var error = await _kitchens.JoinAsync(_sam.Id, code);

        Assert.Equal("No kitchen has that join code. Check it with the kitchen's owner.", error);
        Assert.Equal(_sam.KitchenId, await KitchenOfAsync(_sam));
    }

    [Fact]
    public async Task Leaving_returns_you_to_your_own_kitchen_with_its_data_as_you_left_it()
    {
        await using (var sams = _database.Open(_sam.KitchenId))
        {
            sams.Ingredients.Add(new Ingredient { Name = "Rice" });
            await sams.SaveChangesAsync();
        }

        await _kitchens.JoinAsync(_sam.Id, await JoinCodeAsync(_mark));
        await _kitchens.LeaveAsync(_sam.Id);

        Assert.Equal(_sam.KitchenId, await KitchenOfAsync(_sam));
        await using var back = _database.Open(_sam.KitchenId);
        Assert.Equal("Rice", (await back.Ingredients.SingleAsync()).Name);
    }

    [Fact]
    public async Task The_owner_can_remove_a_guest_who_goes_back_to_their_own_kitchen()
    {
        await _kitchens.JoinAsync(_sam.Id, await JoinCodeAsync(_mark));

        Assert.Null(await _kitchens.RemoveMemberAsync(_mark.Id, _sam.Id));

        Assert.Equal(_sam.KitchenId, await KitchenOfAsync(_sam));
        Assert.Equal(["mark"], (await _kitchens.GetAsync(_mark.Id))!.Members.Select(m => m.UserName));
    }

    [Fact]
    public async Task A_guest_cannot_remove_people_rename_the_kitchen_or_change_its_code()
    {
        await _kitchens.JoinAsync(_sam.Id, await JoinCodeAsync(_mark));
        await _kitchens.JoinAsync(_alex.Id, await JoinCodeAsync(_mark));
        var code = await JoinCodeAsync(_mark);

        Assert.NotNull(await _kitchens.RemoveMemberAsync(_sam.Id, _alex.Id));
        Assert.NotNull(await _kitchens.RemoveMemberAsync(_sam.Id, _mark.Id));
        Assert.NotNull(await _kitchens.RenameAsync(_sam.Id, "Sam's now"));
        Assert.NotNull(await _kitchens.RegenerateJoinCodeAsync(_sam.Id));

        var info = await _kitchens.GetAsync(_mark.Id);
        Assert.Equal("mark's kitchen", info!.Kitchen.Name);
        Assert.Equal(code, info.Kitchen.JoinCode);
        Assert.Equal(["alex", "mark", "sam"], info.Members.Select(m => m.UserName));
    }

    [Fact]
    public async Task An_owner_cannot_remove_themselves_or_someone_who_is_not_in_their_kitchen()
    {
        Assert.NotNull(await _kitchens.RemoveMemberAsync(_mark.Id, _mark.Id));
        Assert.NotNull(await _kitchens.RemoveMemberAsync(_mark.Id, _sam.Id));

        Assert.Equal(_sam.KitchenId, await KitchenOfAsync(_sam));
    }

    [Fact]
    public async Task A_new_join_code_stops_the_old_one_working_but_keeps_current_guests()
    {
        var oldCode = await JoinCodeAsync(_mark);
        await _kitchens.JoinAsync(_sam.Id, oldCode);

        Assert.Null(await _kitchens.RegenerateJoinCodeAsync(_mark.Id));

        Assert.NotEqual(oldCode, await JoinCodeAsync(_mark));
        Assert.NotNull(await _kitchens.JoinAsync(_alex.Id, oldCode));
        Assert.Equal(_mark.KitchenId, await KitchenOfAsync(_sam));
    }

    [Fact]
    public async Task The_owner_can_rename_their_kitchen()
    {
        Assert.Null(await _kitchens.RenameAsync(_mark.Id, "  Lalich house  "));
        Assert.Equal("Enter a name for the kitchen.", await _kitchens.RenameAsync(_mark.Id, " "));

        Assert.Equal("Lalich house", (await _kitchens.GetAsync(_mark.Id))!.Kitchen.Name);
    }

    private async Task<string> JoinCodeAsync(AppUser owner)
    {
        await using var db = _database.Open();
        return (await db.Kitchens.SingleAsync(k => k.OwnerId == owner.Id)).JoinCode;
    }

    private async Task<int> KitchenOfAsync(AppUser user)
    {
        await using var db = _database.Open();
        return (await db.Users.SingleAsync(u => u.Id == user.Id)).KitchenId;
    }
}
