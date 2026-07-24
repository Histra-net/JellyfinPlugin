using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// A show with the number of episodes watched, for the top-shows ranking.
/// </summary>
public class TopShow
{
    /// <summary>Gets or sets the histra show id.</summary>
    [JsonPropertyName("show_id")]
    public long ShowId { get; set; }

    /// <summary>Gets or sets the show title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the poster path.</summary>
    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    /// <summary>Gets or sets the number of episodes watched for this show.</summary>
    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }
}
