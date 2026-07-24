using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Unified scrobble request accepting any external id.
/// </summary>
public class ScrobbleRequest
{
    /// <summary>Gets or sets the playback phase: start, pause, stop or clear.</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "start";

    /// <summary>Gets or sets the playback progress in percent (0-100).</summary>
    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    /// <summary>Gets or sets an optional playback session id.</summary>
    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; set; }

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
