using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Resolves user tokens from the per-user token map in the plugin configuration.
/// </summary>
public class ConfigUserTokenProvider : IUserTokenProvider
{
    /// <inheritdoc />
    public Task<string?> GetTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return Task.FromResult<string?>(null);
        }

        // Jellyfin user ids are compared in "N" (dashless) format, case-insensitive.
        var target = userId.ToString("N");
        foreach (var entry in config.UserTokens)
        {
            if (string.IsNullOrWhiteSpace(entry.Token) || string.IsNullOrWhiteSpace(entry.UserId))
            {
                continue;
            }

            if (Guid.TryParse(entry.UserId, out var parsed)
                && string.Equals(parsed.ToString("N"), target, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>(entry.Token);
            }
        }

        return Task.FromResult<string?>(null);
    }
}
