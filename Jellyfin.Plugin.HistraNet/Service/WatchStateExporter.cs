using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Pushes local watched/unwatched and favorite (heart) changes to histra.net in
/// real time, as the user toggles them during normal use.
/// </summary>
public sealed class WatchStateExporter : IHostedService, IDisposable
{
    // Favorites: the latest state the user asked for, recorded synchronously on
    // the event thread (events arrive in save order), and the state last
    // confirmed by histra.net. The export worker always sends the LATEST desired
    // state under a single gate, so a quick on/off toggle can never reach
    // histra.net out of order.
    private readonly ConcurrentDictionary<(Guid User, Guid Item), bool> _favoriteDesired = new();
    private readonly ConcurrentDictionary<(Guid User, Guid Item), bool> _favoriteSent = new();
    private readonly SemaphoreSlim _favoriteGate = new(1, 1);

    private readonly IUserDataManager _userDataManager;
    private readonly IUserTokenProvider _tokenProvider;
    private readonly Api.HistraClient _client;
    private readonly ILogger<WatchStateExporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchStateExporter"/> class.
    /// </summary>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="tokenProvider">The token provider.</param>
    /// <param name="client">The histra.net client.</param>
    /// <param name="logger">The logger.</param>
    public WatchStateExporter(
        IUserDataManager userDataManager,
        IUserTokenProvider tokenProvider,
        Api.HistraClient client,
        ILogger<WatchStateExporter> logger)
    {
        _userDataManager = userDataManager;
        _tokenProvider = tokenProvider;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        return Task.CompletedTask;
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        switch (e.SaveReason)
        {
            case UserDataSaveReason.TogglePlayed:
                OnWatchedToggled(e);
                break;

            // Jellyfin 10.11: the heart button (POST/DELETE /UserFavoriteItems/{id})
            // saves with UpdateUserRating. The generic POST /UserItems/{id}/UserData
            // (used by some clients) can set IsFavorite too and saves with
            // UpdateUserData.
            case UserDataSaveReason.UpdateUserRating:
            case UserDataSaveReason.UpdateUserData:
                OnFavoriteMaybeChanged(e);
                break;

            // Our own sync task writes favorites (and watched/progress) with
            // Import. Never export those back, that would loop. Just forget what
            // we know about the item: its state now comes from histra.net.
            case UserDataSaveReason.Import:
                ForgetFavoriteState(e);
                break;
        }
    }

    private void OnWatchedToggled(UserDataSaveEventArgs e)
    {
        // React only to the user manually toggling watched/unwatched.
        //
        // PlaybackFinished is deliberately NOT handled: finishing playback already
        // reaches histra.net through the scrobbler's "stop" action, which marks the
        // title watched. Exporting here as well wrote the same watch twice and
        // produced duplicate history entries.
        //
        // Import is also skipped (see OnUserDataSaved), otherwise our own sync
        // writes would loop back.
        var config = Plugin.Instance?.Configuration;
        if (config is null || e.Item is null || e.UserData is null)
        {
            return;
        }

        var watched = e.UserData.Played;
        if (watched && !config.ExportWatchedOnChange)
        {
            return;
        }

        if (!watched && !config.ExportUnwatchedOnChange)
        {
            return;
        }

        // Copy values needed off the event thread before going async.
        var userId = e.UserId;
        var item = e.Item;
        _ = Task.Run(() => ExportAsync(userId, item, watched));
    }

    private async Task ExportAsync(Guid userId, MediaBrowser.Controller.Entities.BaseItem item, bool watched)
    {
        try
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            var token = await _tokenProvider.GetTokenAsync(userId, CancellationToken.None).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var request = MediaRefBuilder.BuildWatched(item);
            if (request is null)
            {
                return;
            }

            var ok = await _client.SetWatchedAsync(token, watched, request, CancellationToken.None).ConfigureAwait(false);
            if (ok && (Plugin.Instance?.Configuration.EnableDebugLogging ?? false))
            {
                _logger.LogInformation(
                    "histra.net: realtime export '{Name}' -> {State}",
                    item.Name,
                    watched ? "watched" : "unwatched");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "histra.net: realtime export failed");
        }
    }

    private void OnFavoriteMaybeChanged(UserDataSaveEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.ExportFavoritesOnChange || e.UserData is null || e.UserId == Guid.Empty)
        {
            return;
        }

        // histra.net favorites are movies and shows only. Episodes, seasons, box
        // sets, people etc. can be hearted in Jellyfin too: silently ignore them.
        if (e.Item is not (Movie or Series))
        {
            return;
        }

        // Copy values needed off the event thread before going async.
        var item = e.Item;
        var favorite = e.UserData.IsFavorite;
        var key = (e.UserId, item.Id);

        if (_favoriteDesired.TryGetValue(key, out var previous))
        {
            // The same save reasons also fire for Likes and other user-data
            // edits: nothing to do when the heart itself did not change.
            if (previous == favorite)
            {
                return;
            }
        }
        else if (e.SaveReason == UserDataSaveReason.UpdateUserData && !favorite)
        {
            // Generic user-data update with no known previous heart state: we
            // cannot tell it was un-hearted, so don't risk deleting a histra.net
            // favorite that simply has not been imported yet. An explicit heart
            // click (UpdateUserRating) is always exported.
            return;
        }

        _favoriteDesired[key] = favorite;
        _ = Task.Run(() => ExportFavoriteAsync(key, item));
    }

    private void ForgetFavoriteState(UserDataSaveEventArgs e)
    {
        if (e.Item is null)
        {
            return;
        }

        var key = (e.UserId, e.Item.Id);
        _favoriteDesired.TryRemove(key, out _);
        _favoriteSent.TryRemove(key, out _);
    }

    private async Task ExportFavoriteAsync((Guid User, Guid Item) key, BaseItem item)
    {
        try
        {
            await _favoriteGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Send the latest desired state, not the one captured when this
                // task was queued: a later toggle supersedes an earlier one.
                if (!_favoriteDesired.TryGetValue(key, out var favorite))
                {
                    return; // forgotten by an import in the meantime
                }

                if (_favoriteSent.TryGetValue(key, out var sent) && sent == favorite)
                {
                    return;
                }

                var request = MediaRefBuilder.BuildFavorite(item);
                if (request is null)
                {
                    return; // no tmdb/imdb/tvdb id, histra.net cannot identify it
                }

                var token = await _tokenProvider.GetTokenAsync(key.User, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                if (!await _client.SetFavoriteAsync(token, favorite, request, CancellationToken.None).ConfigureAwait(false))
                {
                    return;
                }

                _favoriteSent[key] = favorite;
                if (Plugin.Instance?.Configuration.EnableDebugLogging ?? false)
                {
                    _logger.LogInformation(
                        "histra.net: realtime favorite export '{Name}' -> {State}",
                        item.Name,
                        favorite ? "favorite" : "not favorite");
                }
            }
            finally
            {
                _favoriteGate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "histra.net: realtime favorite export failed");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Event unsubscribed in StopAsync.
        _favoriteGate.Dispose();
    }
}
