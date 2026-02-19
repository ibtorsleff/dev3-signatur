namespace SignaturPortal.Application.DTOs;

/// <summary>
/// A single row in the "Activity Members" Excel export sheet.
/// One row per member per activity (includes Responsible, AlternativeResponsible, and committee members).
/// Mirrors the legacy ERActivityAndMembers.Member model.
/// </summary>
public record ActivityMemberExportRow
{
    public int ActivityId { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    /// <summary>Comma-separated ASP.NET role names filtered to RecruitmentPortal roles for the client.</summary>
    public string InRoles { get; init; } = "";
    /// <summary>Legacy always outputs false — Responsible users get IsMember=true but IsResponsible is never set.</summary>
    public bool IsResponsible { get; init; }
    /// <summary>True when this user appears in ERActivityAlternativeResponsible for this activity.</summary>
    public bool IsResponsibleAlternative { get; init; }
    /// <summary>True for Responsible users and committee members (ERActivityMember); false for pure AlternativeResponsible-only entries.</summary>
    public bool IsMember { get; init; }
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; }
}
