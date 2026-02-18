namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Lightweight DTO for client dropdown items.
/// ClientName is extracted from the Client.ObjectData XML column via SQL XPath.
/// </summary>
public record ClientDropdownDto(int ClientId, string ClientName);
