using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Tests;

/// <summary>A real, migrated SQLite database in memory, with kitchens numbered from 1 ready to hold data.</summary>
internal sealed class TestDatabase : IDisposable
{
    // The in-memory database lives only as long as this connection stays open.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase(int kitchens = 1)
    {
        _connection.Open();
        Options = new DbContextOptionsBuilder<PantrixDbContext>().UseSqlite(_connection).Options;

        using var db = Open();
        db.Database.Migrate();
        for (var id = 1; id <= kitchens; id++)
        {
            db.Kitchens.Add(new Kitchen { Id = id, Name = $"Kitchen {id}", JoinCode = $"CODE{id:0000}" });
        }

        db.SaveChanges();
    }

    public DbContextOptions<PantrixDbContext> Options { get; }

    /// <summary>A context working in the given kitchen; 0 is no kitchen, which sees accounts and kitchens only.</summary>
    public PantrixDbContext Open(int kitchenId = 0) => new(Options) { KitchenId = kitchenId };

    /// <summary>What a service is given: a factory whose contexts all work in one kitchen.</summary>
    public IDbContextFactory<PantrixDbContext> InKitchen(int kitchenId) => new Factory(this, kitchenId);

    public void Dispose() => _connection.Dispose();

    private sealed class Factory(TestDatabase database, int kitchenId) : IDbContextFactory<PantrixDbContext>
    {
        public PantrixDbContext CreateDbContext() => database.Open(kitchenId);
    }
}
