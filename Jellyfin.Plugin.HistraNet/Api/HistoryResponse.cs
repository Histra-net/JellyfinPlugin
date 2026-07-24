using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of GET /api/v1/tracking/history.
/// </summary>
public class HistoryResponse
{
    /// <summary>Gets or sets the history entries (newest first).</summary>
    [JsonPropertyName("entries")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public HistoryEntry[] Entries { get; set; } = Array.Empty<HistoryEntry>();
}
