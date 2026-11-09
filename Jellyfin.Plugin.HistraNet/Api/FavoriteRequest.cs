using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Body for POST/DELETE /api/v1/favorites. Set exactly one of
/// <see cref="Movie"/> or <see cref="Show"/>; histra.net accepts only movies
/// and shows as favorites (never seasons or episodes).
/// </summary>
public class FavoriteRequest
{
    /// <summary>Gets or sets the movie reference.</summary>
    [JsonPropertyName("movie")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExternalRef? Movie { get; set; }

    /// <summary>Gets or sets the show (series) reference.</summary>
    [JsonPropertyName("show")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExternalRef? Show { get; set; }
}
