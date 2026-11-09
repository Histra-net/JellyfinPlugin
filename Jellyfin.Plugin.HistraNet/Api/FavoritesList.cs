using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Metadata of the user's favorites list.
/// </summary>
public class FavoritesList
{
    /// <summary>Gets or sets the total number of titles in the list.</summary>
    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }
}
