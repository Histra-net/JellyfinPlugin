using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of GET /api/v1/favorites. An empty favorites list is a 200 with no
/// items (the list does not exist on the server until the first add), never a 404.
/// </summary>
public class FavoritesResponse
{
    /// <summary>Gets or sets the list metadata (may be null for an empty list).</summary>
    [JsonPropertyName("list")]
    public FavoritesList? List { get; set; }

    /// <summary>Gets or sets the favorite titles on this page.</summary>
    [JsonPropertyName("items")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Deserialized API response DTO.")]
    public FavoriteItem[]? Items { get; set; } = Array.Empty<FavoriteItem>();
}
