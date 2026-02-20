namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Lightweight DTO for a user row in the impersonation search dialog.
/// FullName, Title, Email are direct columns on [User]; ClientSection is extracted from ObjectData XML;
/// ClientName is extracted from the joined Client.ObjectData XML.
/// </summary>
public record ImpersonateUserDto(
    Guid UserId,
    string? FullName,
    string? Title,
    string? Email,
    string? ClientSection,
    string? ClientName);
