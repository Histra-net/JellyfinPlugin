using System;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HistraNet.Configuration;

/// <summary>
/// Plugin configuration for the histra.net tracking plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        BaseUrl = "https://histra.net";
        UserTokens = Array.Empty<UserToken>();

        // Token source: "config" = per-user map below; "manager" = look up per
        // Jellyfin user from the company manager over HTTP.
        TokenSource = "config";
        ManagerUrl = string.Empty;
        ManagerAppToken = string.Empty;

        ScrobbleMovies = true;
        ScrobbleEpisodes = true;
        ProgressReportIntervalPercent = 10;

        // Import (histra.net → Jellyfin), applied by the scheduled sync task.
        SkipUnwatchedImport = true;
        SkipWatchedImport = false;
        SkipPlaybackProgressImport = false;

        // Export (Jellyfin → histra.net).
        ExportWatchedOnScheduledTask = true;
        ExportUnwatchedOnScheduledTask = true;
        ExportWatchedOnChange = true;
        ExportUnwatchedOnChange = true;

        EnableDebugLogging = false;
    }

    /// <summary>
    /// Gets or sets the base URL of the histra.net API (no trailing slash).
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the per-user histra.net API tokens.
    /// Each Jellyfin user scrobbles under their own token; users without a
    /// token are not scrobbled.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Serialized by the Jellyfin XML plugin configuration serializer, which requires a settable array.")]
    public UserToken[] UserTokens { get; set; }

    /// <summary>
    /// Gets or sets the token source: "config" (per-user map) or "manager"
    /// (looked up per Jellyfin user from the company manager over HTTP).
    /// </summary>
    public string TokenSource { get; set; }

    /// <summary>
    /// Gets or sets the manager endpoint URL that returns a histra.net token for
    /// a Jellyfin user (used when <see cref="TokenSource"/> is "manager").
    /// </summary>
    public string ManagerUrl { get; set; }

    /// <summary>
    /// Gets or sets the service token the plugin sends to the manager to
    /// authenticate its token lookups.
    /// </summary>
    public string ManagerAppToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether movie playback is scrobbled.
    /// </summary>
    public bool ScrobbleMovies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether episode playback is scrobbled.
    /// </summary>
    public bool ScrobbleEpisodes { get; set; }

    /// <summary>
    /// Gets or sets the minimum change in playback percent between two progress
    /// reports sent to histra.net. Prevents flooding the API on frequent progress events.
    /// </summary>
    public int ProgressReportIntervalPercent { get; set; }

    // ---------- Import: histra.net → Jellyfin (scheduled task) ----------

    /// <summary>
    /// Gets or sets a value indicating whether unwatched status is NOT imported.
    /// When false, items not watched on histra.net are set unwatched locally.
    /// </summary>
    public bool SkipUnwatchedImport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether watched status is NOT imported.
    /// When false, items watched on histra.net are marked watched locally.
    /// </summary>
    public bool SkipWatchedImport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether playback progress is NOT imported.
    /// </summary>
    public bool SkipPlaybackProgressImport { get; set; }

    // ---------- Export: Jellyfin → histra.net ----------

    /// <summary>
    /// Gets or sets a value indicating whether the scheduled task marks items
    /// watched on histra.net when they are watched locally.
    /// </summary>
    public bool ExportWatchedOnScheduledTask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the scheduled task marks items
    /// unwatched on histra.net when they are unwatched locally.
    /// </summary>
    public bool ExportUnwatchedOnScheduledTask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a local watched change is pushed
    /// to histra.net immediately during normal use.
    /// </summary>
    public bool ExportWatchedOnChange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a local unwatched change is pushed
    /// to histra.net immediately during normal use.
    /// </summary>
    public bool ExportUnwatchedOnChange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether verbose debug logging is enabled.
    /// </summary>
    public bool EnableDebugLogging { get; set; }
}
