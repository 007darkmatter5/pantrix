using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Pantrix.Services;

/// <summary>A recipe as read from somewhere else, before anyone has decided what to do with its ingredients.</summary>
public record ImportedRecipe(
    string Name,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    string? Instructions,
    IReadOnlyList<string> IngredientLines,
    string? SourceUrl);

/// <summary>
/// Reads a recipe from a web page. Most recipe sites describe their recipe in a standard block of data
/// (schema.org "Recipe") for search engines; this reads that block and nothing else on the page.
/// </summary>
public partial class RecipeImportService(HttpClient http, ILogger<RecipeImportService> logger)
{
    private const int MaxPageBytes = 5 * 1024 * 1024;

    /// <summary>Fetches the page and reads its recipe. Returns the recipe, or a message saying why there isn't one.</summary>
    public async Task<(ImportedRecipe? Recipe, string? Error)> FetchAsync(string? address, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return (null, "Enter the recipe's full web address, starting with https://.");
        }

        string html;
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"That site answered \"{(int)response.StatusCode} {response.ReasonPhrase}\". Some sites turn away anything but a person's browser; paste the recipe instead.");
            }

            if (response.Content.Headers.ContentLength > MaxPageBytes)
            {
                return (null, "That page is too large to read.");
            }

            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogInformation(e, "Recipe page could not be fetched");
            return (null, "Couldn't reach that page. Check the address, or paste the recipe instead.");
        }

        return ParseHtml(html, uri.AbsoluteUri) is { } recipe
            ? (recipe, null)
            : (null, "That page doesn't describe a recipe in the standard way. Paste the recipe instead.");
    }

    /// <summary>Finds the schema.org Recipe in a page's data blocks. Null when there isn't one with ingredients.</summary>
    public static ImportedRecipe? ParseHtml(string html, string? sourceUrl)
    {
        foreach (Match block in DataBlock().Matches(html))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(block.Groups[1].Value, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                if (FindRecipe(document.RootElement) is { } recipe && ToRecipe(recipe, sourceUrl) is { } imported)
                {
                    return imported;
                }
            }
        }

        return null;
    }

    // The recipe may be the block itself, one of a list, or inside a "@graph" alongside the page's other data.
    private static JsonElement? FindRecipe(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindRecipe(item) is { } found)
                    {
                        return found;
                    }
                }

                return null;

            case JsonValueKind.Object:
                if (element.TryGetProperty("@type", out var type) && Strings(type).Any(t => t.Equals("Recipe", StringComparison.OrdinalIgnoreCase)))
                {
                    return element;
                }

                return element.TryGetProperty("@graph", out var graph) ? FindRecipe(graph) : null;

            default:
                return null;
        }
    }

    private static ImportedRecipe? ToRecipe(JsonElement recipe, string? sourceUrl)
    {
        var ingredients = recipe.TryGetProperty("recipeIngredient", out var list) ? Strings(list).Select(Clean).Where(l => l.Length > 0).ToList() : [];
        var name = recipe.TryGetProperty("name", out var n) ? Clean(Strings(n).FirstOrDefault() ?? "") : "";
        if (ingredients.Count == 0 || name.Length == 0)
        {
            return null;
        }

        var steps = recipe.TryGetProperty("recipeInstructions", out var instructions) ? Steps(instructions).Select(Clean).Where(s => s.Length > 0).ToList() : [];
        var description = recipe.TryGetProperty("description", out var d) ? Clean(Strings(d).FirstOrDefault() ?? "") : "";
        return new ImportedRecipe(
            name,
            description.Length == 0 ? null : description,
            recipe.TryGetProperty("recipeYield", out var yield) ? FirstNumber(yield) : null,
            recipe.TryGetProperty("prepTime", out var prep) ? Minutes(prep) : null,
            recipe.TryGetProperty("cookTime", out var cook) ? Minutes(cook) : null,
            steps.Count == 0 ? null : string.Join("\n", steps),
            ingredients,
            sourceUrl);
    }

    // Instructions come as one text, a list of texts, a list of steps, or sections each holding steps.
    private static IEnumerable<string> Steps(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            case JsonValueKind.Array:
                return element.EnumerateArray().SelectMany(Steps);

            case JsonValueKind.Object when element.TryGetProperty("itemListElement", out var items):
                return Steps(items);

            case JsonValueKind.Object when element.TryGetProperty("text", out var text):
                return Steps(text);

            default:
                return [];
        }
    }

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Number => [element.GetRawText()],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        _ => []
    };

    // "4", "4 servings", "Makes 12 muffins", or a list of such: the first number is the count.
    private static int? FirstNumber(JsonElement element) =>
        Strings(element).Select(s => Number().Match(s)).Where(m => m.Success)
            .Select(m => int.TryParse(m.Value, out var number) && number is > 0 and < 1000 ? number : (int?)null)
            .FirstOrDefault(number => number is not null);

    // Times are written as ISO 8601 durations: "PT15M", "PT1H30M".
    private static int? Minutes(JsonElement element)
    {
        try
        {
            var minutes = Strings(element).FirstOrDefault() is { Length: > 0 } text ? (int)XmlConvert.ToTimeSpan(text).TotalMinutes : 0;
            return minutes > 0 ? minutes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Clean(string text) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(text, " ")), " ").Trim();

    /// <summary>
    /// Connects only to addresses on the public internet. The address is one a signed-in person typed, and without
    /// this check it could be used to make the server fetch pages from the home network it sits on.
    /// </summary>
    public static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPublic)
            ?? throw new HttpRequestException($"{context.DnsEndPoint.Host} is not an address on the public internet.");

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast)
        {
            return false;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return !address.Equals(IPAddress.IPv6Any);
        }

        var b = address.GetAddressBytes();
        return !(b[0] is 0 or 10 or 127 || b[0] >= 224
                 || (b[0] == 100 && b[1] is >= 64 and <= 127)
                 || (b[0] == 169 && b[1] == 254)
                 || (b[0] == 172 && b[1] is >= 16 and <= 31)
                 || (b[0] == 192 && b[1] == 168));
    }

    [GeneratedRegex("""<script[^>]*type\s*=\s*["']application/ld\+json["'][^>]*>(.*?)</script>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DataBlock();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();
}
