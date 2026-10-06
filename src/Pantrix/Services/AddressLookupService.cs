using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantrix.Services;

public record AddressSuggestion(
    string? Name, string Street, string? City, string? State, string? PostalCode,
    string? PlaceId = null, double? Latitude = null, double? Longitude = null)
{
    public string Label
    {
        get
        {
            var stateAndZip = string.Join(" ", new[] { State, PostalCode }.Where(p => !string.IsNullOrWhiteSpace(p)));
            var address = string.Join(", ", new[] { Street, City, stateAndZip }.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(Name) ? address : $"{Name} — {address}";
        }
    }
}

/// <summary>
/// Looks up street addresses and named places in OpenStreetMap data. Meant to be called when the user asks
/// for a lookup, not on every keystroke: Nominatim's usage policy doesn't allow search-as-you-type.
/// </summary>
public class AddressLookupService(HttpClient http, ILogger<AddressLookupService> logger)
{
    private const int MaxSuggestions = 6;

    /// <summary>Returns matching addresses, or nothing if the services can't be reached; lookup is a convenience, never required.</summary>
    public async Task<IReadOnlyList<AddressSuggestion>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        // Nominatim is quick but literal; Photon is slow but tolerates misspellings and loose store names.
        var found = await SearchNominatimAsync(text, cancellationToken);
        if (found.Count == 0)
        {
            found = await SearchPhotonAsync(text, cancellationToken);
        }

        // The same building often appears several times (the shop, its fuel station, a bus stop outside),
        // so keep one result per address and put the ones with a street number first.
        return found
            .OrderBy(a => char.IsDigit(a.Street[0]) ? 0 : 1)
            .DistinctBy(a => (a.Street, a.City, a.PostalCode))
            .Take(MaxSuggestions)
            .ToList();
    }

    private async Task<List<AddressSuggestion>> SearchNominatimAsync(string text, CancellationToken cancellationToken)
    {
        var results = await GetAsync<List<NominatimResult>>(
            $"https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=10&q={Uri.EscapeDataString(text)}",
            cancellationToken);

        return (results ?? [])
            .Where(r => r.Address is not null && r.Address.ContainsKey("road"))
            .Select(r =>
            {
                var address = r.Address!;
                string? Part(params string[] keys) => keys.Select(address.GetValueOrDefault).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                // "US-IL" gives the familiar two-letter state; elsewhere fall back to the full name.
                var region = Part("ISO3166-2-lvl4");
                var state = region is not null && region.StartsWith("US-") ? region[3..] : Part("state");
                return new AddressSuggestion(
                    string.IsNullOrWhiteSpace(r.Name) ? null : r.Name,
                    $"{Part("house_number")} {Part("road")}".Trim(),
                    Part("city", "town", "village", "hamlet"),
                    state,
                    Part("postcode"),
                    PlaceId(r.OsmType, r.OsmId),
                    Coordinate(r.Lat),
                    Coordinate(r.Lon));
            })
            .ToList();
    }

    private async Task<List<AddressSuggestion>> SearchPhotonAsync(string text, CancellationToken cancellationToken)
    {
        var response = await GetAsync<PhotonResponse>(
            $"https://photon.komoot.io/api/?limit=10&lang=en&q={Uri.EscapeDataString(text)}", cancellationToken);

        return (response?.Features ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Properties.Street))
            .Select(f =>
            {
                // GeoJSON puts longitude first.
                var coordinates = f.Geometry?.Coordinates is { Length: 2 } c ? c : null;
                var p = f.Properties;
                return new AddressSuggestion(
                    p.Name, $"{p.Housenumber} {p.Street}".Trim(), p.City, p.State, p.Postcode,
                    PlaceId(p.OsmType, p.OsmId), coordinates?[1], coordinates?[0]);
            })
            .ToList();
    }

    // Both services identify the same OpenStreetMap object, but one spells the type out ("way") and the other
    // abbreviates it ("W"); "W402307311" is the common form.
    private static string? PlaceId(string? osmType, long? osmId) =>
        string.IsNullOrEmpty(osmType) || osmId is null ? null : $"{char.ToUpperInvariant(osmType[0])}{osmId}";

    private static double? Coordinate(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        try
        {
            return await http.GetFromJsonAsync<T>(url, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Address lookup failed for {Host}", new Uri(url).Host);
            }

            return null;
        }
    }

    private sealed record NominatimResult(
        string? Name,
        Dictionary<string, string>? Address,
        [property: JsonPropertyName("osm_type")] string? OsmType,
        [property: JsonPropertyName("osm_id")] long? OsmId,
        string? Lat,
        string? Lon);

    private sealed record PhotonResponse(List<PhotonFeature> Features);

    private sealed record PhotonFeature(PhotonProperties Properties, PhotonGeometry? Geometry);

    private sealed record PhotonGeometry(double[]? Coordinates);

    private sealed record PhotonProperties(
        string? Name, string? Housenumber, string? Street, string? City, string? State, string? Postcode,
        [property: JsonPropertyName("osm_type")] string? OsmType,
        [property: JsonPropertyName("osm_id")] long? OsmId);
}
