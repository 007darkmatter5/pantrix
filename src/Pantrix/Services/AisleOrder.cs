using System.Text.RegularExpressions;

namespace Pantrix.Services;

public static partial class AisleOrder
{
    /// <summary>
    /// Sort key that walks a store in order: numbered aisles by number ("2" before "12", "12" before "12B"),
    /// then named areas ("Bakery", "Back wall") alphabetically.
    /// </summary>
    public static (int Group, int Number, string Text) Key(string aisle)
    {
        var text = aisle.Trim().ToUpperInvariant();
        var match = LeadingNumber().Match(text);
        return match.Success && int.TryParse(match.Value, out var number)
            ? (0, number, text)
            : (1, 0, text);
    }

    /// <summary>"12" reads as "Aisle 12"; anything already descriptive, like "Bakery", is left alone.</summary>
    public static string Title(string aisle) =>
        LeadingNumber().IsMatch(aisle.Trim()) ? $"Aisle {aisle.Trim()}" : aisle.Trim();

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumber();
}
