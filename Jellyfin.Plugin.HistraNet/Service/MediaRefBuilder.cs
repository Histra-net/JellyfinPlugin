using Jellyfin.Plugin.HistraNet.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Builds histra.net external references from Jellyfin library items. Shared by
/// the scrobbler, the sync task and the realtime exporter so provider-id
/// extraction lives in one place.
/// </summary>
public static class MediaRefBuilder
{
    /// <summary>
    /// Builds a watched request for a library item, or null if it is not a
    /// movie/episode with a usable provider id (or missing season/episode).
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <returns>The watched request, or null when the item cannot be referenced.</returns>
    public static WatchedRequest? BuildWatched(BaseItem item)
    {
        switch (item)
        {
            case Movie movie:
            {
                var reference = BuildRef(movie);
                return reference is null ? null : new WatchedRequest { Movie = reference };
            }

            case Episode episode:
            {
                if (episode.ParentIndexNumber is not int season || episode.IndexNumber is not int number)
                {
                    return null;
                }

                var series = episode.Series;
                var reference = series is null ? null : BuildRef(series);
                return reference is null
                    ? null
                    : new WatchedRequest
                    {
                        Show = reference,
                        Episode = new EpisodeRef { Season = season, Number = number }
                    };
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Builds a scrobble request for a library item with the given action and
    /// progress, or null if it cannot be referenced.
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <param name="action">The scrobble action (start / pause / stop / clear).</param>
    /// <param name="progress">The playback progress in percent (0-100).</param>
    /// <returns>The scrobble request, or null.</returns>
    public static ScrobbleRequest? BuildScrobble(BaseItem item, string action, double progress)
    {
        // Reuse the movie/show+episode split from BuildWatched.
        var watched = BuildWatched(item);
        if (watched is null)
        {
            return null;
        }

        return new ScrobbleRequest
        {
            Action = action,
            Progress = progress,
            Movie = watched.Movie,
            Show = watched.Show,
            Episode = watched.Episode
        };
    }

    /// <summary>
    /// Builds an external reference (tmdb/imdb/tvdb) for an item, or null if none present.
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <returns>The external reference, or null.</returns>
    public static ExternalRef? BuildRef(BaseItem item)
    {
        var reference = new ExternalRef
        {
            Tmdb = GetProviderId(item, MetadataProvider.Tmdb),
            Imdb = GetProviderId(item, MetadataProvider.Imdb),
            Tvdb = GetProviderId(item, MetadataProvider.Tvdb)
        };

        return reference.HasAny ? reference : null;
    }

    private static string? GetProviderId(BaseItem item, MetadataProvider provider)
    {
        return item.TryGetProviderId(provider.ToString(), out var id) && !string.IsNullOrEmpty(id)
            ? id
            : null;
    }
}
