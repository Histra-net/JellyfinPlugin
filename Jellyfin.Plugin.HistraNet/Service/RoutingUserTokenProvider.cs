using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Delegates token resolution to the provider selected by the current
/// configuration's <c>TokenSource</c> ("config" or "manager"), evaluated per
/// call so changing the source in the UI takes effect without a restart.
/// </summary>
public class RoutingUserTokenProvider : IUserTokenProvider
{
    private readonly ConfigUserTokenProvider _config;
    private readonly ManagerUserTokenProvider _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingUserTokenProvider"/> class.
    /// </summary>
    /// <param name="config">The config-backed provider.</param>
    /// <param name="manager">The manager-backed provider.</param>
    public RoutingUserTokenProvider(ConfigUserTokenProvider config, ManagerUserTokenProvider manager)
    {
        _config = config;
        _manager = manager;
    }

    /// <inheritdoc />
    public Task<string?> GetTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var source = Plugin.Instance?.Configuration.TokenSource;
        var provider = string.Equals(source, "manager", StringComparison.OrdinalIgnoreCase)
            ? (IUserTokenProvider)_manager
            : _config;

        return provider.GetTokenAsync(userId, cancellationToken);
    }
}
