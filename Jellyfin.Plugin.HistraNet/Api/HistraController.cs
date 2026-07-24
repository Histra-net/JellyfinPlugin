using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HistraNet.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Plugin API endpoints, served by the Jellyfin server under the same origin
/// as the dashboard. Used by the configuration page so browser requests avoid
/// cross-origin (CORS) restrictions against histra.net.
/// </summary>
[ApiController]
[Authorize(Policy = "FirstTimeSetupOrDefault")]
[Route("HistraNet")]
[Produces(MediaTypeNames.Application.Json)]
public class HistraController : ControllerBase
{
    // Claim carrying the Jellyfin user id (Jellyfin.Api.Constants.InternalClaimTypes.UserId).
    private const string UserIdClaim = "Jellyfin-UserId";

    private readonly HistraClient _client;
    private readonly IUserTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistraController"/> class.
    /// </summary>
    /// <param name="client">The histra.net client.</param>
    /// <param name="tokenProvider">Resolves the histra.net token for the current user.</param>
    public HistraController(HistraClient client, IUserTokenProvider tokenProvider)
    {
        _client = client;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Verifies a histra.net token, server-side, by calling /api/v1/auth/me.
    /// </summary>
    /// <param name="token">The histra.net API token to verify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authenticated histra.net user, or 400 if the token is invalid.</returns>
    [HttpGet("Test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthMe>> Test(
        [FromQuery, Required] string token,
        CancellationToken cancellationToken)
    {
        var me = await _client.GetMeAsync(token, cancellationToken).ConfigureAwait(false);
        if (me is null)
        {
            return BadRequest(new { message = "Connection failed. Check the server URL and token." });
        }

        return Ok(me);
    }

    /// <summary>
    /// Returns the current Jellyfin user's histra.net tracking statistics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stats, 404 if the user has no configured token, or 502 on upstream failure.</returns>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TrackingStats>> Stats(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return NotFound(new { message = "No Jellyfin user in request." });
        }

        var token = await _tokenProvider.GetTokenAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotFound(new { message = "No histra.net token configured for this user." });
        }

        var stats = await _client.GetStatsAsync(token, cancellationToken).ConfigureAwait(false);
        if (stats is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "histra.net stats request failed." });
        }

        return Ok(stats);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(UserIdClaim)?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
