using Pantrix.Data;

namespace Pantrix.Services;

public record IngredientAmount(int IngredientId, decimal Quantity, Unit Unit);

/// <summary>An amount of an ingredient that any of <see cref="AlternativeIds"/> could satisfy instead.</summary>
public record IngredientNeed(int IngredientId, decimal Quantity, Unit Unit, IReadOnlyList<int>? AlternativeIds = null)
{
    public IReadOnlyList<int> Alternatives => AlternativeIds ?? [];
}

public static class ShoppingListBuilder
{
    /// <summary>
    /// Returns what still has to be bought: the needed amounts minus what is on hand, raised where
    /// necessary so that buying it also brings current stock up to each ingredient's minimum.
    /// The two aren't added together: needing 1 more for a recipe and being 1 below the minimum lists 1.
    /// Volume units are netted against each other, as are weight units. A need in one kind
    /// (e.g. cups) can't be offset by stock in another (e.g. grams), so it is listed in full.
    /// </summary>
    public static List<IngredientNeed> ComputeShortfall(
        IEnumerable<IngredientNeed> needed,
        IEnumerable<IngredientAmount> onHand,
        IEnumerable<IngredientAmount>? minimums = null)
    {
        var stock = onHand
            .GroupBy(a => Key(a.IngredientId, a.Unit))
            .ToDictionary(g => g.Key, g => g.Sum(a => Units.ToBase(a.Quantity, a.Unit)));

        var available = new Dictionary<(int, UnitKind, Unit?), decimal>(stock);
        var lines = new List<Line>();

        // Needs with the fewest acceptable ingredients claim stock first, so an either/or need
        // doesn't use up something that a stricter need has no substitute for.
        foreach (var need in needed.OrderBy(n => n.Alternatives.Count))
        {
            var missing = Units.ToBase(need.Quantity, need.Unit);
            foreach (var ingredientId in need.Alternatives.Prepend(need.IngredientId))
            {
                var key = Key(ingredientId, need.Unit);
                var used = Math.Min(missing, available.GetValueOrDefault(key));
                if (used > 0)
                {
                    available[key] -= used;
                    missing -= used;
                }
            }

            AddMissing(lines, need.IngredientId, need.Unit, need.Alternatives, missing);
        }

        foreach (var minimum in minimums ?? [])
        {
            var key = Key(minimum.IngredientId, minimum.Unit);
            var belowMinimum = Units.ToBase(minimum.Quantity, minimum.Unit) - stock.GetValueOrDefault(key);
            if (belowMinimum <= 0)
            {
                continue;
            }

            // Prefer the line for exactly this ingredient, then an either/or line it could be bought for.
            var line = lines
                .Where(l => l.Alternatives.Prepend(l.IngredientId).Any(id => Key(id, l.Unit) == key))
                .OrderBy(l => l.IngredientId == minimum.IngredientId ? 0 : 1).ThenBy(l => l.Alternatives.Count)
                .FirstOrDefault();
            if (line is null)
            {
                AddMissing(lines, minimum.IngredientId, minimum.Unit, [], belowMinimum);
            }
            else
            {
                line.Missing = Math.Max(line.Missing, belowMinimum);
            }
        }

        return lines
            .Select(l => new IngredientNeed(l.IngredientId, Math.Round(Units.FromBase(l.Missing, l.Unit), 2), l.Unit, l.Alternatives))
            .Where(n => n.Quantity > 0)
            .ToList();
    }

    private sealed class Line(int ingredientId, Unit unit, IReadOnlyList<int> alternatives)
    {
        public int IngredientId => ingredientId;
        public Unit Unit => unit;
        public IReadOnlyList<int> Alternatives => alternatives;
        public decimal Missing { get; set; }
    }

    // Amounts of the same ingredient are combined into one line unless they accept different substitutes.
    private static void AddMissing(List<Line> lines, int ingredientId, Unit unit, IReadOnlyList<int> alternatives, decimal missing)
    {
        if (missing <= 0)
        {
            return;
        }

        var line = lines.FirstOrDefault(l =>
            Key(l.IngredientId, l.Unit) == Key(ingredientId, unit)
            && l.Alternatives.Order().SequenceEqual(alternatives.Order()));
        if (line is null)
        {
            line = new Line(ingredientId, unit, alternatives);
            lines.Add(line);
        }

        line.Missing += missing;
    }

    // Count-style units (can, bunch, ...) only match themselves.
    private static (int IngredientId, UnitKind Kind, Unit? Unit) Key(int ingredientId, Unit unit)
    {
        var kind = Units.KindOf(unit);
        return (ingredientId, kind, kind == UnitKind.Count ? unit : null);
    }
}
