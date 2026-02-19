namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Lightweight DTO for client dropdown items.
/// ClientName is extracted from the Client.ObjectData XML column via SQL XPath.
/// IsEnabled reflects Client.ObjectData/Enabled; disabled clients are shown as [ClientName] in dropdowns.
/// </summary>
public record ClientDropdownDto(int ClientId, string ClientName, bool IsEnabled);
