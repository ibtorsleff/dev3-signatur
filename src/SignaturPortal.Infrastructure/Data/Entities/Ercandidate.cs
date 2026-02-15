using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Ercandidate
{
    public int ErcandidateId { get; set; }

    public int ErcandidateStatusId { get; set; }

    public int EractivityId { get; set; }

    public Guid? ErjobProfileId { get; set; }

    public string? MitIdUuid { get; set; }

    public string? CprNumber { get; set; }

    public int LanguageId { get; set; }

    public int? StatusMailCandidateStatusId { get; set; }

    public int? StatusMailCandidateLogId { get; set; }

    public int? StatusMailInterviewRoundNo { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public string ZipCode { get; set; } = null!;

    public string City { get; set; } = null!;

    public DateTime RegistrationDate { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public int WelcomeEmailStatus { get; set; }

    public int? WelcomeEmailCandidateLogId { get; set; }

    public int CandidateId2 { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public Guid IntId { get; set; }

    public DateTime? InterviewBookingLinkSendTimeStamp { get; set; }

    public bool InterviewBookingMissingReminderSend { get; set; }

    public bool InterviewReminderSend { get; set; }

    public bool IsInternalCandidate { get; set; }

    public int? InterviewRoundNo { get; set; }

    public bool CandidateChanged { get; set; }

    public string? CandidateChanges { get; set; }

    public DateTime? InterviewConfirmationLinkSendTimeStamp { get; set; }

    public bool InterviewConfirmationMissingReminderSend { get; set; }

    public int? ErjobBankCandidateId { get; set; }

    public Guid? InterviewAppointmentIcsId { get; set; }

    public int? InterviewAppointmentIcsSequence { get; set; }

    public bool? InterviewAppointmentApproved { get; set; }

    public DateTime? InterviewAppointmentApprovedTimeStamp { get; set; }

    public string? InterviewAppointmentApprovedCandidateResponse { get; set; }

    public string? CandidateExportData { get; set; }

    public bool CollectedForInterview { get; set; }

    public DateTime StatusChangedTimeStamp { get; set; }

    public string? ConcentHeadline { get; set; }

    public bool MustUpdateCombinePdfFile { get; set; }

    public bool IsAnonymised { get; set; }

    public DateTime? ConfirmedDate { get; set; }

    public DateTime? DeleteWarningSentTimestamp { get; set; }

    public bool? InterviewAppointmentIsTeamsMeeting { get; set; }

    public virtual Eractivity Eractivity { get; set; } = null!;
}
