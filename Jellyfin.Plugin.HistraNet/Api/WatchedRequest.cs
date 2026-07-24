using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Body for POST/DELETE /api/v1/watched. Set either <see cref="Movie"/>, or
/// <see cref="Show"/> together with <see cref="Episode"/>.
/// </summary>
public class WatchedRequest
{
    /// <summary>Gets or sets the movie reference (for a film).</summary>
    [JsonPropertyName("movie")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExternalRef? Movie { get; set; }

    /// <summary>Gets or sets the show reference (for an episode).</summary>
    [JsonPropertyName("show")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExternalRef? Show { get; set; }

    /// <summary>Gets or sets the episode reference (required together with show).</summary>
    [JsonPropertyName("episode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EpisodeRef? Episode { get; set; }
}
