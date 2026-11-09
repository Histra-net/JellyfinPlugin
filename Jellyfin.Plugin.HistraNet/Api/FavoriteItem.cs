using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// One title in the user's histra.net favorites (GET /api/v1/favorites?extended=full).
/// </summary>
public class FavoriteItem
{
    /// <summary>Gets or sets the media type: "movie" or "show".</summary>
    [JsonPropertyName("media_type")]
    public string? MediaType { get; set; }

    /// <summary>Gets or sets the histra media id.</summary>
    [JsonPropertyName("media_id")]
    public long MediaId { get; set; }

    /// <summary>Gets or sets the title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the poster path.</summary>
    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    /// <summary>Gets or sets the release year.</summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>Gets or sets the external ids (imdb/tmdb/tvdb/trakt) of the movie or show.</summary>
    [JsonPropertyName("external_ids")]
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Deserialized API response DTO.")]
    public Dictionary<string, string>? ExternalIds { get; set; }

    /// <summary>Gets a value indicating whether this item is a show (series).</summary>
    [JsonIgnore]
    public bool IsShow => string.Equals(MediaType, "show", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether this item is a movie.</summary>
    [JsonIgnore]
    public bool IsMovie => string.Equals(MediaType, "movie", System.StringComparison.OrdinalIgnoreCase);
}
