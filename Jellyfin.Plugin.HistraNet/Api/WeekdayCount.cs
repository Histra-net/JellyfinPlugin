using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Watch count for a single weekday. The weekday index (0-6) is returned raw;
/// its interpretation (which day is 0) is left to the presentation layer.
/// </summary>
public class WeekdayCount
{
    /// <summary>Gets or sets the weekday index (0-6).</summary>
    [JsonPropertyName("weekday")]
    public int Weekday { get; set; }

    /// <summary>Gets or sets the number of items watched on that weekday.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
