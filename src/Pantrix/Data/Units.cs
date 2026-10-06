namespace Pantrix.Data;

public enum UnitKind
{
    Count,
    Volume,
    Weight
}

public enum Unit
{
    Each,
    Can,
    Package,
    Box,
    Bottle,
    Bunch,
    Clove,
    Pinch,
    Teaspoon,
    Tablespoon,
    FluidOunce,
    Cup,
    Pint,
    Quart,
    Gallon,
    Milliliter,
    Liter,
    Ounce,
    Pound,
    Gram,
    Kilogram
}

public static class Units
{
    // Volume factors are in milliliters, weight factors in grams.
    private static readonly Dictionary<Unit, (UnitKind Kind, decimal Factor, string Abbreviation)> Info = new()
    {
        [Unit.Each] = (UnitKind.Count, 1m, ""),
        [Unit.Can] = (UnitKind.Count, 1m, "can"),
        [Unit.Package] = (UnitKind.Count, 1m, "pkg"),
        [Unit.Box] = (UnitKind.Count, 1m, "box"),
        [Unit.Bottle] = (UnitKind.Count, 1m, "bottle"),
        [Unit.Bunch] = (UnitKind.Count, 1m, "bunch"),
        [Unit.Clove] = (UnitKind.Count, 1m, "clove"),
        [Unit.Pinch] = (UnitKind.Count, 1m, "pinch"),
        [Unit.Teaspoon] = (UnitKind.Volume, 4.92892m, "tsp"),
        [Unit.Tablespoon] = (UnitKind.Volume, 14.7868m, "tbsp"),
        [Unit.FluidOunce] = (UnitKind.Volume, 29.5735m, "fl oz"),
        [Unit.Cup] = (UnitKind.Volume, 236.588m, "cup"),
        [Unit.Pint] = (UnitKind.Volume, 473.176m, "pt"),
        [Unit.Quart] = (UnitKind.Volume, 946.353m, "qt"),
        [Unit.Gallon] = (UnitKind.Volume, 3785.41m, "gal"),
        [Unit.Milliliter] = (UnitKind.Volume, 1m, "ml"),
        [Unit.Liter] = (UnitKind.Volume, 1000m, "l"),
        [Unit.Ounce] = (UnitKind.Weight, 28.3495m, "oz"),
        [Unit.Pound] = (UnitKind.Weight, 453.592m, "lb"),
        [Unit.Gram] = (UnitKind.Weight, 1m, "g"),
        [Unit.Kilogram] = (UnitKind.Weight, 1000m, "kg")
    };

    public static UnitKind KindOf(Unit unit) => Info[unit].Kind;

    public static string Abbreviation(Unit unit) => Info[unit].Abbreviation;

    public static decimal ToBase(decimal quantity, Unit unit) => quantity * Info[unit].Factor;

    public static decimal FromBase(decimal baseQuantity, Unit unit) => baseQuantity / Info[unit].Factor;

    public static string Format(decimal quantity, Unit unit) => $"{quantity:0.##} {Abbreviation(unit)}".Trim();
}
