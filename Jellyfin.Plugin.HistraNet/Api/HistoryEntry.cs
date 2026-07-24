using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// A single watched entry in the unified history. For a movie, external_ids are
/// the movie's ids; for an episode, they are the SERIES' ids plus
/// season_number / episode_number.
/// </summary>
public class HistoryEntry
{
    /// <summary>Gets or sets the kind: "movie" or "episode".</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>Gets or sets the histra media id.</summary>
    [JsonPropertyName("media_id")]
    public long MediaId { get; set; }

    /// <summary>Gets or sets the title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the watched timestamp.</summary>
    [JsonPropertyName("watched_at")]
    public string? WatchedAt { get; set; }

    /// <summary>Gets or sets the season number (episodes only).</summary>
    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    /// <summary>Gets or sets the episode number (episodes only).</summary>
    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the external ids (imdb/tmdb/tvdb/trakt). For an episode these
    /// identify the series, not the episode.
    /// </summary>
    [JsonPropertyName("external_ids")]
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Deserialized API response DTO.")]
    public Dictionary<string, string>? ExternalIds { get; set; }

    /// <summary>Gets a value indicating whether this entry is an episode.</summary>
    [JsonIgnore]
    public bool IsEpisode => string.Equals(Kind, "episode", System.StringComparison.OrdinalIgnoreCase);
}
