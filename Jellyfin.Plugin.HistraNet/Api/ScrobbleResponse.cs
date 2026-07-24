using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response body of a scrobble call.
/// </summary>
public class ScrobbleResponse
{
    /// <summary>Gets or sets the echoed action.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>Gets or sets the echoed progress.</summary>
    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    /// <summary>Gets or sets a value indicating whether playback was completed (marked watched).</summary>
    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    /// <summary>Gets or sets the resulting watched state.</summary>
    [JsonPropertyName("state")]
    public WatchState? State { get; set; }
}
