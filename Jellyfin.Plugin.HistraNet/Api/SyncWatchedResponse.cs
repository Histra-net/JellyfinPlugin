using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of the batch sync endpoints.
/// </summary>
public class SyncWatchedResponse
{
    /// <summary>Gets or sets the counts of items successfully added/changed.</summary>
    [JsonPropertyName("added")]
    public SyncCounts? Added { get; set; }
}
