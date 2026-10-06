using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public class StoreDuplicateFinderTests
{
    private static readonly Store SamsClub = new()
    {
        Id = 1,
        Name = "Sam's Club",
        StreetAddress = "2300 West White Oaks Drive",
        City = "Springfield",
        State = "IL",
        PostalCode = "62704",
        OsmPlaceId = "W402307311"
    };

    [Fact]
    public void Same_place_id_is_a_duplicate_even_when_everything_else_differs()
    {
        var candidate = new Store { Name = "Sams", StreetAddress = "somewhere else", OsmPlaceId = "W402307311" };

        Assert.Same(SamsClub, StoreDuplicateFinder.Find(candidate, [SamsClub]));
    }

    [Theory]
    [InlineData("2300 W. White Oaks Dr", "62704")]
    [InlineData("2300 west white oaks drive", "62704-1234")]
    [InlineData("2300  West White Oaks Dr.", " 62704 ")]
    public void Same_address_typed_differently_is_a_duplicate(string street, string zip)
    {
        var candidate = new Store { Name = "Wholesale club", StreetAddress = street, PostalCode = zip };

        Assert.Same(SamsClub, StoreDuplicateFinder.Find(candidate, [SamsClub]));
    }

    [Fact]
    public void City_stands_in_when_there_is_no_zip()
    {
        var candidate = new Store { Name = "Sam's Club", StreetAddress = "2300 W White Oaks Dr", City = "springfield" };
        var existing = new Store { Id = 2, Name = "Sam's Club", StreetAddress = "2300 West White Oaks Drive", City = "Springfield" };

        Assert.Same(existing, StoreDuplicateFinder.Find(candidate, [existing]));
    }

    [Theory]
    [InlineData("2301 West White Oaks Drive", "62704")]
    [InlineData("2300 West White Oaks Drive", "65807")]
    [InlineData("2300 East White Oaks Drive", "62704")]
    public void A_different_number_street_or_town_is_not_a_duplicate(string street, string zip)
    {
        var candidate = new Store { Name = "Sam's Club", StreetAddress = street, PostalCode = zip };

        Assert.Null(StoreDuplicateFinder.Find(candidate, [SamsClub]));
    }

    [Fact]
    public void Stores_without_an_address_are_never_duplicates_of_each_other()
    {
        var candidate = new Store { Name = "Sam's Club" };
        var existing = new Store { Id = 2, Name = "Sam's Club" };

        Assert.Null(StoreDuplicateFinder.Find(candidate, [existing]));
    }

    [Fact]
    public void A_street_with_no_number_is_too_vague_to_match_on()
    {
        var candidate = new Store { Name = "Sam's Club", StreetAddress = "West White Oaks Drive", PostalCode = "62704" };
        var existing = new Store { Id = 2, Name = "Gas station", StreetAddress = "West White Oaks Drive", PostalCode = "62704" };

        Assert.Null(StoreDuplicateFinder.Find(candidate, [existing]));
    }

    [Fact]
    public void A_store_is_not_a_duplicate_of_itself_when_edited()
    {
        var edited = new Store { Id = 1, Name = "Sam's Club", StreetAddress = "2300 W White Oaks Dr", PostalCode = "62704", OsmPlaceId = "W402307311" };

        Assert.Null(StoreDuplicateFinder.Find(edited, [SamsClub]));
    }
}
