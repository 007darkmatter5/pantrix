using Microsoft.EntityFrameworkCore;

namespace Pantrix.Data;

public class PantrixDbContext(DbContextOptions<PantrixDbContext> options) : DbContext(options)
{
    /// <summary>
    /// The kitchen this context works in. Every query for kitchen data is limited to it, and everything saved
    /// is put in it, so one kitchen can never read or change another's. Left at 0 (no kitchen) a context sees
    /// no kitchen data at all; accounts, kitchens and settings are not limited.
    /// </summary>
    public int KitchenId { get; init; }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeIngredientAlternative> RecipeIngredientAlternatives => Set<RecipeIngredientAlternative>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreAisle> StoreAisles => Set<StoreAisle>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Kitchen> Kitchens => Set<Kitchen>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        KeepChangesInKitchen();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        KeepChangesInKitchen();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // New records go into this context's kitchen, and so do records saved from a copy that never carried a
    // kitchen (dialogs save a fresh object with just the id). Anything already marked for another kitchen is refused.
    private void KeepChangesInKitchen()
    {
        foreach (var entry in ChangeTracker.Entries<IKitchenOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (KitchenId == 0)
            {
                throw new InvalidOperationException("Kitchen data can't be saved without a kitchen.");
            }

            if (entry.Entity.KitchenId == 0)
            {
                entry.Entity.KitchenId = KitchenId;
            }
            else if (entry.Entity.KitchenId != KitchenId)
            {
                throw new InvalidOperationException("That record belongs to a different kitchen.");
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Stored as text so enum members can be reordered or inserted without corrupting existing rows.
        configurationBuilder.Properties<Unit>().HaveConversion<string>();
        configurationBuilder.Properties<IngredientCategory>().HaveConversion<string>();
        configurationBuilder.Properties<StorageLocation>().HaveConversion<string>();
        configurationBuilder.Properties<MealType>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Each kitchen-owned record is deleted with its kitchen and only visible from inside it.
        Owned<Ingredient>(modelBuilder).HasQueryFilter(e => e.KitchenId == KitchenId);
        Owned<Recipe>(modelBuilder).HasQueryFilter(e => e.KitchenId == KitchenId);
        Owned<MealPlan>(modelBuilder).HasQueryFilter(e => e.KitchenId == KitchenId);
        Owned<ShoppingList>(modelBuilder).HasQueryFilter(e => e.KitchenId == KitchenId);
        Owned<Store>(modelBuilder).HasQueryFilter(e => e.KitchenId == KitchenId);

        // Records that hang off those are limited through the one they belong to, so that querying them
        // directly (all automatic shopping items, all upcoming meals) stays inside the kitchen too.
        modelBuilder.Entity<InventoryItem>().HasQueryFilter(e => e.Ingredient.KitchenId == KitchenId);
        modelBuilder.Entity<RecipeIngredient>().HasQueryFilter(e => e.Ingredient.KitchenId == KitchenId);
        modelBuilder.Entity<RecipeIngredientAlternative>().HasQueryFilter(e => e.Ingredient.KitchenId == KitchenId);
        modelBuilder.Entity<StoreAisle>().HasQueryFilter(e => e.Ingredient.KitchenId == KitchenId);
        modelBuilder.Entity<MealPlanEntry>().HasQueryFilter(e => e.MealPlan.KitchenId == KitchenId);
        modelBuilder.Entity<ShoppingListItem>().HasQueryFilter(e => e.ShoppingList.KitchenId == KitchenId);

        modelBuilder.Entity<Ingredient>(e =>
        {
            e.Property(i => i.Name).UseCollation("NOCASE");
            e.HasIndex(i => new { i.KitchenId, i.Name }).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.UserName).UseCollation("NOCASE");
            e.HasIndex(u => u.UserName).IsUnique();

            // A kitchen with people still working in it can't be deleted from under them.
            e.HasOne(u => u.Kitchen).WithMany().HasForeignKey(u => u.KitchenId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Kitchen>(e =>
        {
            e.HasOne(k => k.Owner).WithMany().HasForeignKey(k => k.OwnerId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(k => k.JoinCode).IsUnique();
        });

        modelBuilder.Entity<AppSetting>().HasKey(s => s.Key);

        // An ingredient that a recipe depends on can't be deleted out from under it.
        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Ingredient).WithMany()
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecipeIngredient>()
            .HasMany(ri => ri.Alternatives).WithOne()
            .HasForeignKey(a => a.RecipeIngredientId);

        // Deleting a recipe or ingredient takes its planned meals with it instead of leaving them looking like eating out.
        modelBuilder.Entity<MealPlanEntry>()
            .HasOne(e => e.Recipe).WithMany()
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MealPlanEntry>()
            .HasOne(e => e.Ingredient).WithMany()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShoppingListItem>()
            .HasOne(i => i.Ingredient).WithMany()
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShoppingList>()
            .HasOne(l => l.MealPlan).WithMany()
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShoppingList>()
            .HasOne(l => l.Store).WithMany()
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StoreAisle>()
            .HasIndex(a => new { a.StoreId, a.IngredientId }).IsUnique();
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> Owned<T>(ModelBuilder modelBuilder)
        where T : class, IKitchenOwned
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasOne<Kitchen>().WithMany().HasForeignKey(e => e.KitchenId).OnDelete(DeleteBehavior.Cascade);
        return entity;
    }
}
