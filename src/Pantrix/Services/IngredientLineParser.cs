using System.Globalization;
using System.Text.RegularExpressions;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>What one line of a recipe's ingredient list says.</summary>
public record ParsedIngredient(decimal Quantity, Unit Unit, string Name, string? Note);

/// <summary>Reads lines such as "1 1/2 cups all-purpose flour, sifted" or "1 (15 ounce) can black beans".</summary>
public static partial class IngredientLineParser
{
    private static readonly Dictionary<string, string> Fractions = new()
    {
        ["½"] = "1/2", ["⅓"] = "1/3", ["⅔"] = "2/3", ["¼"] = "1/4", ["¾"] = "3/4",
        ["⅕"] = "1/5", ["⅙"] = "1/6", ["⅛"] = "1/8", ["⅜"] = "3/8", ["⅝"] = "5/8", ["⅞"] = "7/8"
    };

    // Single capital "T" and lower "t" (tablespoon, teaspoon) are left out: they differ only by case and are easy to get wrong.
    private static readonly Dictionary<string, Unit> UnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cup"] = Unit.Cup, ["cups"] = Unit.Cup, ["c"] = Unit.Cup,
        ["tablespoon"] = Unit.Tablespoon, ["tablespoons"] = Unit.Tablespoon, ["tbsp"] = Unit.Tablespoon, ["tbsps"] = Unit.Tablespoon, ["tbs"] = Unit.Tablespoon, ["tbl"] = Unit.Tablespoon,
        ["teaspoon"] = Unit.Teaspoon, ["teaspoons"] = Unit.Teaspoon, ["tsp"] = Unit.Teaspoon, ["tsps"] = Unit.Teaspoon,
        ["ounce"] = Unit.Ounce, ["ounces"] = Unit.Ounce, ["oz"] = Unit.Ounce,
        ["pound"] = Unit.Pound, ["pounds"] = Unit.Pound, ["lb"] = Unit.Pound, ["lbs"] = Unit.Pound,
        ["gram"] = Unit.Gram, ["grams"] = Unit.Gram, ["g"] = Unit.Gram,
        ["kilogram"] = Unit.Kilogram, ["kilograms"] = Unit.Kilogram, ["kg"] = Unit.Kilogram,
        ["milliliter"] = Unit.Milliliter, ["milliliters"] = Unit.Milliliter, ["millilitre"] = Unit.Milliliter, ["millilitres"] = Unit.Milliliter, ["ml"] = Unit.Milliliter,
        ["liter"] = Unit.Liter, ["liters"] = Unit.Liter, ["litre"] = Unit.Liter, ["litres"] = Unit.Liter, ["l"] = Unit.Liter,
        ["pint"] = Unit.Pint, ["pints"] = Unit.Pint, ["pt"] = Unit.Pint,
        ["quart"] = Unit.Quart, ["quarts"] = Unit.Quart, ["qt"] = Unit.Quart,
        ["gallon"] = Unit.Gallon, ["gallons"] = Unit.Gallon, ["gal"] = Unit.Gallon,
        ["can"] = Unit.Can, ["cans"] = Unit.Can,
        ["package"] = Unit.Package, ["packages"] = Unit.Package, ["pkg"] = Unit.Package, ["packet"] = Unit.Package, ["packets"] = Unit.Package,
        ["box"] = Unit.Box, ["boxes"] = Unit.Box,
        ["bottle"] = Unit.Bottle, ["bottles"] = Unit.Bottle,
        ["bunch"] = Unit.Bunch, ["bunches"] = Unit.Bunch,
        ["clove"] = Unit.Clove, ["cloves"] = Unit.Clove,
        ["pinch"] = Unit.Pinch, ["pinches"] = Unit.Pinch, ["dash"] = Unit.Pinch, ["dashes"] = Unit.Pinch
    };

    /// <summary>Reads one line. Returns null for a blank line or a heading such as "For the sauce:".</summary>
    public static ParsedIngredient? Parse(string? line)
    {
        var text = (line ?? "").Trim().TrimStart('-', '•', '*', '▢', '☐').Trim();
        foreach (var (symbol, written) in Fractions)
        {
            // "1½" means one and a half, so keep the two apart.
            text = text.Replace(symbol, " " + written + " ");
        }

        text = Spaces().Replace(text, " ").Trim();
        if (text.Length == 0 || text.EndsWith(':'))
        {
            return null;
        }

        // Asides in brackets ("(15 ounce)", "(optional)") are kept as a note, wherever they appear.
        var notes = new List<string>();
        text = Brackets().Replace(text, m =>
        {
            notes.Add(m.Groups[1].Value.Trim());
            return " ";
        });
        text = Spaces().Replace(text, " ").Trim();

        var quantity = 1m;
        var amount = Amount().Match(text);
        if (amount.Success)
        {
            quantity = ToNumber(amount.Groups["first"].Value);
            text = text[amount.Length..].Trim();
        }

        var unit = Unit.Each;
        var words = text.Split(' ', 3);
        if (words.Length > 1 && words[0].Equals("fl", StringComparison.OrdinalIgnoreCase) && UnitWords.GetValueOrDefault(words[1].TrimEnd('.')) == Unit.Ounce)
        {
            unit = Unit.FluidOunce;
            text = words.Length > 2 ? words[2] : "";
        }
        else if (words.Length > 1 && words[0].Equals("fluid", StringComparison.OrdinalIgnoreCase) && UnitWords.GetValueOrDefault(words[1]) == Unit.Ounce)
        {
            unit = Unit.FluidOunce;
            text = words.Length > 2 ? words[2] : "";
        }
        else if (words.Length > 1 && UnitWords.TryGetValue(words[0].TrimEnd('.'), out var found))
        {
            unit = found;
            text = string.Join(' ', words.Skip(1));
        }

        if (text.StartsWith("of ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..];
        }

        // What follows the first comma is how to prepare it, not what it is.
        var parts = text.Split(',', 2);
        var name = parts[0].Trim().Trim('.', ';').Trim();
        if (parts.Length > 1 && parts[1].Trim().Length > 0)
        {
            notes.Insert(0, parts[1].Trim());
        }

        return name.Length == 0
            ? null
            : new ParsedIngredient(quantity, unit, name, notes.Count == 0 ? null : string.Join("; ", notes));
    }

    // "1 1/2", "3/4", "2.5", "2", and for a range such as "2-3" or "2 to 3" the first of the two.
    private static decimal ToNumber(string text)
    {
        var total = 0m;
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var fraction = part.Split('/');
            total += fraction.Length == 2 && decimal.TryParse(fraction[0], CultureInfo.InvariantCulture, out var top)
                     && decimal.TryParse(fraction[1], CultureInfo.InvariantCulture, out var bottom) && bottom != 0
                ? top / bottom
                : decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var whole) ? whole : 0;
        }

        return total == 0 ? 1 : Math.Round(total, 3);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"^(?<first>\d+\s+\d+/\d+|\d+/\d+|\d+(\.\d+)?)(\s*(-|–|—|to)\s*(\d+\s+\d+/\d+|\d+/\d+|\d+(\.\d+)?))?\s*")]
    private static partial Regex Amount();
}
