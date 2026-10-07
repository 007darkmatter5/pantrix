using System.Globalization;
using System.Text.RegularExpressions;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>How a name from a recipe relates to the ingredients a kitchen already has.</summary>
/// <param name="Exact">The ingredient that is the same thing, when there is exactly one; safe to pre-select.</param>
/// <param name="Similar">Ingredients that might be the same thing, most alike first; for a person to judge.</param>
public record IngredientMatch(Ingredient? Exact, IReadOnlyList<Ingredient> Similar);

/// <summary>
/// Compares ingredient names so that importing a recipe doesn't quietly add "tomatoes" next to "Tomato".
/// It only ever suggests: deciding that two names are the same ingredient is left to the person importing.
/// </summary>
public static partial class IngredientMatcher
{
    // Words that describe how an ingredient comes or is prepared, not which ingredient it is.
    private static readonly HashSet<string> Descriptors =
    [
        "fresh", "freshly", "large", "medium", "small", "chopped", "diced", "minced", "sliced", "shredded", "grated",
        "crushed", "peeled", "cooked", "uncooked", "raw", "dried", "frozen", "canned", "finely", "roughly", "thinly",
        "whole", "ripe", "optional", "divided", "softened", "melted", "packed", "boneless", "skinless", "extra", "of", "the", "a", "an", "and", "or"
    ];

    public static IngredientMatch Match(string name, IReadOnlyCollection<Ingredient> existing)
    {
        var wanted = Words(name);
        if (wanted.Count == 0)
        {
            return new IngredientMatch(null, []);
        }

        var scored = existing
            .Select(ingredient => (Ingredient: ingredient, Words: Words(ingredient.Name)))
            .Select(x => (x.Ingredient, Shared: x.Words.Intersect(wanted).Count(), Total: x.Words.Union(wanted).Count()))
            .Where(x => x.Shared > 0)
            .OrderByDescending(x => (double)x.Shared / x.Total)
            .ThenBy(x => x.Ingredient.Name)
            .ToList();

        var same = scored.Where(x => x.Shared == x.Total).Select(x => x.Ingredient).ToList();
        return new IngredientMatch(same.Count == 1 ? same[0] : null, scored.Select(x => x.Ingredient).Take(4).ToList());
    }

    /// <summary>A name as it would be written for a new ingredient: "all-purpose flour" becomes "All-Purpose Flour".</summary>
    public static string Tidy(string name) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Spaces().Replace(name.Trim(), " ").ToLowerInvariant());

    // The distinctive words of a name, in a comparable form: lower case, singular, without descriptors.
    private static HashSet<string> Words(string name) =>
        NotLetter().Split(name.ToLowerInvariant())
            .Where(word => word.Length > 1 && !Descriptors.Contains(word))
            .Select(Singular)
            .ToHashSet();

    private static string Singular(string word) => word switch
    {
        _ when word.EndsWith("ies") && word.Length > 4 => word[..^3] + "y",
        _ when word.EndsWith("oes") && word.Length > 4 => word[..^2],
        _ when word.EndsWith("ches") || word.EndsWith("shes") || word.EndsWith("sses") || word.EndsWith("xes") => word[..^2],
        _ when word.EndsWith('s') && !word.EndsWith("ss") && word.Length > 3 => word[..^1],
        _ => word
    };

    [GeneratedRegex(@"[^\p{L}]+")]
    private static partial Regex NotLetter();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
