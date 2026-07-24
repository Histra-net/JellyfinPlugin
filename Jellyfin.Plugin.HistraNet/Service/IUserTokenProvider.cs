using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.HistraNet.Service;

/// <summary>
/// Resolves the histra.net API token for a given Jellyfin user.
/// </summary>
/// <remarks>
/// The current implementation reads a per-user token map from the plugin
/// configuration. A future implementation will look the token up in the
/// company manager database — swapping the DI registration is all that is
/// required; no consumer changes.
/// </remarks>
public interface IUserTokenProvider
{
    /// <summary>
    /// Gets the histra.net token for a Jellyfin user, or null if none is known.
    /// </summary>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token, or null when the user has no configured token.</returns>
    Task<string?> GetTokenAsync(Guid userId, CancellationToken cancellationToken);
}
