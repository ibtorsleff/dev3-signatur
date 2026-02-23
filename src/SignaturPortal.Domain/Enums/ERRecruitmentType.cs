namespace SignaturPortal.Domain.Enums;

/// <summary>
/// Mirrors legacy RecruitmentTypeEn enum (ActivityCreateEdit.aspx.cs).
/// Values are stored as RecruitmentTypeId on ERActivity.
/// LeadershipPositionId/BlindRecruitmentId binary flags are derived from this value on save.
/// </summary>
public enum ERRecruitmentType
{
    Normal = 1,
    LeadershipPosition = 2,
    BlindRecruitment = 3
}
