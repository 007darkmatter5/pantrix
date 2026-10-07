using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>What Pantrix knows about getting aisles for one chain of stores.</summary>
/// <param name="Name">The chain as people know it.</param>
/// <param name="Aliases">Lower-case letters-and-digits spellings that identify the chain in a store's name.</param>
/// <param name="Source">The best method that works for it.</param>
/// <param name="SearchUrl">For browser lookup, the chain's search page with {item} where the search words go.</param>
/// <param name="Tested">True when the method has been tried against the real site, not just reasoned about.</param>
/// <param name="Notes">What someone setting up the store needs to know.</param>
/// <param name="Locations">How the chain's website is told which location to show; null when Pantrix doesn't know.</param>
public record KnownChain(
    string Name, string[] Aliases, AisleSource Source, string? SearchUrl, bool Tested, string Notes,
    LocationPicker? Locations = null);

/// <summary>How a chain's website selects one of its locations, and where its location numbers can be found.</summary>
/// <param name="CookieName">The cookie in which the site keeps the selected location's number.</param>
/// <param name="CookieDomain">The site the cookie belongs to.</param>
/// <param name="LocatorUrl">The site's store finder, with {place} where a city or ZIP goes.</param>
/// <param name="LinkMarker">Text that appears in the address of each location's own page on the finder.</param>
/// <param name="Help">How a person can find the number themselves.</param>
public record LocationPicker(string CookieName, string CookieDomain, string LocatorUrl, string LinkMarker, string Help);

/// <summary>
/// The chains Pantrix recognises and how aisles are obtained for each. A store that isn't listed can still be
/// tried with browser lookup by giving it its website's search address.
/// </summary>
public static partial class StoreCatalog
{
    public static readonly IReadOnlyList<KnownChain> Chains =
    [
        new("H-E-B", ["heb"], AisleSource.Browser, "https://www.heb.com/search?q={item}", Tested: true,
            "Search results on heb.com show each product's aisle. Aisles differ between locations, so give each " +
            "store its H-E-B store number and lookups will use that location.",
            new LocationPicker("SHOPPING_STORE_ID", "www.heb.com", "https://www.heb.com/store-locations?address={place}", "/heb-store/",
                "On heb.com's store locator, open your store's page: the number at the end of its address " +
                "(…/west-hopkins-h-e-b-455) is the store number.")),

        new("Sam's Club", ["samsclub", "sams"], AisleSource.Manual, null, Tested: true,
            "The Sam's Club website doesn't show where items are in the club; only their phone app does. " +
            "Enter aisles yourself as you shop."),

        new("Target", ["target"], AisleSource.Manual, null, Tested: true,
            "Target's search results don't list aisles, and its site asks for a press-and-hold human check after a " +
            "couple of automated page views. Enter aisles yourself as you shop.")
    ];

    /// <summary>The word people are asked to search a store's website for when checking whether it shows aisles.</summary>
    public const string SampleSearch = "milk";

    /// <summary>
    /// Turns the address of a results page for <see cref="SampleSearch"/> into a reusable search address, by
    /// putting {item} where the search word was. Null when the address isn't https or doesn't contain the word.
    /// </summary>
    public static string? ToSearchAddress(string? resultsPageAddress)
    {
        var address = resultsPageAddress?.Trim() ?? "";
        var at = address.LastIndexOf(SampleSearch, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }

        var template = address[..at] + "{item}" + address[(at + SampleSearch.Length)..];
        return BuildSearchUri(template, SampleSearch) is null ? null : template;
    }

    /// <summary>The chain a store belongs to, judged by its name, or null when it isn't one Pantrix knows.</summary>
    public static KnownChain? Find(string? storeName)
    {
        var normalized = new string((storeName ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        return normalized.Length == 0
            ? null
            : Chains.FirstOrDefault(chain => chain.Aliases.Any(alias => normalized.StartsWith(alias) || normalized.EndsWith(alias)));
    }

    /// <summary>
    /// The page to open for a search, or null when the address can't be used. Only https addresses with an
    /// {item} placeholder are accepted, so the connected browser can't be pointed at something on the local network.
    /// </summary>
    public static Uri? BuildSearchUri(string? searchUrl, string query)
    {
        if (string.IsNullOrWhiteSpace(searchUrl) || !searchUrl.Contains("{item}", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var address = searchUrl.Trim().Replace("{item}", Uri.EscapeDataString(query.Trim()), StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    /// <summary>A location number safe to hand to a website: letters, digits, dashes and underscores only. Null when blank or anything else.</summary>
    public static string? CleanLocationId(string? locationId)
    {
        var id = locationId?.Trim() ?? "";
        return id.Length is > 0 and <= 40 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? id : null;
    }

    /// <summary>
    /// Picks, from a store finder's links, the location at <paramref name="streetAddress"/>. A link matches when
    /// the text beside it has the same street number and every distinctive word of the street name; words like
    /// "West" or "Street" are ignored because sites abbreviate them. The location's number is the digits that
    /// end its link. Null when nothing matches, or more than one location does.
    /// </summary>
    public static (string Id, string Label)? MatchLocation(string? streetAddress, IEnumerable<(string Href, string Text)> links)
    {
        // In order, so the street number is the one that leads the address and not a route number later in it.
        var words = NotLetterOrDigit().Split((streetAddress ?? "").ToUpperInvariant()).Where(w => w.Length > 0).ToList();
        var number = words.FirstOrDefault();
        var names = words.Skip(1).Where(w => w.Length > 1 && !GenericStreetWords.Contains(w)).ToList();
        if (number is null || !char.IsDigit(number[0]) || names.Count == 0)
        {
            return null;
        }

        var matches = links
            .Where(link => Words(link.Text) is var text && text.Contains(number) && names.All(text.Contains))
            .Select(link => (Id: TrailingDigits().Match(link.Href.TrimEnd('/')).Value, Label: link.Text.Split('\n')[0].Trim()))
            .Where(m => m.Id.Length > 0)
            .DistinctBy(m => m.Id)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static readonly HashSet<string> GenericStreetWords =
    [
        "NORTH", "SOUTH", "EAST", "WEST", "STREET", "ROAD", "DRIVE", "AVENUE", "BOULEVARD", "LANE", "HIGHWAY", "PARKWAY",
        "COURT", "PLACE", "SUITE", "STE", "AVE", "BLVD", "HWY", "PKWY", "ST", "RD", "DR", "LN", "CT", "PL"
    ];

    private static HashSet<string> Words(string? text) =>
        NotLetterOrDigit().Split((text ?? "").ToUpperInvariant()).Where(w => w.Length > 0).ToHashSet();

    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial System.Text.RegularExpressions.Regex NotLetterOrDigit();

    [System.Text.RegularExpressions.GeneratedRegex(@"\d+$")]
    private static partial System.Text.RegularExpressions.Regex TrailingDigits();

    public static string Describe(AisleSource source) => source switch
    {
        AisleSource.Browser => "Browser lookup",
        _ => "Manual"
    };
}
