using Pantrix.Data;
using Pantrix.Services;

namespace Pantrix.Tests;

public class StoreCatalogTests
{
    [Theory]
    [InlineData("H-E-B", "H-E-B")]
    [InlineData("HEB", "H-E-B")]
    [InlineData("h.e.b.", "H-E-B")]
    [InlineData("H-E-B plus!", "H-E-B")]
    [InlineData("Sam's Club", "Sam's Club")]
    [InlineData("Sams", "Sam's Club")]
    [InlineData("SAMS CLUB #4958", "Sam's Club")]
    public void A_store_is_matched_to_its_chain_however_the_name_is_typed(string storeName, string chain)
    {
        Assert.Equal(chain, StoreCatalog.Find(storeName)?.Name);
    }

    [Theory]
    [InlineData("https://www.heb.com/search?q=milk", "https://www.heb.com/search?q={item}")]
    [InlineData("https://www.target.com/s?searchTerm=Milk&category=0", "https://www.target.com/s?searchTerm={item}&category=0")]
    [InlineData("  https://shop.example.com/search/milk  ", "https://shop.example.com/search/{item}")]
    public void A_pasted_results_page_becomes_a_reusable_search_address(string pasted, string expected)
    {
        Assert.Equal(expected, StoreCatalog.ToSearchAddress(pasted));
    }

    [Theory]
    [InlineData("https://www.heb.com/search?q=eggs")]
    [InlineData("http://www.heb.com/search?q=milk")]
    [InlineData("milk")]
    [InlineData("")]
    [InlineData(null)]
    public void A_pasted_address_without_the_sample_word_or_not_https_is_refused(string? pasted)
    {
        Assert.Null(StoreCatalog.ToSearchAddress(pasted));
    }

    private static readonly (string Href, string Text)[] SanMarcosStores =
    [
        ("https://www.heb.com/heb-store/tx/san-marcos/west-hopkins-h-e-b-455", "West Hopkins H‑E‑B\n200 WEST HOPKINS ST.\nSan Marcos, TX 78666"),
        ("https://www.heb.com/heb-store/tx/san-marcos/east-hopkins-h-e-b-243", "East Hopkins H‑E‑B\n641 EAST HOPKINS STREET\nSan Marcos, TX 78666"),
        ("https://www.heb.com/heb-store/tx/san-marcos/mccarty-lane-h-e-b-822", "McCarty Lane H‑E‑B\n1202 McCarty Ln\nSan Marcos, TX 78666"),
        ("https://www.heb.com/heb-store/tx/kyle/kyle-h-e-b-plus--14/", "Kyle H‑E‑B plus!\n5401 S FM 1626\nKyle, TX 78640")
    ];

    [Theory]
    [InlineData("200 West Hopkins Street", "455", "West Hopkins H‑E‑B")]
    [InlineData("641 E. Hopkins St", "243", "East Hopkins H‑E‑B")]
    [InlineData("1202 mccarty lane", "822", "McCarty Lane H‑E‑B")]
    [InlineData("5401 South FM 1626", "14", "Kyle H‑E‑B plus!")]
    public void A_store_is_matched_to_its_location_on_the_store_finder_by_street_address(string street, string id, string label)
    {
        Assert.Equal((id, label), StoreCatalog.MatchLocation(street, SanMarcosStores));
    }

    [Theory]
    [InlineData("300 West Hopkins Street")]
    [InlineData("200 Main Street")]
    [InlineData("West Hopkins Street")]
    [InlineData("200")]
    [InlineData(null)]
    public void An_address_that_is_not_on_the_store_finder_matches_nothing(string? street)
    {
        Assert.Null(StoreCatalog.MatchLocation(street, SanMarcosStores));
    }

    [Theory]
    [InlineData(" 455 ", "455")]
    [InlineData("T-2412", "T-2412")]
    [InlineData("455; path=/", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_location_number_is_only_passed_to_a_website_when_it_is_plain(string? typed, string? expected)
    {
        Assert.Equal(expected, StoreCatalog.CleanLocationId(typed));
    }

    [Theory]
    [InlineData("Target", "Target")]
    [InlineData("Super Target", "Target")]
    public void Target_is_a_known_chain(string storeName, string chain)
    {
        Assert.Equal(chain, StoreCatalog.Find(storeName)?.Name);
    }

    [Theory]
    [InlineData("Corner Market")]
    [InlineData("Whole Foods")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_store_matches_nothing(string? storeName)
    {
        Assert.Null(StoreCatalog.Find(storeName));
    }

    [Fact]
    public void A_chain_with_browser_lookup_comes_with_a_usable_search_address()
    {
        foreach (var chain in StoreCatalog.Chains.Where(c => c.Source == AisleSource.Browser))
        {
            Assert.NotNull(StoreCatalog.BuildSearchUri(chain.SearchUrl, "milk"));
        }
    }

    [Fact]
    public void The_search_words_are_put_into_the_address_safely()
    {
        var uri = StoreCatalog.BuildSearchUri("https://www.heb.com/search?q={item}", " mac & cheese ");

        Assert.Equal("https://www.heb.com/search?q=mac%20%26%20cheese", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://www.heb.com/search?q={item}")]
    [InlineData("https://www.heb.com/search?q=milk")]
    [InlineData("file:///etc/passwd?{item}")]
    [InlineData("javascript:alert('{item}')")]
    [InlineData("www.heb.com/search?q={item}")]
    [InlineData("")]
    [InlineData(null)]
    public void Only_https_addresses_with_a_place_for_the_search_words_are_accepted(string? searchUrl)
    {
        Assert.Null(StoreCatalog.BuildSearchUri(searchUrl, "milk"));
    }
}
