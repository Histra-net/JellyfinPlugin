using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Movie/episode counts in a batch sync response.
/// </summary>
public class SyncCounts
{
    /// <summary>Gets or sets the number of movies affected.</summary>
    [JsonPropertyName("movies")]
    public int Movies { get; set; }

    /// <summary>Gets or sets the number of episodes affected.</summary>
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
}
