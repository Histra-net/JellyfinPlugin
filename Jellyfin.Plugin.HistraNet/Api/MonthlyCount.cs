using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Watch count for a single month.
/// </summary>
public class MonthlyCount
{
    /// <summary>Gets or sets the month, formatted "yyyy-MM".</summary>
    [JsonPropertyName("month")]
    public string? Month { get; set; }

    /// <summary>Gets or sets the number of items watched that month.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
