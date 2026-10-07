using System.Text.RegularExpressions;

namespace Pantrix.Services;

/// <summary>
/// Makes sense of what's typed or looked up as an aisle. It has an area, the part of the store to walk to
/// ("8", "Bakery", "Meat Market on the Back Wall"), and optionally, after a comma, a spot within it ("A29").
/// </summary>
public static partial class AisleOrder
{
    /// <summary>The part of the store, without the spot: "8, A10" and "8, A8" are both area "8".</summary>
    public static string Area(string aisle) => aisle.Split(',', 2)[0].Trim();

    /// <summary>
    /// Sort key that walks a store in order: numbered aisles by number ("2" before "12", "12" before "12B"),
    /// then named areas ("Bakery", "Back wall") alphabetically. Everything in one area shares a key.
    /// </summary>
    public static (int Group, int Number, string Text) Key(string aisle)
    {
        var text = Area(aisle).ToUpperInvariant();
        var match = LeadingNumber().Match(text);
        return match.Success && int.TryParse(match.Value, out var number)
            ? (0, number, text)
            : (1, 0, text);
    }

    /// <summary>Sort key for the spot within an area, so "A8" comes before "A10". Aisles with no spot come first.</summary>
    public static (string Letters, int Number) SpotKey(string aisle)
    {
        var parts = aisle.Split(',', 2);
        var match = parts.Length < 2 ? Match.Empty : Spot().Match(parts[1].Trim().ToUpperInvariant());
        return match.Success ? (match.Groups[1].Value, int.Parse(match.Groups[2].Value)) : ("", 0);
    }

    /// <summary>The area as a heading: "12" reads as "Aisle 12"; anything already descriptive, like "Bakery", is left alone.</summary>
    public static string Title(string aisle) => Describe(Area(aisle));

    /// <summary>The whole aisle as people say it: "8, A8" reads as "Aisle 8, A8".</summary>
    public static string Describe(string aisle) =>
        LeadingNumber().IsMatch(aisle.Trim()) ? $"Aisle {aisle.Trim()}" : aisle.Trim();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^([A-Z]*)(\d{1,6})")]
    private static partial Regex Spot();
}
