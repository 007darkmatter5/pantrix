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
    [InlineData("8, A8", "8, A10")]
    [InlineData("Meat Market on the Back Wall, A29", "Meat Market on the Back Wall, A30")]
    [InlineData("Produce", "Produce, A7")]
    public void Spots_in_the_same_area_belong_together(string one, string another)
    {
        Assert.Equal(AisleOrder.Key(one), AisleOrder.Key(another));
    }

    [Fact]
    public void Within_an_area_spots_sort_by_their_number_not_as_text()
    {
        string[] aisles = ["8, A10", "8, B2", "8, A8", "8", "8, A9"];

        Assert.Equal(["8", "8, A8", "8, A9", "8, A10", "8, B2"], aisles.OrderBy(AisleOrder.SpotKey));
    }

    [Theory]
    [InlineData("12", "Aisle 12")]
    [InlineData("12B", "Aisle 12B")]
    [InlineData("8, A8", "Aisle 8")]
    [InlineData("Bakery", "Bakery")]
    [InlineData("Meat Market on the Back Wall, A29", "Meat Market on the Back Wall")]
    public void An_areas_heading_leaves_out_the_spot(string aisle, string expected)
    {
        Assert.Equal(expected, AisleOrder.Title(aisle));
    }

    [Theory]
    [InlineData("8, A8", "Aisle 8, A8")]
    [InlineData("Meat Market on the Back Wall, A29", "Meat Market on the Back Wall, A29")]
    public void A_full_location_keeps_the_spot(string aisle, string expected)
    {
        Assert.Equal(expected, AisleOrder.Describe(aisle));
    }
}
