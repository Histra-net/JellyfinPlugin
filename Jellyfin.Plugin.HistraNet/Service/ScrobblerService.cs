using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HistraNet.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Listens to Jellyfin playback events and scrobbles them to histra.net.
/// </summary>
public sealed class ScrobblerService : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly HistraClient _client;
    private readonly IUserTokenProvider _tokenProvider;
    private readonly ILogger<ScrobblerService> _logger;

    // Keyed by session id — last progress percent we actually reported.
    private readonly ConcurrentDictionary<string, int> _lastReported = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrobblerService"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="client">The histra.net client.</param>
    /// <param name="tokenProvider">Resolves the histra.net token per Jellyfin user.</param>
    /// <param name="logger">The logger.</param>
    public ScrobblerService(
        ISessionManager sessionManager,
        HistraClient client,
        IUserTokenProvider tokenProvider,
        ILogger<ScrobblerService> logger)
    {
        _sessionManager = sessionManager;
        _client = client;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("histra.net scrobbler started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        return Task.CompletedTask;
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
        => FireAndForget(() => ScrobbleAsync(e, "start"));

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        => FireAndForget(() => OnProgressAsync(e));

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Session?.Id))
        {
            _lastReported.TryRemove(e.Session.Id, out _);
        }

        FireAndForget(() => ScrobbleAsync(e, "stop"));
    }

    private async Task OnProgressAsync(PlaybackProgressEventArgs e)
    {
        var percent = ComputePercent(e);
        if (percent < 0)
        {
            return;
        }

        var sessionId = e.Session?.Id;
        if (!string.IsNullOrEmpty(sessionId))
        {
            var interval = Math.Max(1, Plugin.Instance?.Configuration.ProgressReportIntervalPercent ?? 10);
            var last = _lastReported.TryGetValue(sessionId, out var prev) ? prev : -interval;

            // Always report a pause immediately; otherwise throttle by interval.
            if (!e.IsPaused && Math.Abs(percent - last) < interval)
            {
                return;
            }

            _lastReported[sessionId] = percent;
        }

        await ScrobbleAsync(e, e.IsPaused ? "pause" : "start").ConfigureAwait(false);
    }

    private async Task ScrobbleAsync(PlaybackProgressEventArgs e, string action)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        var userId = e.Session?.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return;
        }

        var token = await _tokenProvider.GetTokenAsync(userId, CancellationToken.None).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            // No token configured for this user → not scrobbled.
            return;
        }

        var request = BuildRequest(e, action, config);
        if (request is null)
        {
            return;
        }

        var response = await _client.ScrobbleAsync(token, request, CancellationToken.None).ConfigureAwait(false);
        if (response is not null)
        {
            _logger.LogDebug(
                "histra.net scrobble '{Action}' @ {Progress}% (completed={Completed})",
                request.Action,
                (int)request.Progress,
                response.Completed);
        }
    }

    private ScrobbleRequest? BuildRequest(PlaybackProgressEventArgs e, string action, Configuration.PluginConfiguration config)
    {
        var item = e.Item;
        var progress = ComputePercent(e);
        // On start/stop the position may be unknown; default to 0 / passthrough.
        var progressValue = progress < 0 ? 0 : progress;

        switch (item)
        {
            case Movie movie when config.ScrobbleMovies:
            {
                var reference = MediaRefBuilder.BuildRef(movie);
                if (reference is null)
                {
                    _logger.LogDebug("Movie '{Name}' has no supported provider id; skipping scrobble", movie.Name);
                    return null;
                }

                return new ScrobbleRequest
                {
                    Action = action,
                    Progress = progressValue,
                    SessionId = e.Session?.Id,
                    Movie = reference
                };
            }

            case Episode episode when config.ScrobbleEpisodes:
            {
                if (episode.ParentIndexNumber is not int season || episode.IndexNumber is not int number)
                {
                    _logger.LogDebug("Episode '{Name}' missing season/episode number; skipping scrobble", episode.Name);
                    return null;
                }

                var series = episode.Series;
                var reference = series is null ? null : MediaRefBuilder.BuildRef(series);
                if (reference is null)
                {
                    _logger.LogDebug("Series for episode '{Name}' has no supported provider id; skipping scrobble", episode.Name);
                    return null;
                }

                return new ScrobbleRequest
                {
                    Action = action,
                    Progress = progressValue,
                    SessionId = e.Session?.Id,
                    Show = reference,
                    Episode = new EpisodeRef { Season = season, Number = number }
                };
            }

            default:
                return null;
        }
    }

    private static int ComputePercent(PlaybackProgressEventArgs e)
    {
        var position = e.PlaybackPositionTicks;
        var runtime = e.Item?.RunTimeTicks;
        if (position is null || runtime is null || runtime.Value <= 0)
        {
            return -1;
        }

        var percent = (double)position.Value / runtime.Value * 100.0;
        return (int)Math.Clamp(percent, 0, 100);
    }

    private void FireAndForget(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while scrobbling to histra.net");
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lastReported.Clear();
    }
}
