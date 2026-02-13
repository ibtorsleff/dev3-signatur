using SignaturPortal.Application.DTOs;
using SignaturPortal.Infrastructure.Data.Entities;

namespace SignaturPortal.Infrastructure.Mappings;

/// <summary>
/// Manual mapping extensions for Client entity to DTO.
/// No AutoMapper -- explicit mapping for clarity and performance.
/// </summary>
public static class ClientMappings
{
    /// <summary>
    /// Maps a Client entity to ClientDto.
    /// </summary>
    public static ClientDto ToDto(this Client client)
    {
        return new ClientDto(
            client.ClientId,
            client.SiteId,
            client.CreateDate,
            client.ModifiedDate);
    }

    /// <summary>
    /// Projects an IQueryable of Client entities to DTOs for efficient querying.
    /// Use this for database queries to avoid loading full entities.
    /// </summary>
    public static IQueryable<ClientDto> ProjectToDto(this IQueryable<Client> query)
    {
        return query.Select(c => new ClientDto(
            c.ClientId,
            c.SiteId,
            c.CreateDate,
            c.ModifiedDate));
    }
}
