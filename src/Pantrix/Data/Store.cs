namespace Pantrix.Data;

/// <summary>How a store's aisles get into Pantrix.</summary>
public enum AisleSource
{
    /// <summary>Typed in by hand; the only choice when the store publishes no aisles online.</summary>
    Manual,

    /// <summary>Read from the store's website through the browser the admin has connected.</summary>
    Browser
}

public class Store : IKitchenOwned
{
    public int Id { get; set; }
    public int KitchenId { get; set; }

    /// <summary>The chain or shop name, e.g. "Sam's Club".</summary>
    public string Name { get; set; } = "";

    /// <summary>Which branch, e.g. a city or club number. Aisles differ between branches of the same chain.</summary>
    public string? Location { get; set; }

    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>
    /// OpenStreetMap's permanent id for the place (e.g. "W402307311"), set when the address came from a lookup.
    /// Two stores with the same id are the same building however their names and addresses were typed.
    /// </summary>
    public string? OsmPlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public AisleSource AisleSource { get; set; }

    /// <summary>
    /// For <see cref="AisleSource.Browser"/>: the address of the store website's search page, with {item} where
    /// the search words go, e.g. "https://www.heb.com/search?q={item}".
    /// </summary>
    public string? SearchUrl { get; set; }

    /// <summary>
    /// The number the store's website knows this location by (H-E-B's West Hopkins store is 455). With it, a
    /// lookup selects this location first; without it, the site shows whichever location the browser last used.
    /// </summary>
    public string? WebsiteStoreId { get; set; }

    public List<StoreAisle> Aisles { get; set; } = [];

    /// <summary>"Store - City State (Location)", leaving out whichever parts aren't filled in.</summary>
    public string DisplayName
    {
        get
        {
            var location = Location?.Trim();
            if (!string.IsNullOrEmpty(location) && !(location.StartsWith('(') && location.EndsWith(')')))
            {
                location = $"({location})";
            }

            var where = string.Join(" ", new[] { City, State, location }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return where.Length == 0 ? Name : $"{Name} - {where}";
        }
    }

    /// <summary>The address on one line, e.g. "123 Main St, Springfield, IL 62701". Empty when none is recorded.</summary>
    public string FormattedAddress
    {
        get
        {
            var stateAndZip = string.Join(" ", new[] { State, PostalCode }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.Join(", ", new[] { StreetAddress, City, stateAndZip }.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}

/// <summary>
/// What is known about an ingredient at one store: its aisle, its page on the store's website, or both. Kept apart from the ingredient itself so that these
/// records can later be shared between users without sharing anyone's ingredient list.
/// </summary>
public class StoreAisle
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    /// <summary>Empty when only the product page is known.</summary>
    public string Aisle { get; set; } = "";

    /// <summary>The last day the item was entered, corrected, or found at this aisle.</summary>
    public DateOnly LastConfirmed { get; set; }

    /// <summary>The product's page on this store's website.</summary>
    public string? ProductUrl { get; set; }
}
