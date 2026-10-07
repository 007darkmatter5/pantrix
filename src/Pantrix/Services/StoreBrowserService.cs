using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>A product found on a store's website, with where the site says it is.</summary>
public record AisleMatch(string Name, string Aisle, string? Url);

/// <param name="Error">Why there are no matches, when there are none.</param>
/// <param name="Notice">Something to know about matches that were found, e.g. that they may be for another location.</param>
public record AisleSearchResult(IReadOnlyList<AisleMatch> Matches, string? Error = null, string? Notice = null);

/// <summary>
/// Looks up aisles on a store's website by driving a real Chrome that the admin runs alongside Pantrix (usually
/// a second container) and has connected by address. Store sites turn away plain programs, but a real browser on
/// the home network, in which someone has already opened the site and chosen their store, is served normally.
///
/// It is used sparingly on purpose: one page per lookup, only when someone asks, and no attempt to get past a
/// site that asks for a human check. That is reported so a person can deal with it in the browser.
/// </summary>
public class StoreBrowserService(HttpClient http, IDbContextFactory<PantrixDbContext> dbFactory, ILogger<StoreBrowserService> logger)
{
    private const string AddressKey = "BrowserAddress";
    private static readonly TimeSpan ReadFor = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    // Runs in the store's page. Finds each piece of text like "Aisle 8, A8", takes the product card around it
    // (the largest box that doesn't also include a neighbour's aisle), and reads the product's name and link
    // from the card. Nothing here is specific to one store.
    private const string ReadAislesScript = """
        (() => {
          const body = document.body ? document.body.innerText : '';
          const emptyChallenge = body.length < 2000 && /robot|captcha|access denied|pardon our interruption/i.test(document.title + ' ' + body.slice(0, 400));
          // Some sites lay the check over the real page instead, so its wording has to be looked for everywhere.
          const overlayChallenge = /press\s*(&|and)\s*hold|confirm you('|’)?re (a )?human|verify you are (a )?human|not a (ro)?bot\b/i.test(body);
          if (emptyChallenge || overlayChallenge) return JSON.stringify({ blocked: true, matches: [] });

          const aisleText = /\bAisle\s+[A-Za-z0-9][^\n]{0,30}/i;
          const mentions = el => ((el.innerText || '').match(/\bAisle\s+[A-Za-z0-9]/gi) || []).length;
          const leaves = [...document.querySelectorAll('body *')]
            .filter(el => el.children.length === 0 && aisleText.test(el.textContent || ''));

          const clean = text => (text || '').split('\n')[0].trim();
          const matches = [];
          const seen = new Set();
          for (const leaf of leaves) {
            const aisle = (leaf.textContent.match(aisleText) || [''])[0].trim().replace(/^Aisle\s+/i, '');

            let card = leaf;
            while (card.parentElement && card.parentElement !== document.body && mentions(card.parentElement) === 1) card = card.parentElement;

            let name, url;
            if (leaves.length === 1) {
              name = clean((document.querySelector('h1') || {}).innerText);
              url = location.href;
            } else {
              const candidates = [...card.querySelectorAll('a[href]')]
                .map(a => ({ text: clean(a.innerText) || clean(a.getAttribute('aria-label')) || clean((a.querySelector('img') || {}).alt), href: a.href }))
                .filter(c => c.text && c.text.length <= 160 && !/^(add|view|see|shop)\b|cart|coupon/i.test(c.text));
              candidates.sort((a, b) => b.text.length - a.text.length);
              if (candidates.length === 0) continue;
              name = candidates[0].text;
              url = candidates[0].href;
            }

            if (!name || seen.has(url)) continue;
            seen.add(url);
            matches.push({ name, aisle, url });
            if (matches.length >= 30) break;
          }

          return JSON.stringify({ blocked: false, matches });
        })()
        """;

    // Runs in a chain's store-finder page. Returns each link to a location's own page with the text of the box
    // around it, which carries that location's name and street address.
    private const string ReadLocationsScript = """
        (() => {
          const isLocation = a => a.href.includes(__MARKER__);
          const locationsIn = el => new Set([...el.querySelectorAll('a[href]')].filter(isLocation).map(a => a.href)).size;
          const seen = new Set();
          const links = [];
          for (const a of document.querySelectorAll('a[href]')) {
            if (!isLocation(a) || seen.has(a.href)) continue;
            seen.add(a.href);

            // A location's box is the largest one around its link that doesn't also take in another location.
            let card = a;
            while (card.parentElement && card.parentElement !== document.body && locationsIn(card.parentElement) === 1) card = card.parentElement;

            // The link's own words go first, as the name to show; the box supplies the street address.
            links.push({ href: a.href, text: ((a.innerText || '').split('\n')[0].trim() + '\n' + (card.innerText || '')).slice(0, 600) });
          }
          return JSON.stringify(links);
        })()
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The connected browser's address, e.g. "http://192.168.1.10:9222"; null when none is set up.</summary>
    public async Task<string?> GetAddressAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.AppSettings.FindAsync(AddressKey))?.Value is { Length: > 0 } address ? address : null;
    }

    public async Task SetAddressAsync(string? address)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(AddressKey) ?? db.Add(new AppSetting { Key = AddressKey }).Entity;
        setting.Value = string.IsNullOrWhiteSpace(address) ? null : address.Trim().TrimEnd('/');
        await db.SaveChangesAsync();
    }

    /// <summary>Checks the browser can be reached and says which browser answered.</summary>
    public async Task<(bool Ok, string Message)> TestAsync(string? address)
    {
        if (ParseAddress(address) is not { } browser)
        {
            return (false, "Enter the browser's address as http://host:port.");
        }

        try
        {
            using var response = await SendAsync(HttpMethod.Get, browser, "json/version", CancellationToken.None);
            response.EnsureSuccessStatusCode();
            var version = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (true, $"Connected to {version.GetProperty("Browser").GetString()}.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return (false, $"Couldn't reach a browser at {address}: {e.Message}");
        }
    }

    /// <summary>Searches the store's website for <paramref name="query"/> and returns the products it lists with their aisles.</summary>
    public async Task<AisleSearchResult> SearchAsync(Store store, string query, CancellationToken cancellationToken = default)
    {
        if (ParseAddress(await GetAddressAsync()) is not { } browser)
        {
            return new([], NoBrowser);
        }

        if (StoreCatalog.BuildSearchUri(store.SearchUrl, query) is not { } page)
        {
            return new([], "This store needs a search address that starts with https:// and contains {item}.");
        }

        // A store that doesn't have its location number yet gets it now, from its address, and keeps it: nobody
        // should have to set that up by hand before their first lookup.
        var chain = StoreCatalog.Find(store.Name);
        if (chain?.Locations is not null && StoreCatalog.CleanLocationId(store.WebsiteStoreId) is null)
        {
            var (found, _) = await FindLocationAsync(store, cancellationToken);
            if (found is not null)
            {
                store.WebsiteStoreId = found;
                if (store.Id != 0)
                {
                    await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                    await db.Stores.Where(s => s.Id == store.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.WebsiteStoreId, found), cancellationToken);
                }
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PageTimeout);
        try
        {
            await using var tab = await OpenTabAsync(browser, timeout.Token);

            // Aisles differ between a chain's locations, so tell the site which one this store is before searching.
            // Without a number the site falls back on whichever location the browser used last, often the nearest.
            string? notice = null;
            if (chain?.Locations is { } picker)
            {
                if (StoreCatalog.CleanLocationId(store.WebsiteStoreId) is { } locationId)
                {
                    await tab.SendAsync("Network.setCookie",
                        new { name = picker.CookieName, value = locationId, domain = picker.CookieDomain, path = "/", secure = true }, timeout.Token);
                }
                else
                {
                    notice = $"Pantrix couldn't work out which {chain.Name} location this is from its address, so these aisles are for whichever location the browser last used. Edit the store to check its address or enter its store number.";
                }
            }

            await tab.SendAsync("Page.navigate", new { url = page.AbsoluteUri }, timeout.Token);

            // The page fills in after it loads (and a site's own check may reload it), so keep reading until
            // products appear or the wait is up; what the last reading saw says why there were none.
            var giveUpAt = DateTime.UtcNow + ReadFor;
            PageReading? reading = null;
            for (var attempt = 1; DateTime.UtcNow < giveUpAt; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt == 1 ? 3 : 2), timeout.Token);
                reading = await tab.EvaluateAsync<PageReading>(ReadAislesScript, timeout.Token);

                // A human check won't clear by waiting; say so straight away.
                if (reading is { Matches.Count: > 0 } or { Blocked: true })
                {
                    break;
                }
            }

            return reading switch
            {
                null => new([], "The page didn't load."),
                { Blocked: true } => new([], HumanCheck(store.Name)),
                { Matches.Count: 0 } => new([], "The page loaded but showed no aisles. Check the connected browser shows a store selected on the site. A store that only shows aisles in its phone app has to stay manual."),
                _ => new(reading.Matches.Select(m => new AisleMatch(m.Name, m.Aisle, m.Url)).ToList(), Notice: notice)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new([], $"{store.Name}'s site didn't show any aisles in time. Open the connected browser and check the site loads.");
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(e, "Aisle lookup through the browser failed");
            return new([], $"Couldn't use the connected browser: {e.Message}");
        }
    }

    /// <summary>
    /// Finds the number a chain's website uses for the location at the store's address, by reading the site's
    /// own store finder. Returns the number, or a message saying why it couldn't be found.
    /// </summary>
    public async Task<(string? LocationId, string Message)> FindLocationAsync(Store store, CancellationToken cancellationToken = default)
    {
        if (StoreCatalog.Find(store.Name) is not { Locations: { } picker } chain)
        {
            return (null, "Pantrix doesn't know how this store's website numbers its locations.");
        }

        var place = string.Join(" ", new[] { store.City, store.State, store.PostalCode }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (place.Length == 0 || string.IsNullOrWhiteSpace(store.StreetAddress))
        {
            return (null, "Fill in the street address and city first, so the right location can be recognised.");
        }

        if (ParseAddress(await GetAddressAsync()) is not { } browser)
        {
            return (null, NoBrowser);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PageTimeout);
        try
        {
            await using var tab = await OpenTabAsync(browser, timeout.Token);
            var finder = picker.LocatorUrl.Replace("{place}", Uri.EscapeDataString(place), StringComparison.OrdinalIgnoreCase);
            await tab.SendAsync("Page.navigate", new { url = finder }, timeout.Token);

            var script = ReadLocationsScript.Replace("__MARKER__", JsonSerializer.Serialize(picker.LinkMarker));
            var giveUpAt = DateTime.UtcNow + ReadFor;
            List<LocationLink> links = [];
            while (links.Count == 0 && DateTime.UtcNow < giveUpAt)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
                links = await tab.EvaluateAsync<List<LocationLink>>(script, timeout.Token) ?? [];
            }

            if (links.Count == 0)
            {
                return (null, $"{chain.Name}'s store finder didn't list any locations. {picker.Help}");
            }

            return StoreCatalog.MatchLocation(store.StreetAddress, links.Select(l => (l.Href, l.Text))) is { } found
                ? (found.Id, $"Found it: {found.Label} is store {found.Id}.")
                : (null, $"None of the {links.Count} locations near {place} matched that street address. {picker.Help}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, $"{chain.Name}'s store finder didn't load in time. {picker.Help}");
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(e, "Store finder lookup through the browser failed");
            return (null, $"Couldn't use the connected browser: {e.Message}");
        }
    }

    private const string NoBrowser = "No browser is connected. An admin can set one up on the Admin page.";

    private static string HumanCheck(string storeName) =>
        $"{storeName}'s site is asking to confirm you're a person. Open the connected browser, complete the check there, then try again.";

    // Each task gets a tab of its own in the shared browser, closed again afterwards.
    private async Task<Tab> OpenTabAsync(Uri browser, CancellationToken cancellationToken)
    {
        using var opened = await SendAsync(HttpMethod.Put, browser, "json/new?about:blank", cancellationToken);
        opened.EnsureSuccessStatusCode();
        var id = (await opened.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetString()!;

        var socket = new ClientWebSocket();
        try
        {
            if (!IPAddress.TryParse(browser.Host, out _))
            {
                socket.Options.SetRequestHeader("Host", "localhost");
            }

            await socket.ConnectAsync(new Uri($"ws://{browser.Authority}/devtools/page/{id}"), cancellationToken);
            return new Tab(this, browser, id, socket);
        }
        catch
        {
            socket.Dispose();
            await CloseTabAsync(browser, id);
            throw;
        }
    }

    private async Task CloseTabAsync(Uri browser, string id)
    {
        try
        {
            using var closed = await SendAsync(HttpMethod.Get, browser, $"json/close/{id}", CancellationToken.None);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(e, "Couldn't close the lookup tab");
        }
    }

    // Chrome only answers its remote-control port when addressed as localhost or by IP, so say "localhost"
    // whatever name the container is reached by.
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri browser, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(browser, path));
        if (!IPAddress.TryParse(browser.Host, out _))
        {
            request.Headers.Host = "localhost";
        }

        return await http.SendAsync(request, cancellationToken);
    }

    private static Uri? ParseAddress(string? address) =>
        Uri.TryCreate(address?.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp ? uri : null;

    /// <summary>One browser tab under remote control.</summary>
    private sealed class Tab(StoreBrowserService owner, Uri browser, string id, ClientWebSocket socket) : IAsyncDisposable
    {
        private int _lastCommand;

        /// <summary>Sends one command and returns the "result" part of the browser's reply.</summary>
        public async Task<JsonElement> SendAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            var commandId = ++_lastCommand;
            var request = JsonSerializer.Serialize(new { id = commandId, method, @params = parameters });
            await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);

            // The browser also sends events nobody asked for; skip to the reply carrying this command's id.
            var buffer = new byte[64 * 1024];
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await socket.ReceiveAsync(buffer, cancellationToken);
                    message.Write(buffer, 0, received.Count);
                } while (!received.EndOfMessage);

                using var reply = JsonDocument.Parse(message.ToArray());
                if (reply.RootElement.TryGetProperty("id", out var replyId) && replyId.GetInt32() == commandId)
                {
                    return reply.RootElement.TryGetProperty("result", out var result) ? result.Clone() : default;
                }
            }
        }

        /// <summary>Runs a script in the page that returns JSON text, and reads that as <typeparamref name="T"/>.</summary>
        public async Task<T?> EvaluateAsync<T>(string script, CancellationToken cancellationToken) where T : class
        {
            var result = await SendAsync("Runtime.evaluate", new { expression = script, returnByValue = true }, cancellationToken);
            return result.ValueKind == JsonValueKind.Object
                   && result.TryGetProperty("result", out var value)
                   && value.TryGetProperty("value", out var text)
                   && text.ValueKind == JsonValueKind.String
                ? JsonSerializer.Deserialize<T>(text.GetString()!, Json)
                : null;
        }

        public async ValueTask DisposeAsync()
        {
            socket.Dispose();
            await owner.CloseTabAsync(browser, id);
        }
    }

    private sealed record PageReading(bool Blocked, List<PageMatch> Matches);

    private sealed record PageMatch(string Name, string Aisle, string? Url);

    private sealed record LocationLink(string Href, string Text);
}
