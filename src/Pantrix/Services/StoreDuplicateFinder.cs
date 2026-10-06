using System.Text.RegularExpressions;
using Pantrix.Data;

namespace Pantrix.Services;

public static partial class StoreDuplicateFinder
{
    private static readonly Dictionary<string, string> Abbreviations = new()
    {
        ["ST"] = "STREET", ["DR"] = "DRIVE", ["AVE"] = "AVENUE", ["AV"] = "AVENUE", ["RD"] = "ROAD",
        ["BLVD"] = "BOULEVARD", ["LN"] = "LANE", ["CT"] = "COURT", ["PL"] = "PLACE", ["HWY"] = "HIGHWAY",
        ["PKWY"] = "PARKWAY", ["CIR"] = "CIRCLE", ["TER"] = "TERRACE", ["TRL"] = "TRAIL", ["SQ"] = "SQUARE",
        ["N"] = "NORTH", ["S"] = "SOUTH", ["E"] = "EAST", ["W"] = "WEST",
        ["NE"] = "NORTHEAST", ["NW"] = "NORTHWEST", ["SE"] = "SOUTHEAST", ["SW"] = "SOUTHWEST"
    };

    /// <summary>
    /// Returns an existing store that is probably the same place as <paramref name="candidate"/>: one with the
    /// same OpenStreetMap place id, or failing that the same street address once spelling differences are evened out.
    /// </summary>
    public static Store? Find(Store candidate, IEnumerable<Store> existing)
    {
        var others = existing.Where(s => s.Id != candidate.Id).ToList();
        var addressKey = AddressKey(candidate);

        return others.FirstOrDefault(s => candidate.OsmPlaceId is not null && s.OsmPlaceId == candidate.OsmPlaceId)
            ?? others.FirstOrDefault(s => addressKey is not null && AddressKey(s) == addressKey);
    }

    /// <summary>
    /// A comparable form of the address, so that "2300 W. White Oaks Dr" and "2300 West White Oaks Drive" match.
    /// Null when there isn't enough of an address to compare safely.
    /// </summary>
    public static string? AddressKey(Store store)
    {
        var street = Normalize(store.StreetAddress);
        if (street.Length == 0 || !char.IsDigit(street[0]))
        {
            return null;
        }

        // The ZIP pins the street to one town; fall back to the city name when there isn't one.
        var zip = Digits().Match(store.PostalCode ?? "").Value;
        var area = zip.Length >= 5 ? zip[..5] : Normalize(store.City);
        return area.Length == 0 ? null : $"{street}|{area}";
    }

    private static string Normalize(string? text)
    {
        var words = NotLetterOrDigit().Replace((text ?? "").ToUpperInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Abbreviations.GetValueOrDefault(word, word));
        return string.Join(" ", words);
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NotLetterOrDigit();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
