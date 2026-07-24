using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// An episode entry in a batch sync request: the show reference plus the
/// season and episode number.
/// </summary>
public class SyncEpisode
{
    /// <summary>Gets or sets the show reference (external ids of the series).</summary>
    [JsonPropertyName("show")]
    public ExternalRef? Show { get; set; }

    /// <summary>Gets or sets the season number.</summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>Gets or sets the episode number within the season.</summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }
}
