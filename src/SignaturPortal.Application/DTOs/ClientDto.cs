namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Client data transfer object for presentation layer consumption.
/// Maps from Infrastructure.Data.Entities.Client.
/// </summary>
public record ClientDto(
    int ClientId,
    int SiteId,
    DateTime CreateDate,
    DateTime? ModifiedDate);
