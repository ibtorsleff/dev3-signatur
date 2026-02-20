namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Command to update the current user's editable profile fields.
/// WorkArea and Title are null when the user is not internal (fields not shown).
/// All string values are trimmed by the service before persisting.
/// </summary>
public class UpdateUserProfileCommand
{
    public Guid UserId { get; init; }
    public int SiteId { get; init; }

    /// <summary>Null when user is not internal (field not displayed).</summary>
    public string? WorkArea { get; init; }

    /// <summary>Null when user is not internal (field not displayed).</summary>
    public string? Title { get; init; }

    public string? OfficePhone { get; init; }
    public string? CellPhone { get; init; }

    /// <summary>Null when email editing is disabled — service skips email update.</summary>
    public string? Email { get; init; }
}
