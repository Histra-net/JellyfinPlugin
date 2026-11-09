using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of GET /api/v1/favorites/status and of POST/DELETE /api/v1/favorites.
/// </summary>
public class FavoriteStatus
{
    /// <summary>Gets or sets a value indicating whether the title is a favorite.</summary>
    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }
}
