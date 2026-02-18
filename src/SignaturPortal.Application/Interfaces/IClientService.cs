using SignaturPortal.Application.DTOs;

namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Service for client-related operations.
/// Provides client lookup data for dropdown selectors.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Gets a list of enabled clients for a given site, with names extracted from ObjectData XML.
    /// Used to populate the client selector dropdown for non-client (staff/admin) users.
    /// </summary>
    Task<List<ClientDropdownDto>> GetClientsForSiteAsync(int siteId, CancellationToken ct = default);
}
