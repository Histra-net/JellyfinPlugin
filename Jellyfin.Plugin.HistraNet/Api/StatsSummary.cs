using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Aggregate watch totals for a user.
/// </summary>
public class StatsSummary
{
    /// <summary>Gets or sets the number of movies watched.</summary>
    [JsonPropertyName("movies_watched")]
    public int MoviesWatched { get; set; }

    /// <summary>Gets or sets the number of episodes watched.</summary>
    [JsonPropertyName("episodes_watched")]
    public int EpisodesWatched { get; set; }

    /// <summary>Gets or sets the number of distinct shows watched.</summary>
    [JsonPropertyName("shows_watched")]
    public int ShowsWatched { get; set; }

    /// <summary>Gets or sets the total watch time in minutes.</summary>
    [JsonPropertyName("total_minutes")]
    public long TotalMinutes { get; set; }

    /// <summary>Gets or sets the date of the first watch (may be a placeholder).</summary>
    [JsonPropertyName("first_watched")]
    public string? FirstWatched { get; set; }
}
