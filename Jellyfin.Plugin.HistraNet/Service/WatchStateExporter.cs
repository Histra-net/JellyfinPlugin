using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Pushes local watched/unwatched changes to histra.net in real time, as the
/// user toggles them during normal use.
/// </summary>
public sealed class WatchStateExporter : IHostedService, IDisposable
{
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
        // React only to the user manually toggling watched/unwatched.
        //
        // PlaybackFinished is deliberately NOT handled: finishing playback already
        // reaches histra.net through the scrobbler's "stop" action, which marks the
        // title watched. Exporting here as well wrote the same watch twice and
        // produced duplicate history entries.
        //
        // Import is also skipped, otherwise our own sync writes would loop back.
        if (e.SaveReason is not UserDataSaveReason.TogglePlayed)
        {
            return;
        }

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

    /// <inheritdoc />
    public void Dispose()
    {
        // Event unsubscribed in StopAsync.
    }
}
