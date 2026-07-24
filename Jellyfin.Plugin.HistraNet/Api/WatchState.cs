using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Watched state as returned inside a scrobble response.
/// </summary>
public class WatchState
{
    /// <summary>Gets or sets the histra movie id.</summary>
    [JsonPropertyName("movie_id")]
    public long? MovieId { get; set; }

    /// <summary>Gets or sets a value indicating whether the title is marked watched.</summary>
    [JsonPropertyName("watched")]
    public bool Watched { get; set; }

    /// <summary>Gets or sets the number of plays.</summary>
    [JsonPropertyName("plays")]
    public int Plays { get; set; }
}
