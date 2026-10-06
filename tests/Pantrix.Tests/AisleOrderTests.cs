using Pantrix.Services;

namespace Pantrix.Tests;

public class AisleOrderTests
{
    [Fact]
    public void Numbered_aisles_sort_by_number_then_named_areas_alphabetically()
    {
        string[] aisles = ["Produce", "12", "2", "Bakery", "12B", "1"];

        Assert.Equal(["1", "2", "12", "12B", "Bakery", "Produce"], aisles.OrderBy(AisleOrder.Key));
    }

    [Fact]
    public void Aisles_that_differ_only_in_case_or_spacing_are_the_same()
    {
        Assert.Equal(AisleOrder.Key("bakery"), AisleOrder.Key(" Bakery "));
    }

    [Theory]
    [InlineData("12", "Aisle 12")]
    [InlineData("12B", "Aisle 12B")]
    [InlineData("Bakery", "Bakery")]
    public void Numbered_aisles_are_titled_as_aisles(string aisle, string expected)
    {
        Assert.Equal(expected, AisleOrder.Title(aisle));
    }
}
