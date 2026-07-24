using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of GET /api/v1/tracking/stats — the user's watch dashboard data.
/// </summary>
public class TrackingStats
{
    /// <summary>Gets or sets the aggregate totals.</summary>
    [JsonPropertyName("summary")]
    public StatsSummary? Summary { get; set; }

    /// <summary>Gets or sets the per-month watch counts.</summary>
    [JsonPropertyName("monthly")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public MonthlyCount[] Monthly { get; set; } = Array.Empty<MonthlyCount>();

    /// <summary>Gets or sets the per-weekday watch counts.</summary>
    [JsonPropertyName("weekday")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public WeekdayCount[] Weekday { get; set; } = Array.Empty<WeekdayCount>();

    /// <summary>Gets or sets the most-watched shows.</summary>
    [JsonPropertyName("top_shows")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public TopShow[] TopShows { get; set; } = Array.Empty<TopShow>();
}
