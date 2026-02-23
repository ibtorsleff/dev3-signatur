namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Write command mapped from ActivityFormModel.
/// Passed to ErActivityService.CreateActivityAsync / UpdateActivityAsync.
/// </summary>
public record ActivitySaveCommand
{
    // -------------------------------------------------------------------------
    // Master data
    // -------------------------------------------------------------------------

    public int StatusId { get; init; }
    public int ClientId { get; init; }
    public string Headline { get; init; } = "";
    public string? JobTitle { get; init; }
    public int? JobnetOccupationId { get; init; }
    public int? ClientSectionGroupId { get; init; }
    public int? ClientSectionId { get; init; }
    public bool Reposting { get; init; }
    public int? LeadershipPositionId { get; init; }
    public int? BlindRecruitmentId { get; init; }
    public int? RecruitmentTypeId { get; init; }
    public bool LockCandidateEvaluation { get; init; }
    public bool ViewCommitteeEvaluations { get; init; }
    public bool SendEmailOnNewCandidate { get; init; }
    public bool SendDailyStatusMail { get; init; }
    public bool ContinuousPosting { get; init; }
    public DateTime? ApplicationDeadline { get; init; }

    /// <summary>0 = None, 1 = Date, 2 = FreeText</summary>
    public int HireDateType { get; init; }

    public DateTime? HireDate { get; init; }
    public string? HireDateFreeText { get; init; }

    // -------------------------------------------------------------------------
    // Interview calendar
    // -------------------------------------------------------------------------

    public int? InterviewRounds { get; init; }
    public int? CalendarTypeId { get; init; }
    public bool ClosedCalendarEnabled { get; init; }
    public bool OpenCalendarEnabled { get; init; }
    public int? InterviewDurationId { get; init; }
    public bool SendSmsInterviewReminders { get; init; }
    public bool ExtendedEvaluationEnabled { get; init; }
    public List<int> SelectedLanguageIds { get; init; } = new();

    // -------------------------------------------------------------------------
    // Templates
    // -------------------------------------------------------------------------

    public int? TemplateGroupId { get; init; }
    public int? ApplicationTemplateId { get; init; }
    public int? EmailTemplateReceivedId { get; init; }
    public int? EmailTemplateInterviewId { get; init; }
    public int? EmailTemplateInterview2PlusId { get; init; }
    public int? EmailTemplateRejectedId { get; init; }
    public int? EmailTemplateRejectedAfterInterviewId { get; init; }
    public int? EmailTemplateNotifyCommitteeId { get; init; }
    public int? SmsTemplateInterviewId { get; init; }

    // -------------------------------------------------------------------------
    // Sidebar
    // -------------------------------------------------------------------------

    public Guid? RecruitmentResponsibleUserId { get; init; }
    public List<Guid> AlternativeResponsibleUserIds { get; init; } = new();

    // -------------------------------------------------------------------------
    // Audit
    // -------------------------------------------------------------------------

    /// <summary>UserId of the user performing the save operation.</summary>
    public Guid SavedByUserId { get; init; }

    public int SiteId { get; init; }
}
