using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// External reference to a title by any provider id. Set exactly one.
/// </summary>
public class ExternalRef
{
    /// <summary>Gets or sets the TMDb id.</summary>
    [JsonPropertyName("tmdb")]
    public string? Tmdb { get; set; }

    /// <summary>Gets or sets the IMDb id.</summary>
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    /// <summary>Gets or sets the TVDB id.</summary>
    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }

    /// <summary>Gets or sets the Trakt id.</summary>
    [JsonPropertyName("trakt")]
    public string? Trakt { get; set; }

    /// <summary>Gets a value indicating whether any provider id is set.</summary>
    [JsonIgnore]
    public bool HasAny => !string.IsNullOrEmpty(Tmdb)
        || !string.IsNullOrEmpty(Imdb)
        || !string.IsNullOrEmpty(Tvdb)
        || !string.IsNullOrEmpty(Trakt);
}
