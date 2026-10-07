using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>What Pantrix knows about getting aisles for one chain of stores.</summary>
/// <param name="Name">The chain as people know it.</param>
/// <param name="Aliases">Lower-case letters-and-digits spellings that identify the chain in a store's name.</param>
/// <param name="Source">The best method that works for it.</param>
/// <param name="SearchUrl">For browser lookup, the chain's search page with {item} where the search words go.</param>
/// <param name="Tested">True when the method has been tried against the real site, not just reasoned about.</param>
/// <param name="Notes">What someone setting up the store needs to know.</param>
public record KnownChain(string Name, string[] Aliases, AisleSource Source, string? SearchUrl, bool Tested, string Notes);

/// <summary>
/// The chains Pantrix recognises and how aisles are obtained for each. A store that isn't listed can still be
/// tried with browser lookup by giving it its website's search address.
/// </summary>
public static class StoreCatalog
{
    public static readonly IReadOnlyList<KnownChain> Chains =
    [
        new("H-E-B", ["heb"], AisleSource.Browser, "https://www.heb.com/search?q={item}", Tested: true,
            "Search results on heb.com show each product's aisle. In the connected browser, open heb.com once and " +
            "choose the same store location, because aisles differ between locations."),

        new("Sam's Club", ["samsclub", "sams"], AisleSource.Manual, null, Tested: true,
            "The Sam's Club website doesn't show where items are in the club; only their phone app does. " +
            "Enter aisles yourself as you shop.")
    ];

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

    public static string Describe(AisleSource source) => source switch
    {
        AisleSource.Browser => "Browser lookup",
        _ => "Manual"
    };
}
