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

    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserTokenProvider _tokenProvider;
    private readonly HistraClient _client;
    private readonly ILogger<HistraSyncTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistraSyncTask"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="tokenProvider">The token provider.</param>
    /// <param name="client">The histra.net client.</param>
    /// <param name="logger">The logger.</param>
    public HistraSyncTask(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserTokenProvider tokenProvider,
        HistraClient client,
        ILogger<HistraSyncTask> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _tokenProvider = tokenProvider;
        _client = client;
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

        var step = 100.0 / users.Count;
        var done = 0;

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var token = await _tokenProvider.GetTokenAsync(user.Id, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                await ImportForUserAsync(user, token, config, cancellationToken).ConfigureAwait(false);
                await ImportProgressForUserAsync(user, token, config, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        // If both watched and unwatched imports are skipped, there is nothing to do here.
        if (config.SkipWatchedImport)
        {
            _logger.LogInformation("histra.net: watched import skipped for user {User}", user.Username);
            return;
        }

        var marked = 0;
        var offset = 0;

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
                    ? FindEpisode(entry.ExternalIds, entry.SeasonNumber, entry.EpisodeNumber)
                    : FindMovie(entry.ExternalIds);
                if (item is null)
                {
                    continue;
                }

                if (await MarkPlayedAsync(user, item, cancellationToken).ConfigureAwait(false))
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
    }

    private async Task ImportProgressForUserAsync(
        User user,
        string token,
        Configuration.PluginConfiguration config,
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
                var item = FindMovie(it.ExternalIds);
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
                var item = FindEpisode(it.ExternalIds, it.SeasonNumber, it.EpisodeNumber);
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

        // Enumerate movies and episodes for the user and keep those the user has
        // played. IsPlayed is per-user data, so we read it via the user data
        // manager (the InternalItemsQuery.IsPlayed filter proved unreliable here).
        // Note: do NOT pass the user into the query ctor — that applies library
        // visibility filtering that can drop most items. Query the library
        // globally and resolve per-user played state via the user data manager.
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query);
        var exported = 0;
        var progressExported = 0;
        var played = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var data = _userDataManager.GetUserData(user, item);
            if (data is null)
            {
                continue;
            }

            if (data.Played)
            {
                played++;
                var request = MediaRefBuilder.BuildWatched(item);
                if (request is not null
                    && await _client.SetWatchedAsync(token, true, request, cancellationToken).ConfigureAwait(false))
                {
                    exported++;
                }

                continue;
            }

            // In-progress item → push its resume position as a paused scrobble.
            var percent = ToPercent(data.PlaybackPositionTicks, item.RunTimeTicks);
            if (percent <= 0)
            {
                continue;
            }

            var scrobble = MediaRefBuilder.BuildScrobble(item, "pause", percent);
            if (scrobble is not null
                && await _client.ScrobbleAsync(token, scrobble, cancellationToken).ConfigureAwait(false) is not null)
            {
                progressExported++;
            }
        }

        if (config.EnableDebugLogging)
        {
            _logger.LogInformation(
                "histra.net: export scanned={Scanned}, watched={Watched}, progress={Progress}",
                items.Count,
                exported,
                progressExported);
        }

        // Note: bulk "unwatched" export is intentionally NOT performed. Mass
        // DELETE /watched over the whole library would wipe histra state set by
        // other clients. Unwatched changes are exported in real time on toggle.
        if (config.ExportUnwatchedOnScheduledTask && config.EnableDebugLogging)
        {
            _logger.LogInformation("histra.net: bulk unwatched export skipped (handled on toggle, not in bulk)");
        }

        if (config.EnableDebugLogging || exported > 0)
        {
            _logger.LogInformation("histra.net: exported {Count} watched item(s) for user {User}", exported, user.Username);
        }
    }

    private BaseItem? FindMovie(Dictionary<string, string>? externalIds)
    {
        if (externalIds is null)
        {
            return null;
        }

        var providerMap = BuildProviderMap(externalIds);
        if (providerMap.Count == 0)
        {
            return null;
        }

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            HasAnyProviderId = providerMap,
            Limit = 1
        };

        var results = _libraryManager.GetItemList(query);
        return results.Count > 0 ? results[0] : null;
    }

    private Episode? FindEpisode(Dictionary<string, string>? externalIds, int? seasonNumber, int? episodeNumber)
    {
        if (externalIds is null || seasonNumber is not int season || episodeNumber is not int number)
        {
            return null;
        }

        var providerMap = BuildProviderMap(externalIds);
        if (providerMap.Count == 0)
        {
            return null;
        }

        // Find the series by its external id, then the episode by S/E number.
        var seriesQuery = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            HasAnyProviderId = providerMap,
            Limit = 1
        };

        var series = _libraryManager.GetItemList(seriesQuery).OfType<Series>().FirstOrDefault();
        if (series is null)
        {
            return null;
        }

        var episodeQuery = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            AncestorIds = new[] { series.Id },
            ParentIndexNumber = season,
            IndexNumber = number
        };

        return _libraryManager.GetItemList(episodeQuery).OfType<Episode>().FirstOrDefault();
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

        // SaveUserData writes to the shared SQLite database. A concurrent library
        // scan can briefly hold the write lock, so retry transient "database is
        // locked" errors instead of aborting the whole sync.
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
                _logger.LogWarning(ex, "histra.net: failed to mark '{Name}' played", item.Name);
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

    private static Dictionary<string, string> BuildProviderMap(Dictionary<string, string> externalIds)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // histra keys are lowercase (tmdb/imdb/tvdb); Jellyfin providers are Tmdb/Imdb/Tvdb.
        AddIfPresent(map, externalIds, "tmdb", MetadataProvider.Tmdb);
        AddIfPresent(map, externalIds, "imdb", MetadataProvider.Imdb);
        AddIfPresent(map, externalIds, "tvdb", MetadataProvider.Tvdb);

        return map;
    }

    private static void AddIfPresent(
        Dictionary<string, string> map,
        Dictionary<string, string> externalIds,
        string histraKey,
        MetadataProvider provider)
    {
        if (externalIds.TryGetValue(histraKey, out var value) && !string.IsNullOrEmpty(value))
        {
            map[provider.ToString()] = value;
        }
    }
}
