using System.ComponentModel.DataAnnotations;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Mutable form model for EditForm two-way binding on the ActivityCreateEdit page.
/// Implements IValidatableObject for cross-field validation rules.
/// </summary>
public class ActivityFormModel : IValidatableObject
{
    // -------------------------------------------------------------------------
    // Master data
    // -------------------------------------------------------------------------

    [Required]
    public int? StatusId { get; set; }

    [Required]
    public int? ClientId { get; set; }

    [Required]
    [MaxLength(70)]
    public string Headline { get; set; } = "";

    [MaxLength(70)]
    public string? JobTitle { get; set; }

    [Required]
    public int? JobnetOccupationId { get; set; }

    public int? ClientSectionGroupId { get; set; }

    [Required]
    public int? ClientSectionId { get; set; }

    public bool Reposting { get; set; }

    // Conditionally required depending on client config flags — validated by UI visibility
    public int? LeadershipPositionId { get; set; }

    public int? BlindRecruitmentId { get; set; }

    public int? RecruitmentTypeId { get; set; }

    public bool LockCandidateEvaluation { get; set; }

    public bool ViewCommitteeEvaluations { get; set; }

    public bool SendEmailOnNewCandidate { get; set; }

    public bool SendDailyStatusMail { get; set; }

    public bool ContinuousPosting { get; set; }

    public DateTime? ApplicationDeadline { get; set; }

    /// <summary>0 = None, 1 = Date, 2 = FreeText</summary>
    public int HireDateType { get; set; }

    public DateTime? HireDate { get; set; }

    [MaxLength(100)]
    public string? HireDateFreeText { get; set; }

    // -------------------------------------------------------------------------
    // Interview calendar
    // -------------------------------------------------------------------------

    public int? InterviewRounds { get; set; }

    public int? CalendarTypeId { get; set; }

    public bool ClosedCalendarEnabled { get; set; }

    public bool OpenCalendarEnabled { get; set; }

    public int? InterviewDurationId { get; set; }

    public bool SendSmsInterviewReminders { get; set; }

    public bool ExtendedEvaluationEnabled { get; set; }

    public List<int> SelectedLanguageIds { get; set; } = new();

    // -------------------------------------------------------------------------
    // Templates
    // -------------------------------------------------------------------------

    public int? TemplateGroupId { get; set; }

    [Required]
    public int? ApplicationTemplateId { get; set; }

    [Required]
    public int? EmailTemplateReceivedId { get; set; }

    [Required]
    public int? EmailTemplateInterviewId { get; set; }

    public int? EmailTemplateInterview2PlusId { get; set; }

    [Required]
    public int? EmailTemplateRejectedId { get; set; }

    [Required]
    public int? EmailTemplateRejectedAfterInterviewId { get; set; }

    [Required]
    public int? EmailTemplateNotifyCommitteeId { get; set; }

    public int? SmsTemplateInterviewId { get; set; }

    // -------------------------------------------------------------------------
    // Sidebar — responsible / alternative responsible
    // -------------------------------------------------------------------------

    public Guid? RecruitmentResponsibleUserId { get; set; }

    public List<Guid> AlternativeResponsibleUserIds { get; set; } = new();

    // -------------------------------------------------------------------------
    // Cross-field validation
    // -------------------------------------------------------------------------

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var loc = validationContext.GetService(typeof(ILocalizationService)) as ILocalizationService;

        // Application deadline required unless continuous posting
        if (!ContinuousPosting && !ApplicationDeadline.HasValue)
            yield return new ValidationResult(
                loc?.GetText("ApplicationDeadlineMandatory") ?? "Application deadline is required when continuous posting is not enabled.",
                new[] { nameof(ApplicationDeadline) });

        // Hire date required if type = Date
        if (HireDateType == 1 && !HireDate.HasValue)
            yield return new ValidationResult(
                loc?.GetText("HireDateMandatory") ?? "Hire date is required.",
                new[] { nameof(HireDate) });

        // Hire date must be >= application deadline
        if (HireDateType == 1 && HireDate.HasValue && ApplicationDeadline.HasValue
            && HireDate.Value < ApplicationDeadline.Value)
            yield return new ValidationResult(
                loc?.GetText("HireDateMustBeAfterDeadline") ?? "Hire date must be on or after the application deadline.",
                new[] { nameof(HireDate) });

        // Hire date free text required if type = FreeText
        if (HireDateType == 2 && string.IsNullOrWhiteSpace(HireDateFreeText))
            yield return new ValidationResult(
                loc?.GetText("HireDateFreeTextMandatory") ?? "Hire date text is required.",
                new[] { nameof(HireDateFreeText) });
    }
}
