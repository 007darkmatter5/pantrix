using Pantrix.Data;

namespace Pantrix.Tests;

public class StoreTests
{
    [Fact]
    public void Display_name_is_store_then_city_state_and_location_in_parentheses()
    {
        var store = new Store { Name = "H-E-B", City = "San Marcos", State = "TX", Location = "Big HEB" };

        Assert.Equal("H-E-B - San Marcos TX (Big HEB)", store.DisplayName);
    }

    [Fact]
    public void Display_name_does_not_double_parentheses_already_typed()
    {
        var store = new Store { Name = "H-E-B", City = "San Marcos", State = "TX", Location = "(Big HEB)" };

        Assert.Equal("H-E-B - San Marcos TX (Big HEB)", store.DisplayName);
    }

    [Fact]
    public void Display_name_leaves_out_missing_parts()
    {
        Assert.Equal("Sam's Club - (4958)", new Store { Name = "Sam's Club", Location = "4958" }.DisplayName);
        Assert.Equal("Sam's Club - San Marcos TX", new Store { Name = "Sam's Club", City = "San Marcos", State = "TX" }.DisplayName);
        Assert.Equal("Sam's Club", new Store { Name = "Sam's Club" }.DisplayName);
    }
}
