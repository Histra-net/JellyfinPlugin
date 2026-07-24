namespace Jellyfin.Plugin.HistraNet.Configuration;

/// <summary>
/// Maps a Jellyfin user to a personal histra.net API token.
/// </summary>
public class UserToken
{
    /// <summary>
    /// Gets or sets the Jellyfin user id (GUID string, "N" format).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the personal histra.net API token ("hst_...").
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
