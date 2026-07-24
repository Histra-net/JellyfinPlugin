using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Remembers what watched/progress state was already exported to histra.net per
/// Jellyfin user, so the scheduled sync only sends what actually changed.
/// Persisted as JSON in the plugin data folder; survives restarts.
/// </summary>
/// <remarks>
/// Fail-safe: if the store is missing or unreadable, every item looks "changed"
/// and a full export runs (same as before this cache existed) — never fewer
/// writes than correctness requires.
/// </remarks>
public class ExportStateStore
{
    private readonly ILogger<ExportStateStore> _logger;
    private readonly object _saveLock = new();

    // userId(N) -> ( itemId(N) -> state signature )
    private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportStateStore"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ExportStateStore(ILogger<ExportStateStore> logger)
    {
        _logger = logger;
        Load();
    }

    private static string? FilePath
    {
        get
        {
            var dir = Plugin.Instance?.DataFolderPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "export-state.json");
        }
    }

    /// <summary>
    /// Builds the state signature for an item (watched flag + coarse progress bucket).
    /// </summary>
    /// <param name="watched">Whether the item is played.</param>
    /// <param name="progressPercent">Playback progress in percent (0 when not in progress).</param>
    /// <returns>A short signature string.</returns>
    public static string Signature(bool watched, double progressPercent)
    {
        if (watched)
        {
            return "w";
        }

        // Bucket progress into 5% steps so tiny position changes don't re-export.
        var bucket = (int)Math.Round(progressPercent / 5.0) * 5;
        return bucket > 0 ? "p" + bucket.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-";
    }

    /// <summary>
    /// Returns true when the item's current signature differs from what was last
    /// exported for this user (i.e. it needs to be sent).
    /// </summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="itemId">The library item id.</param>
    /// <param name="signature">The current state signature.</param>
    /// <returns>True when the item changed and should be exported.</returns>
    public bool HasChanged(Guid userId, Guid itemId, string signature)
    {
        var userMap = _state.GetOrAdd(userId.ToString("N"), _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        return !userMap.TryGetValue(itemId.ToString("N"), out var prev) || prev != signature;
    }

    /// <summary>
    /// Records the signature that was successfully exported for an item.
    /// </summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="itemId">The library item id.</param>
    /// <param name="signature">The exported state signature.</param>
    public void Record(Guid userId, Guid itemId, string signature)
    {
        var userMap = _state.GetOrAdd(userId.ToString("N"), _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        userMap[itemId.ToString("N")] = signature;
    }

    /// <summary>
    /// Persists the store to disk.
    /// </summary>
    public void Save()
    {
        var path = FilePath;
        if (path is null)
        {
            return;
        }

        try
        {
            lock (_saveLock)
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(_state);
                File.WriteAllText(path, json);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "histra.net: could not save export state");
        }
    }

    private void Load()
    {
        var path = FilePath;
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>(json);
            if (loaded is not null)
            {
                _state = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fail-safe: start empty → full export runs.
            _logger.LogWarning(ex, "histra.net: could not load export state; a full export will run");
        }
    }
}
