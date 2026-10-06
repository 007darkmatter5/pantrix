using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public sealed class StoreAisleServiceTests : IDisposable
{
    // The in-memory database lives only as long as this connection stays open.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<PantrixDbContext> _options;
    private readonly StoreAisleService _service;
    private readonly int _storeId;
    private readonly int _ingredientId;

    public StoreAisleServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<PantrixDbContext>().UseSqlite(_connection).Options;
        _service = new StoreAisleService(new Factory(_options));

        using var db = new PantrixDbContext(_options);
        db.Database.Migrate();
        var store = new Store { Name = "H-E-B" };
        var ingredient = new Ingredient { Name = "Breakfast Sausage" };
        db.AddRange(store, ingredient);
        db.SaveChanges();
        _storeId = store.Id;
        _ingredientId = ingredient.Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Aisle_and_product_page_are_kept_on_one_record()
    {
        await _service.SetAsync(_storeId, _ingredientId, " 12 ");
        await _service.SetProductUrlAsync(_storeId, _ingredientId, "https://www.heb.com/product/1");

        var record = Assert.Single(await RecordsAsync());
        Assert.Equal("12", record.Aisle);
        Assert.Equal("https://www.heb.com/product/1", record.ProductUrl);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), record.LastConfirmed);
    }

    [Fact]
    public async Task Clearing_the_aisle_keeps_the_record_while_it_still_has_a_product_page()
    {
        await _service.SetAsync(_storeId, _ingredientId, "12");
        await _service.SetProductUrlAsync(_storeId, _ingredientId, "https://www.heb.com/product/1");

        await _service.SetAsync(_storeId, _ingredientId, "");

        var record = Assert.Single(await RecordsAsync());
        Assert.Equal("", record.Aisle);
        Assert.Equal("https://www.heb.com/product/1", record.ProductUrl);
    }

    [Fact]
    public async Task The_record_goes_once_both_are_cleared()
    {
        await _service.SetAsync(_storeId, _ingredientId, "12");
        await _service.SetProductUrlAsync(_storeId, _ingredientId, "https://www.heb.com/product/1");

        await _service.SetProductUrlAsync(_storeId, _ingredientId, null);
        await _service.SetAsync(_storeId, _ingredientId, null);

        Assert.Empty(await RecordsAsync());
    }

    [Fact]
    public async Task Clearing_something_that_was_never_recorded_creates_nothing()
    {
        await _service.SetAsync(_storeId, _ingredientId, " ");
        await _service.SetProductUrlAsync(_storeId, _ingredientId, "");

        Assert.Empty(await RecordsAsync());
    }

    [Theory]
    [InlineData("https://www.heb.com/product/1", true)]
    [InlineData("http://example.com", true)]
    [InlineData("www.heb.com/product/1", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_full_web_addresses_count_as_links(string? url, bool expected)
    {
        Assert.Equal(expected, StoreAisleService.IsWebLink(url));
    }

    private async Task<List<StoreAisle>> RecordsAsync()
    {
        await using var db = new PantrixDbContext(_options);
        return await db.StoreAisles.AsNoTracking().ToListAsync();
    }

    private sealed class Factory(DbContextOptions<PantrixDbContext> options) : IDbContextFactory<PantrixDbContext>
    {
        public PantrixDbContext CreateDbContext() => new(options);
    }
}
