namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Snapshot of the current user's [User] table record, loaded once per Blazor circuit.
/// </summary>
public record CurrentUserDto(
    Guid UserId,
    string? FullName,
    string? UserName,
    string? Email,
    bool IsInternal,
    bool Enabled,
    int SiteId,
    int? ClientId);
