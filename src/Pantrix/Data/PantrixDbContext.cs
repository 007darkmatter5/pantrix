using Microsoft.EntityFrameworkCore;

namespace Pantrix.Data;

public class PantrixDbContext(DbContextOptions<PantrixDbContext> options) : DbContext(options)
{
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
        modelBuilder.Entity<Ingredient>(e =>
        {
            e.Property(i => i.Name).UseCollation("NOCASE");
            e.HasIndex(i => i.Name).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.UserName).UseCollation("NOCASE");
            e.HasIndex(u => u.UserName).IsUnique();
        });

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
}
