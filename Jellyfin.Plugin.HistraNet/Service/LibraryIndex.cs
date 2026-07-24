using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// In-memory index of the library, built once, so import can resolve titles by
/// external id without one database query per history entry. Matching a title
/// becomes an O(1) dictionary lookup instead of a SQL round-trip.
/// </summary>
public sealed class LibraryIndex
{
    // provider-id key ("tmdb:550") -> movie
    private readonly Dictionary<string, BaseItem> _moviesByProvider = new(StringComparer.OrdinalIgnoreCase);

    // episode key ("tmdb:1396|1|5") -> episode, keyed by the SERIES provider id + SxE
    private readonly Dictionary<string, Episode> _episodesBySeriesAndNumber = new(StringComparer.OrdinalIgnoreCase);

    private LibraryIndex()
    {
    }

    /// <summary>
    /// Builds the index from the whole library in a single query pass.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <returns>The populated index.</returns>
    public static LibraryIndex Build(ILibraryManager libraryManager)
    {
        var index = new LibraryIndex();

        var movies = libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            Recursive = true
        });
        foreach (var movie in movies)
        {
            foreach (var key in ProviderKeys(movie))
            {
                index._moviesByProvider.TryAdd(key, movie);
            }
        }

        var episodes = libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true
        });
        foreach (var item in episodes)
        {
            if (item is not Episode episode
                || episode.ParentIndexNumber is not int season
                || episode.IndexNumber is not int number)
            {
                continue;
            }

            var series = episode.Series;
            if (series is null)
            {
                continue;
            }

            foreach (var seriesKey in ProviderKeys(series))
            {
                index._episodesBySeriesAndNumber.TryAdd(EpisodeKey(seriesKey, season, number), episode);
            }
        }

        return index;
    }

    /// <summary>
    /// Resolves a movie by its external ids, or null if not in the library.
    /// </summary>
    /// <param name="externalIds">The external ids (tmdb/imdb/tvdb).</param>
    /// <returns>The movie, or null.</returns>
    public BaseItem? FindMovie(IReadOnlyDictionary<string, string>? externalIds)
    {
        if (externalIds is null)
        {
            return null;
        }

        foreach (var key in ProviderKeys(externalIds))
        {
            if (_moviesByProvider.TryGetValue(key, out var movie))
            {
                return movie;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves an episode by the series external ids + season/episode number.
    /// </summary>
    /// <param name="externalIds">The series external ids.</param>
    /// <param name="season">The season number.</param>
    /// <param name="number">The episode number.</param>
    /// <returns>The episode, or null.</returns>
    public Episode? FindEpisode(IReadOnlyDictionary<string, string>? externalIds, int? season, int? number)
    {
        if (externalIds is null || season is not int s || number is not int n)
        {
            return null;
        }

        foreach (var key in ProviderKeys(externalIds))
        {
            if (_episodesBySeriesAndNumber.TryGetValue(EpisodeKey(key, s, n), out var episode))
            {
                return episode;
            }
        }

        return null;
    }

    private static string EpisodeKey(string seriesProviderKey, int season, int number) =>
        string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", seriesProviderKey, season, number);

    // Keys from a Jellyfin item's provider ids.
    private static IEnumerable<string> ProviderKeys(BaseItem item)
    {
        foreach (var provider in new[] { MetadataProvider.Tmdb, MetadataProvider.Imdb, MetadataProvider.Tvdb })
        {
            if (item.TryGetProviderId(provider.ToString(), out var id) && !string.IsNullOrEmpty(id))
            {
                yield return provider.ToString().ToLowerInvariant() + ":" + id;
            }
        }
    }

    // Keys from a histra external_ids map (lowercase provider names).
    private static IEnumerable<string> ProviderKeys(IReadOnlyDictionary<string, string> externalIds)
    {
        foreach (var name in new[] { "tmdb", "imdb", "tvdb" })
        {
            if (externalIds.TryGetValue(name, out var id) && !string.IsNullOrEmpty(id))
            {
                yield return name + ":" + id;
            }
        }
    }
}
