using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pantrix.Data;

namespace Pantrix.Services;

/// <summary>A product found on a store's website, with where the site says it is.</summary>
public record AisleMatch(string Name, string Aisle, string? Url);

public record AisleSearchResult(IReadOnlyList<AisleMatch> Matches, string? Error = null);

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
            return new([], "No browser is connected. An admin can set one up on the Admin page.");
        }

        if (StoreCatalog.BuildSearchUri(store.SearchUrl, query) is not { } page)
        {
            return new([], "This store needs a search address that starts with https:// and contains {item}.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PageTimeout);
        string? tabId = null;
        try
        {
            // Each lookup gets a tab of its own in the shared browser, closed again afterwards.
            using var opened = await SendAsync(HttpMethod.Put, browser, $"json/new?{page.AbsoluteUri}", timeout.Token);
            opened.EnsureSuccessStatusCode();
            tabId = (await opened.Content.ReadFromJsonAsync<JsonElement>(timeout.Token)).GetProperty("id").GetString();

            using var socket = new ClientWebSocket();
            if (!IPAddress.TryParse(browser.Host, out _))
            {
                socket.Options.SetRequestHeader("Host", "localhost");
            }

            await socket.ConnectAsync(new Uri($"ws://{browser.Authority}/devtools/page/{tabId}"), timeout.Token);

            // The page fills in after it loads (and a site's own check may reload it), so keep reading until
            // products appear or the wait is up; what the last reading saw says why there were none.
            var giveUpAt = DateTime.UtcNow + ReadFor;
            PageReading? reading = null;
            for (var attempt = 1; DateTime.UtcNow < giveUpAt; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt == 1 ? 3 : 2), timeout.Token);
                reading = await ReadPageAsync(socket, attempt, timeout.Token);

                // A human check won't clear by waiting; say so straight away.
                if (reading is { Matches.Count: > 0 } or { Blocked: true })
                {
                    break;
                }
            }

            return ToResult(reading, store);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new([], $"{store.Name}'s site didn't show any aisles in time. Open the connected browser and check the site loads and has your store selected.");
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(e, "Aisle lookup through the browser failed");
            return new([], $"Couldn't use the connected browser: {e.Message}");
        }
        finally
        {
            if (tabId is not null)
            {
                try
                {
                    using var closed = await SendAsync(HttpMethod.Get, browser, $"json/close/{tabId}", CancellationToken.None);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
                {
                    logger.LogDebug(e, "Couldn't close the lookup tab");
                }
            }
        }
    }

    private static AisleSearchResult ToResult(PageReading? reading, Store store) => reading switch
    {
        null => new([], "The page didn't load."),
        { Blocked: true } => new([], $"{store.Name}'s site is asking to confirm you're a person. Open the connected browser, complete the check there, then try again."),
        { Matches.Count: 0 } => new([], "The page loaded but showed no aisles. Check the connected browser has your store selected on the site."),
        _ => new(reading.Matches.Select(m => new AisleMatch(m.Name, m.Aisle, m.Url)).ToList())
    };

    private static async Task<PageReading?> ReadPageAsync(ClientWebSocket socket, int id, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Serialize(new
        {
            id,
            method = "Runtime.evaluate",
            @params = new { expression = ReadAislesScript, returnByValue = true }
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);

        // The browser also sends events we didn't ask for; skip to the reply carrying our id.
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
            if (!reply.RootElement.TryGetProperty("id", out var replyId) || replyId.GetInt32() != id)
            {
                continue;
            }

            return reply.RootElement.TryGetProperty("result", out var result)
                   && result.TryGetProperty("result", out var value)
                   && value.TryGetProperty("value", out var text)
                   && text.ValueKind == JsonValueKind.String
                ? JsonSerializer.Deserialize<PageReading>(text.GetString()!, Json)
                : null;
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

    private sealed record PageReading(bool Blocked, List<PageMatch> Matches);

    private sealed record PageMatch(string Name, string Aisle, string? Url);
}
