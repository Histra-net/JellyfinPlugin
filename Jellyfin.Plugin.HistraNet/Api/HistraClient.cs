using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Thin client for the histra.net tracking API. The caller supplies the
/// per-user token on each call.
/// </summary>
public class HistraClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HistraClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistraClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public HistraClient(IHttpClientFactory httpClientFactory, ILogger<HistraClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static string BaseUrl =>
        (Plugin.Instance?.Configuration.BaseUrl ?? "https://histra.net").TrimEnd('/');

    /// <summary>
    /// Sends a scrobble action to histra.net under the given user's token.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="request">The scrobble request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scrobble response, or null on failure.</returns>
    public async Task<ScrobbleResponse?> ScrobbleAsync(string token, ScrobbleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            using var response = await http.PostAsJsonAsync(
                "/api/v1/scrobble",
                request,
                _jsonOptions,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "histra.net scrobble '{Action}' failed: {StatusCode} {Body}",
                    request.Action,
                    (int)response.StatusCode,
                    body);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<ScrobbleResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net scrobble request failed");
            return null;
        }
    }

    /// <summary>
    /// Verifies a token by calling /api/v1/auth/me.
    /// </summary>
    /// <param name="token">The histra.net API token to verify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authenticated user, or null on failure.</returns>
    public async Task<AuthMe?> GetMeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            using var response = await http.GetAsync("/api/v1/auth/me", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<AuthMe>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net auth check failed");
            return null;
        }
    }

    /// <summary>
    /// Fetches the user's tracking statistics (dashboard data).
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stats, or null on failure.</returns>
    public async Task<TrackingStats?> GetStatsAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            using var response = await http.GetAsync("/api/v1/tracking/stats", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net stats request failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<TrackingStats>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net stats request failed");
            return null;
        }
    }

    /// <summary>
    /// Fetches one page of the user's watch history (newest first).
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="limit">Page size.</param>
    /// <param name="offset">Page offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history page, or null on failure.</returns>
    public async Task<HistoryResponse?> GetHistoryPageAsync(string token, int limit, int offset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "/api/v1/tracking/history?limit={0}&offset={1}",
                limit,
                offset);
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net history request failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<HistoryResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net history request failed");
            return null;
        }
    }

    /// <summary>
    /// Marks a title watched (POST) or unwatched (DELETE) on histra.net.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="watched">True to mark watched (POST), false to reset (DELETE).</param>
    /// <param name="request">The watched request body (movie or show+episode).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True on success.</returns>
    public async Task<bool> SetWatchedAsync(string token, bool watched, WatchedRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using var http = CreateClient(token);
            using var content = JsonContent.Create(request, options: _jsonOptions);
            using var message = new HttpRequestMessage(
                watched ? HttpMethod.Post : HttpMethod.Delete,
                "/api/v1/watched")
            {
                Content = content
            };

            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (Plugin.Instance?.Configuration.EnableDebugLogging ?? false)
                {
                    var sent = JsonSerializer.Serialize(request, _jsonOptions);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "histra.net watched {Verb} failed: {StatusCode}; sent {Sent}; response {Body}",
                        watched ? "POST" : "DELETE",
                        (int)response.StatusCode,
                        sent,
                        body);
                }
                else
                {
                    _logger.LogWarning(
                        "histra.net watched {Verb} failed: {StatusCode}",
                        watched ? "POST" : "DELETE",
                        (int)response.StatusCode);
                }

                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net watched request failed");
            return false;
        }
    }

    /// <summary>
    /// Adds a title to (POST) or removes it from (DELETE) the user's histra.net
    /// favorites. Both verbs are idempotent on the server: adding an existing
    /// favorite or removing a missing one returns 200.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="favorite">True to add (POST), false to remove (DELETE).</param>
    /// <param name="request">The favorite request body (movie or show).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True on success.</returns>
    public async Task<bool> SetFavoriteAsync(string token, bool favorite, FavoriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using var http = CreateClient(token);
            using var content = JsonContent.Create(request, options: _jsonOptions);
            using var message = new HttpRequestMessage(
                favorite ? HttpMethod.Post : HttpMethod.Delete,
                "/api/v1/favorites")
            {
                Content = content
            };

            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (Plugin.Instance?.Configuration.EnableDebugLogging ?? false)
                {
                    var sent = JsonSerializer.Serialize(request, _jsonOptions);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "histra.net favorites {Verb} failed: {StatusCode}; sent {Sent}; response {Body}",
                        favorite ? "POST" : "DELETE",
                        (int)response.StatusCode,
                        sent,
                        body);
                }
                else
                {
                    _logger.LogWarning(
                        "histra.net favorites {Verb} failed: {StatusCode}",
                        favorite ? "POST" : "DELETE",
                        (int)response.StatusCode);
                }

                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net favorites request failed");
            return false;
        }
    }

    /// <summary>
    /// Checks whether a single histra.net title is in the user's favorites.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="mediaType">"movie" or "show".</param>
    /// <param name="mediaId">The histra media id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The favorite flag, or null on failure.</returns>
    public async Task<bool?> GetFavoriteStatusAsync(string token, string mediaType, long mediaId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(mediaType))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "/api/v1/favorites/status?media_type={0}&media_id={1}",
                Uri.EscapeDataString(mediaType),
                mediaId);
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net favorite status request failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var status = await response.Content
                .ReadFromJsonAsync<FavoriteStatus>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return status?.Favorite;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net favorite status request failed");
            return null;
        }
    }

    /// <summary>
    /// Fetches one page of the user's favorites (extended=full, with external ids).
    /// An empty favorites list is returned as a page with no items, not as an error.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="limit">Page size.</param>
    /// <param name="offset">Page offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The favorites page, or null on failure.</returns>
    public async Task<FavoritesResponse?> GetFavoritesPageAsync(string token, int limit, int offset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "/api/v1/favorites?extended=full&limit={0}&offset={1}",
                limit,
                offset);
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net favorites request failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<FavoritesResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net favorites request failed");
            return null;
        }
    }

    /// <summary>
    /// Fetches in-progress movies (continue watching).
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="limit">Maximum items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The continue-watching response, or null on failure.</returns>
    public Task<ContinueResponse?> GetContinueMoviesAsync(string token, int limit, CancellationToken cancellationToken) =>
        GetContinueAsync(token, "/api/v1/scrobble/continue", limit, cancellationToken);

    /// <summary>
    /// Fetches in-progress episodes (continue watching).
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="limit">Maximum items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The continue-watching response, or null on failure.</returns>
    public Task<ContinueResponse?> GetContinueEpisodesAsync(string token, int limit, CancellationToken cancellationToken) =>
        GetContinueAsync(token, "/api/v1/scrobble/episodes/continue", limit, cancellationToken);

    private async Task<ContinueResponse?> GetContinueAsync(string token, string path, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            using var http = CreateClient(token);
            var url = string.Format(CultureInfo.InvariantCulture, "{0}?limit={1}", path, limit);
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net continue request failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<ContinueResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net continue request failed");
            return null;
        }
    }

    /// <summary>
    /// Marks a batch of titles watched or unwatched in a single request.
    /// </summary>
    /// <param name="token">The histra.net API token.</param>
    /// <param name="watched">True → /sync/watched, false → /sync/unwatched.</param>
    /// <param name="request">The batch of movies/episodes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response counts, or null on failure.</returns>
    public async Task<SyncWatchedResponse?> SyncWatchedAsync(string token, bool watched, SyncWatchedRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var path = watched ? "/api/v1/sync/watched" : "/api/v1/sync/unwatched";

        try
        {
            using var http = CreateClient(token);
            using var response = await http.PostAsJsonAsync(path, request, _jsonOptions, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("histra.net batch sync failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<SyncWatchedResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "histra.net batch sync request failed");
            return null;
        }
    }

    private HttpClient CreateClient(string token)
    {
        var http = _httpClientFactory.CreateClient(NamedClient.Default);
        http.BaseAddress = new Uri(BaseUrl, UriKind.Absolute);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.Timeout = TimeSpan.FromSeconds(20);
        return http;
    }
}
