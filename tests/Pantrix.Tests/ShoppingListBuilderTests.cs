using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public class ShoppingListBuilderTests
{
    private const int Flour = 1;
    private const int Milk = 2;
    private const int Beans = 3;
    private const int Penne = 4;
    private const int Elbow = 5;

    [Fact]
    public void Lists_full_amount_when_nothing_is_on_hand()
    {
        var result = Compute([new(Flour, 2, Unit.Cup)], []);

        Assert.Equal(["1: 2 Cup"], result);
    }

    [Fact]
    public void Sums_the_same_ingredient_across_recipes_in_the_first_unit_seen()
    {
        var result = Compute([new(Milk, 1, Unit.Cup), new(Milk, 8, Unit.FluidOunce)], []);

        Assert.Equal(["2: 2 Cup"], result);
    }

    [Fact]
    public void Subtracts_inventory_held_in_a_different_unit_of_the_same_kind()
    {
        var result = Compute([new(Flour, 1, Unit.Kilogram)], [new(Flour, 250, Unit.Gram)]);

        Assert.Equal(["1: 0.75 Kilogram"], result);
    }

    [Fact]
    public void Omits_ingredients_that_are_fully_covered()
    {
        var result = Compute([new(Milk, 1, Unit.Cup)], [new(Milk, 1, Unit.Quart)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Does_not_offset_volume_needs_with_weight_stock()
    {
        var result = Compute([new(Flour, 2, Unit.Cup)], [new(Flour, 5, Unit.Pound)]);

        Assert.Equal(["1: 2 Cup"], result);
    }

    [Fact]
    public void Count_units_only_match_themselves()
    {
        var result = Compute([new(Beans, 2, Unit.Can)], [new(Beans, 1, Unit.Can), new(Beans, 5, Unit.Package)]);

        Assert.Equal(["3: 1 Can"], result);
    }

    [Fact]
    public void Other_ingredients_stock_is_ignored()
    {
        var result = Compute([new(Flour, 1, Unit.Cup)], [new(Milk, 10, Unit.Cup)]);

        Assert.Equal(["1: 1 Cup"], result);
    }

    [Fact]
    public void An_alternative_in_stock_satisfies_the_need()
    {
        var result = Compute([new(Penne, 1, Unit.Box, [Elbow])], [new(Elbow, 1, Unit.Box)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Stock_of_the_ingredient_and_its_alternatives_is_combined()
    {
        var result = Compute([new(Penne, 3, Unit.Box, [Elbow])], [new(Penne, 1, Unit.Box), new(Elbow, 1, Unit.Box)]);

        Assert.Equal(["4 or 5: 1 Box"], result);
    }

    [Fact]
    public void Lists_the_alternatives_when_none_are_in_stock()
    {
        var result = Compute([new(Penne, 1, Unit.Box, [Elbow])], []);

        Assert.Equal(["4 or 5: 1 Box"], result);
    }

    [Fact]
    public void Stock_used_by_one_recipe_is_not_counted_again_for_another()
    {
        var result = Compute(
            [new(Penne, 1, Unit.Box, [Elbow]), new(Elbow, 1, Unit.Box, [Penne])],
            [new(Elbow, 1, Unit.Box)]);

        Assert.Equal(["5 or 4: 1 Box"], result);
    }

    [Fact]
    public void An_either_or_need_leaves_stock_for_a_need_with_no_substitute()
    {
        var result = Compute(
            [new(Penne, 1, Unit.Box, [Elbow]), new(Penne, 1, Unit.Box)],
            [new(Penne, 1, Unit.Box), new(Elbow, 1, Unit.Box)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Tops_up_to_the_minimum_when_stock_is_below_it()
    {
        var result = Compute([], [new(Milk, 1, Unit.Cup)], [new(Milk, 1, Unit.Quart)]);

        Assert.Equal(["2: 0.75 Quart"], result);
    }

    [Fact]
    public void Ignores_the_minimum_when_stock_meets_it()
    {
        var result = Compute([], [new(Beans, 2, Unit.Can)], [new(Beans, 2, Unit.Can)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Minimum_is_checked_against_current_stock_not_what_the_recipes_will_leave()
    {
        var result = Compute([new(Beans, 2, Unit.Can)], [new(Beans, 2, Unit.Can)], [new(Beans, 2, Unit.Can)]);

        Assert.Empty(result);
    }

    [Fact]
    public void Recipe_shortfall_and_minimum_are_not_added_together()
    {
        var result = Compute([new(Beans, 3, Unit.Can)], [new(Beans, 1, Unit.Can)], [new(Beans, 2, Unit.Can)]);

        Assert.Equal(["3: 2 Can"], result);
    }

    [Fact]
    public void Minimum_raises_a_smaller_recipe_shortfall()
    {
        var result = Compute([new(Beans, 1, Unit.Can)], [], [new(Beans, 3, Unit.Can)]);

        Assert.Equal(["3: 3 Can"], result);
    }

    [Fact]
    public void Minimum_is_covered_by_an_either_or_line_that_includes_the_ingredient()
    {
        var result = Compute([new(Elbow, 2, Unit.Box, [Penne])], [], [new(Penne, 1, Unit.Box)]);

        Assert.Equal(["5 or 4: 2 Box"], result);
    }

    // Renders each line as "ingredient[ or alternative...]: quantity unit" so expectations stay readable.
    private static List<string> Compute(
        IngredientNeed[] needed, IngredientAmount[] onHand, IngredientAmount[]? minimums = null) =>
        ShoppingListBuilder.ComputeShortfall(needed, onHand, minimums)
            .Select(n => $"{string.Join(" or ", n.Alternatives.Prepend(n.IngredientId))}: {n.Quantity:0.##} {n.Unit}")
            .ToList();
}
