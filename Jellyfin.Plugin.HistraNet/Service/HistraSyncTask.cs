using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.HistraNet.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Scheduled task that synchronizes watch state between histra.net and Jellyfin.
/// This class implements the import direction (histra.net → Jellyfin); the export
/// direction (Jellyfin → histra.net) is added in a later phase.
/// </summary>
public class HistraSyncTask : IScheduledTask
{
    private const int PageSize = 100;

    private const int ExportConcurrency = 6;
    private const int BatchSize = 100;

    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserTokenProvider _tokenProvider;
    private readonly HistraClient _client;
    private readonly ExportStateStore _exportState;
    private readonly ILogger<HistraSyncTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistraSyncTask"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="tokenProvider">The token provider.</param>
    /// <param name="client">The histra.net client.</param>
    /// <param name="exportState">Remembers what was already exported per user.</param>
    /// <param name="logger">The logger.</param>
    public HistraSyncTask(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserTokenProvider tokenProvider,
        HistraClient client,
        ExportStateStore exportState,
        ILogger<HistraSyncTask> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _tokenProvider = tokenProvider;
        _client = client;
        _exportState = exportState;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync with histra.net";

    /// <inheritdoc />
    public string Key => "HistraNetSync";

    /// <inheritdoc />
    public string Description => "Imports watched state and playback progress from histra.net, and exports local watch state to histra.net.";

    /// <inheritdoc />
    public string Category => "histra.net";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
        new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            }
        };

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        var users = _userManager.GetUsers().ToList();
        if (users.Count == 0)
        {
            progress.Report(100);
            return;
        }

        // Build the library index once for the whole run: import resolves titles
        // by external id in-memory (O(1)) instead of one DB query per entry.
        var index = LibraryIndex.Build(_libraryManager);

        var step = 100.0 / users.Count;
        var done = 0;

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var token = await _tokenProvider.GetTokenAsync(user.Id, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                await ImportForUserAsync(user, token, config, index, cancellationToken).ConfigureAwait(false);
                await ImportProgressForUserAsync(user, token, config, index, cancellationToken).ConfigureAwait(false);
                await ExportForUserAsync(user, token, config, cancellationToken).ConfigureAwait(false);
            }

            done++;
            progress.Report(step * done);
        }

        progress.Report(100);
    }

    private async Task ImportForUserAsync(
        User user,
        string token,
        Configuration.PluginConfiguration config,
        LibraryIndex index,
        CancellationToken cancellationToken)
    {
        // Both watched and unwatched import off → nothing to do.
        if (config.SkipWatchedImport && config.SkipUnwatchedImport)
        {
            return;
        }

        // Page through histra history once. Mark matching library items played
        // (unless watched import is skipped) and remember which library items are
        // watched on histra — used below to reconcile unwatched state.
        var marked = 0;
        var offset = 0;
        var watchedOnHistra = new HashSet<Guid>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _client.GetHistoryPageAsync(token, PageSize, offset, cancellationToken).ConfigureAwait(false);
            if (page is null || page.Entries.Length == 0)
            {
                break;
            }

            foreach (var entry in page.Entries)
            {
                if (entry.ExternalIds is null || entry.ExternalIds.Count == 0)
                {
                    continue;
                }

                var item = entry.IsEpisode
                    ? index.FindEpisode(entry.ExternalIds, entry.SeasonNumber, entry.EpisodeNumber)
                    : index.FindMovie(entry.ExternalIds);
                if (item is null)
                {
                    continue;
                }

                watchedOnHistra.Add(item.Id);

                if (!config.SkipWatchedImport
                    && await MarkPlayedAsync(user, item, cancellationToken).ConfigureAwait(false))
                {
                    marked++;
                }
            }

            if (page.Entries.Length < PageSize)
            {
                break;
            }

            offset += PageSize;
        }

        if (config.EnableDebugLogging || marked > 0)
        {
            _logger.LogInformation("histra.net: imported {Count} watched item(s) for user {User}", marked, user.Username);
        }

        // Unwatched reconciliation: when enabled, anything played locally that is
        // NOT watched on histra gets unmarked, making Jellyfin mirror histra.
        if (!config.SkipUnwatchedImport)
        {
            await ReconcileUnwatchedAsync(user, watchedOnHistra, config, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileUnwatchedAsync(
        User user,
        HashSet<Guid> watchedOnHistra,
        Configuration.PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true
        };

        var unmarked = 0;
        foreach (var item in _libraryManager.GetItemList(query))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only touch items played locally that histra does NOT have as watched.
            if (watchedOnHistra.Contains(item.Id))
            {
                continue;
            }

            var data = _userDataManager.GetUserData(user, item);
            if (data is null || !data.Played)
            {
                continue;
            }

            if (await MarkUnplayedAsync(user, item, data, cancellationToken).ConfigureAwait(false))
            {
                unmarked++;
            }
        }

        if (config.EnableDebugLogging || unmarked > 0)
        {
            _logger.LogInformation("histra.net: unmarked {Count} item(s) not watched on histra for user {User}", unmarked, user.Username);
        }
    }

    private async Task ImportProgressForUserAsync(
        User user,
        string token,
        Configuration.PluginConfiguration config,
        LibraryIndex index,
        CancellationToken cancellationToken)
    {
        if (config.SkipPlaybackProgressImport)
        {
            return;
        }

        const int Limit = 100;
        var applied = 0;

        var movies = await _client.GetContinueMoviesAsync(token, Limit, cancellationToken).ConfigureAwait(false);
        if (movies is not null)
        {
            foreach (var it in movies.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = index.FindMovie(it.ExternalIds);
                if (item is not null && await ApplyProgressAsync(user, item, it.Progress, cancellationToken).ConfigureAwait(false))
                {
                    applied++;
                }
            }
        }

        var episodes = await _client.GetContinueEpisodesAsync(token, Limit, cancellationToken).ConfigureAwait(false);
        if (episodes is not null)
        {
            foreach (var it in episodes.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = index.FindEpisode(it.ExternalIds, it.SeasonNumber, it.EpisodeNumber);
                if (item is not null && await ApplyProgressAsync(user, item, it.Progress, cancellationToken).ConfigureAwait(false))
                {
                    applied++;
                }
            }
        }

        if (config.EnableDebugLogging || applied > 0)
        {
            _logger.LogInformation("histra.net: imported playback progress for {Count} item(s) for user {User}", applied, user.Username);
        }
    }

    private async Task<bool> ApplyProgressAsync(User user, BaseItem item, double progressPercent, CancellationToken cancellationToken)
    {
        var runtime = item.RunTimeTicks;
        if (runtime is null || runtime.Value <= 0 || progressPercent <= 0 || progressPercent >= 100)
        {
            return false;
        }

        var data = _userDataManager.GetUserData(user, item);
        if (data is null || data.Played)
        {
            // Don't override a fully-watched item with a resume position.
            return false;
        }

        var positionTicks = (long)(runtime.Value * (progressPercent / 100.0));
        if (positionTicks == data.PlaybackPositionTicks)
        {
            return false;
        }

        data.PlaybackPositionTicks = positionTicks;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _userDataManager.SaveUserData(user, item, data, UserDataSaveReason.Import, cancellationToken);
                return true;
            }
            catch (Exception ex) when (attempt < 5 && IsTransientDbError(ex))
            {
                await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "histra.net: failed to set progress for '{Name}'", item.Name);
                return false;
            }
        }
    }

    private async Task ExportForUserAsync(
        User user,
        string token,
        Configuration.PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        if (!config.ExportWatchedOnScheduledTask)
        {
            return;
        }

        // Enumerate movies and episodes for the user. IsPlayed is per-user data,
        // so we read it via the user data manager (the InternalItemsQuery.IsPlayed
        // filter proved unreliable here). Do NOT pass the user into the query ctor
        // — that applies library visibility filtering that can drop most items.
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query);

        // Phase 1 (fast, in-memory): pick only the items whose exported state
        // changed since last time. Skips everything already on histra.net.
        var changed = new List<(BaseItem Item, bool Watched, double Percent, string Signature)>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var data = _userDataManager.GetUserData(user, item);
            if (data is null)
            {
                continue;
            }

            var watched = data.Played;
            var percent = watched ? 0 : ToPercent(data.PlaybackPositionTicks, item.RunTimeTicks);
            if (!watched && percent <= 0)
            {
                continue; // neither watched nor in-progress → nothing to export
            }

            var signature = ExportStateStore.Signature(watched, percent);
            if (_exportState.HasChanged(user.Id, item.Id, signature))
            {
                changed.Add((item, watched, percent, signature));
            }
        }

        // Phase 2a: watched items go to histra.net in batches of BatchSize via
        // the /sync/watched endpoint — one request per 100 items, not per item.
        var exported = 0;
        var watchedChanges = changed.Where(c => c.Watched).ToList();
        for (var i = 0; i < watchedChanges.Count; i += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = watchedChanges.GetRange(i, Math.Min(BatchSize, watchedChanges.Count - i));

            var request = new SyncWatchedRequest
            {
                Movies = slice.Select(c => MediaRefBuilder.BuildWatched(c.Item)?.Movie)
                    .Where(m => m is not null).Select(m => m!).ToArray(),
                Episodes = slice.Select(c => MediaRefBuilder.BuildWatched(c.Item))
                    .Where(w => w?.Show is not null && w.Episode is not null)
                    .Select(w => new SyncEpisode { Show = w!.Show, Season = w.Episode!.Season, Number = w.Episode.Number })
                    .ToArray()
            };

            if (request.Movies.Length == 0 && request.Episodes.Length == 0)
            {
                continue;
            }

            var result = await _client.SyncWatchedAsync(token, true, request, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                // Record the whole slice as exported (histra accepted the batch).
                foreach (var c in slice)
                {
                    _exportState.Record(user.Id, c.Item.Id, c.Signature);
                }

                exported += slice.Count;
            }
        }

        // Phase 2b: in-progress items still use per-item scrobble (no progress
        // batch endpoint), bounded in parallel.
        var progressChanges = changed.Where(c => !c.Watched).ToList();
        using (var gate = new SemaphoreSlim(ExportConcurrency))
        {
            async Task SendProgressAsync((BaseItem Item, bool Watched, double Percent, string Signature) c)
            {
                try
                {
                    if (await ExportProgressAsync(token, c.Item, c.Percent, cancellationToken).ConfigureAwait(false))
                    {
                        _exportState.Record(user.Id, c.Item.Id, c.Signature);
                        Interlocked.Increment(ref exported);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }

            var tasks = new List<Task>(progressChanges.Count);
            foreach (var c in progressChanges)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                tasks.Add(SendProgressAsync(c));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        _exportState.Save();

        if (config.EnableDebugLogging || exported > 0)
        {
            _logger.LogInformation(
                "histra.net: exported {Exported} changed item(s) (scanned {Scanned}) for user {User}",
                exported,
                items.Count,
                user.Username);
        }

        // Note: bulk "unwatched" export is intentionally NOT performed. Mass
        // DELETE /watched over the whole library would wipe histra state set by
        // other clients. Unwatched changes are exported in real time on toggle.
    }

    private async Task<bool> ExportProgressAsync(string token, BaseItem item, double percent, CancellationToken cancellationToken)
    {
        var scrobble = MediaRefBuilder.BuildScrobble(item, "pause", percent);
        return scrobble is not null
            && await _client.ScrobbleAsync(token, scrobble, cancellationToken).ConfigureAwait(false) is not null;
    }

    private async Task<bool> MarkPlayedAsync(User user, BaseItem item, CancellationToken cancellationToken)
    {
        var data = _userDataManager.GetUserData(user, item);
        if (data is null || data.Played)
        {
            return false;
        }

        data.Played = true;
        if (data.PlayCount < 1)
        {
            data.PlayCount = 1;
        }

        data.LastPlayedDate ??= DateTime.UtcNow;
        return await SaveUserDataWithRetryAsync(user, item, data, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> MarkUnplayedAsync(User user, BaseItem item, UserItemData data, CancellationToken cancellationToken)
    {
        data.Played = false;
        data.PlaybackPositionTicks = 0;
        return await SaveUserDataWithRetryAsync(user, item, data, cancellationToken).ConfigureAwait(false);
    }

    // SaveUserData writes to the shared SQLite database. A concurrent library scan
    // can briefly hold the write lock, so retry transient "database is locked"
    // errors instead of aborting the whole sync.
    private async Task<bool> SaveUserDataWithRetryAsync(User user, BaseItem item, UserItemData data, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _userDataManager.SaveUserData(user, item, data, UserDataSaveReason.Import, cancellationToken);
                return true;
            }
            catch (Exception ex) when (attempt < 5 && IsTransientDbError(ex))
            {
                await Task.Delay(200 * attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "histra.net: failed to save user data for '{Name}'", item.Name);
                return false;
            }
        }
    }

    private static double ToPercent(long positionTicks, long? runtimeTicks)
    {
        if (positionTicks <= 0 || runtimeTicks is null || runtimeTicks.Value <= 0)
        {
            return 0;
        }

        var percent = (double)positionTicks / runtimeTicks.Value * 100.0;
        return percent is > 0 and < 100 ? percent : 0;
    }

    private static bool IsTransientDbError(Exception ex) =>
        ex.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
        && ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase);
}
