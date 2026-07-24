using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.HistraNet.Api;

/// <summary>
/// Batch body for POST /api/v1/sync/watched and /sync/unwatched — a set of
/// movies and episodes handled in one request.
/// </summary>
public class SyncWatchedRequest
{
    /// <summary>Gets or sets the movies to mark (each an external ref).</summary>
    [JsonPropertyName("movies")]
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Serialized request DTO.")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Serialized request DTO.")]
    public ExternalRef[] Movies { get; set; } = Array.Empty<ExternalRef>();

    /// <summary>Gets or sets the episodes to mark (show ref + season/number).</summary>
    [JsonPropertyName("episodes")]
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Serialized request DTO.")]
    [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Serialized request DTO.")]
    public SyncEpisode[] Episodes { get; set; } = Array.Empty<SyncEpisode>();
}
