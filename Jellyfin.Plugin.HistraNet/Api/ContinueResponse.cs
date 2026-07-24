using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of the continue-watching endpoints.
/// </summary>
public class ContinueResponse
{
    /// <summary>Gets or sets the in-progress items.</summary>
    [JsonPropertyName("items")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public ContinueItem[] Items { get; set; } = Array.Empty<ContinueItem>();
}
