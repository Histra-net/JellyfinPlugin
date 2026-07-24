using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Season + episode number, sent together with a show reference.
/// </summary>
public class EpisodeRef
{
    /// <summary>Gets or sets the season number.</summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>Gets or sets the episode number within the season.</summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }
}
