using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// A continue-watching (in-progress) item. For a movie, external_ids are the
/// movie's ids; for an episode, they are the SERIES' ids plus
/// season_number / episode_number.
/// </summary>
public class ContinueItem
{
    /// <summary>Gets or sets the histra movie id (movies only).</summary>
    [JsonPropertyName("movie_id")]
    public long? MovieId { get; set; }

    /// <summary>Gets or sets the histra episode id (episodes only).</summary>
    [JsonPropertyName("episode_id")]
    public long? EpisodeId { get; set; }

    /// <summary>Gets or sets the season number (episodes only).</summary>
    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    /// <summary>Gets or sets the episode number (episodes only).</summary>
    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

    /// <summary>Gets or sets the playback progress in percent (0-100).</summary>
    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the external ids (imdb/tmdb/tvdb/trakt). For an episode these
    /// identify the series, not the episode.
    /// </summary>
    [JsonPropertyName("external_ids")]
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Deserialized API response DTO.")]
    public Dictionary<string, string>? ExternalIds { get; set; }

    /// <summary>Gets a value indicating whether this item is an episode.</summary>
    [JsonIgnore]
    public bool IsEpisode => EpisodeId.HasValue;
}
