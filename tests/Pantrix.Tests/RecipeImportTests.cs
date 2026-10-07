using System.Net;
using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public class RecipeImportTests
{
    [Theory]
    [InlineData("2 cups all-purpose flour, sifted", 2, Unit.Cup, "all-purpose flour", "sifted")]
    [InlineData("1 1/2 tsp salt", 1.5, Unit.Teaspoon, "salt", null)]
    [InlineData("1½ cups milk", 1.5, Unit.Cup, "milk", null)]
    [InlineData("¾ cup sugar", 0.75, Unit.Cup, "sugar", null)]
    [InlineData("1 (15 ounce) can black beans, drained", 1, Unit.Can, "black beans", "drained; 15 ounce")]
    [InlineData("3 eggs", 3, Unit.Each, "eggs", null)]
    [InlineData("2-3 cloves garlic, minced", 2, Unit.Clove, "garlic", "minced")]
    [InlineData("1 lb. ground beef", 1, Unit.Pound, "ground beef", null)]
    [InlineData("8 fl oz heavy cream", 8, Unit.FluidOunce, "heavy cream", null)]
    [InlineData("2 tablespoons of olive oil", 2, Unit.Tablespoon, "olive oil", null)]
    [InlineData("Salt and pepper to taste", 1, Unit.Each, "Salt and pepper to taste", null)]
    [InlineData("- 1 box penne pasta", 1, Unit.Box, "penne pasta", null)]
    [InlineData("0.5 kg potatoes (peeled)", 0.5, Unit.Kilogram, "potatoes", "peeled")]
    public void Ingredient_lines_are_read_into_amount_unit_name_and_note(string line, double quantity, Unit unit, string name, string? note)
    {
        Assert.Equal(new ParsedIngredient((decimal)quantity, unit, name, note), IngredientLineParser.Parse(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("For the sauce:")]
    [InlineData(null)]
    public void Blank_lines_and_headings_are_not_ingredients(string? line)
    {
        Assert.Null(IngredientLineParser.Parse(line));
    }

    private static readonly Ingredient[] Mine =
    [
        new() { Id = 1, Name = "Penne Pasta" },
        new() { Id = 2, Name = "Elbow Pasta" },
        new() { Id = 3, Name = "Ground Beef" },
        new() { Id = 4, Name = "Tomato" },
        new() { Id = 5, Name = "Shredded Cheddar Cheese" }
    ];

    [Theory]
    [InlineData("penne pasta", "Penne Pasta")]
    [InlineData("tomatoes", "Tomato")]
    [InlineData("fresh tomatoes, chopped", "Tomato")]
    [InlineData("lean ground beef", null)]
    [InlineData("pasta", null)]
    [InlineData("cheddar cheese", "Shredded Cheddar Cheese")]
    [InlineData("saffron", null)]
    public void Only_an_unmistakable_match_is_treated_as_the_same_ingredient(string name, string? expected)
    {
        Assert.Equal(expected, IngredientMatcher.Match(name, Mine).Exact?.Name);
    }

    [Fact]
    public void A_name_shared_by_two_of_mine_is_left_for_a_person_to_choose()
    {
        var match = IngredientMatcher.Match("pasta", Mine);

        Assert.Null(match.Exact);
        Assert.Equal(["Elbow Pasta", "Penne Pasta"], match.Similar.Select(i => i.Name));
    }

    [Fact]
    public void Look_alikes_are_suggested_most_alike_first_and_strangers_are_not()
    {
        Assert.Equal(["Ground Beef"], IngredientMatcher.Match("lean ground beef", Mine).Similar.Select(i => i.Name));
        Assert.Empty(IngredientMatcher.Match("saffron", Mine).Similar);
    }

    [Fact]
    public void A_recipe_is_read_from_a_pages_standard_data_block()
    {
        const string html = """
            <html><head>
            <script type="application/ld+json">{"@context":"https://schema.org","@type":"WebSite","name":"Some Site"}</script>
            <script type='application/ld+json'>
            {"@context":"https://schema.org","@graph":[
              {"@type":"BreadcrumbList"},
              {"@type":["Recipe","NewsArticle"],"name":"Mom&#39;s Penne Bake","description":"<p>Weeknight   favorite.</p>",
               "recipeYield":["6","6 servings"],"prepTime":"PT15M","cookTime":"PT1H5M",
               "recipeIngredient":["1 box penne pasta","1 lb ground beef"," "],
               "recipeInstructions":[
                 {"@type":"HowToSection","name":"Prep","itemListElement":[{"@type":"HowToStep","text":"Boil the pasta."}]},
                 {"@type":"HowToStep","text":"Brown the beef."},
                 "Bake."]}
            ]}
            </script></head><body></body></html>
            """;

        var recipe = RecipeImportService.ParseHtml(html, "https://example.com/penne");

        Assert.NotNull(recipe);
        Assert.Equal("Mom's Penne Bake", recipe.Name);
        Assert.Equal("Weeknight favorite.", recipe.Description);
        Assert.Equal(6, recipe.Servings);
        Assert.Equal(15, recipe.PrepMinutes);
        Assert.Equal(65, recipe.CookMinutes);
        Assert.Equal(["1 box penne pasta", "1 lb ground beef"], recipe.IngredientLines);
        Assert.Equal("Boil the pasta.\nBrown the beef.\nBake.", recipe.Instructions);
        Assert.Equal("https://example.com/penne", recipe.SourceUrl);
    }

    [Theory]
    [InlineData("<html><body>No data here</body></html>")]
    [InlineData("""<script type="application/ld+json">{"@type":"Article","name":"Not a recipe"}</script>""")]
    [InlineData("""<script type="application/ld+json">{"@type":"Recipe","name":"No ingredients"}</script>""")]
    [InlineData("""<script type="application/ld+json">{ this is not json </script>""")]
    public void A_page_without_a_usable_recipe_gives_nothing(string html)
    {
        Assert.Null(RecipeImportService.ParseHtml(html, null));
    }

    [Theory]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.20.0.10", false)]
    [InlineData("192.168.1.131", false)]
    [InlineData("172.17.0.2", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:192.168.1.1", false)]
    public void Pages_are_only_fetched_from_the_public_internet(string address, bool expected)
    {
        Assert.Equal(expected, RecipeImportService.IsPublic(IPAddress.Parse(address)));
    }
}
