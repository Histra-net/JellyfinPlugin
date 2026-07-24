using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Response of GET /api/v1/auth/me.
/// </summary>
public class AuthMe
{
    /// <summary>Gets or sets the histra user id.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Gets or sets the user email.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
