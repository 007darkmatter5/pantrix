using System.Text.RegularExpressions;

namespace Pantrix.Data;

public static partial class EnumExtensions
{
    /// <summary>Turns "FluidOunce" into "Fluid ounce" for display.</summary>
    public static string Humanize<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var words = WordBoundary().Replace(value.ToString(), " ");
        return char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
