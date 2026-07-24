using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Resolves a user's histra.net token from the company manager over HTTP.
/// Sends { apptoken, userid } and expects { token } back. Results are cached
/// briefly so frequent playback events do not hammer the manager.
/// </summary>
public class ManagerUserTokenProvider : IUserTokenProvider
{
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ManagerUserTokenProvider> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerUserTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public ManagerUserTokenProvider(IHttpClientFactory httpClientFactory, ILogger<ManagerUserTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null
            || string.IsNullOrWhiteSpace(config.ManagerUrl)
            || string.IsNullOrWhiteSpace(config.ManagerAppToken))
        {
            return null;
        }

        // Serve from cache when fresh. (Uses UtcNow, not a testable clock, but
        // this runs live in the server and never inside a resumable workflow.)
        if (_cache.TryGetValue(userId, out var cached) && cached.Expires > DateTime.UtcNow)
        {
            return cached.Token;
        }

        try
        {
            var http = _httpClientFactory.CreateClient(NamedClient.Default);
            http.Timeout = TimeSpan.FromSeconds(15);

            var body = new ManagerRequest
            {
                AppToken = config.ManagerAppToken,
                UserId = userId.ToString("N")
            };

            using var response = await http.PostAsJsonAsync(config.ManagerUrl, body, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net manager token lookup failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<ManagerResponse>(cancellationToken)
                .ConfigureAwait(false);

            var token = string.IsNullOrWhiteSpace(result?.Token) ? null : result!.Token;
            _cache[userId] = new CacheEntry(token, DateTime.UtcNow.Add(_cacheTtl));

            if (Plugin.Instance?.Configuration.EnableDebugLogging ?? false)
            {
                _logger.LogInformation(
                    "histra.net manager lookup for {UserId}: {Result}",
                    userId.ToString("N"),
                    token is null ? "no token" : "token received");
            }

            return token;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net manager token lookup failed");
            return null;
        }
    }

    private sealed record CacheEntry(string? Token, DateTime Expires);

    private sealed class ManagerRequest
    {
        [JsonPropertyName("apptoken")]
        public string AppToken { get; set; } = string.Empty;

        [JsonPropertyName("userid")]
        public string UserId { get; set; } = string.Empty;
    }

    private sealed class ManagerResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }
}
