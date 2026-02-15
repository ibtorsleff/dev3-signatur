using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Eractivity
{
    public int EractivityId { get; set; }

    public int? ActivityId { get; set; }

    public int? WebAdId { get; set; }

    public int ClientId { get; set; }

    public int? ClientSectionId { get; set; }

    public Guid Responsible { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? DraftResponsible { get; set; }

    public int EractivityStatusId { get; set; }

    public int ErapplicationTemplateId { get; set; }

    public int ErletterTemplateReceivedId { get; set; }

    public int ErletterTemplateInterviewId { get; set; }

    public int ErletterTemplateRejectedId { get; set; }

    public int ErnotifyRecruitmentCommitteeId { get; set; }

    public int? JobnetOccupationId { get; set; }

    public string Headline { get; set; } = null!;

    public string Jobtitle { get; set; } = null!;

    public string? JobTitleLanguage { get; set; }

    public string JournalNo { get; set; } = null!;

    public bool ContinuousPosting { get; set; }

    public DateTime ApplicationDeadline { get; set; }

    public DateTime? HireDate { get; set; }

    public string? HireDateFreeText { get; set; }

    public bool EmailOnNewCandidate { get; set; }

    public bool CandidateEvaluationEnabled { get; set; }

    public bool CandidateExtendedEvaluationEnabled { get; set; }

    public bool IsCleaned { get; set; }

    public int LastCandidateId2 { get; set; }

    public int? ErtemplateGroupId { get; set; }

    public bool AutoRejectionEnabled { get; set; }

    public int ErletterTemplateRejectedAfterInterviewId { get; set; }

    public bool ApplicationTemplateIsCustomized { get; set; }

    public int RecruitmentMembersNotifyMailsSend { get; set; }

    public int? InterviewDuration { get; set; }

    public Guid EditedId { get; set; }

    public bool SendDailyStatusEmailEnabled { get; set; }

    public DateTime? SendDailyStatusEmailLastSend { get; set; }

    public DateTime? SendMembersMissingNotificationEmailLastSend { get; set; }

    public DateTime CreateDate { get; set; }

    public int InterviewRounds { get; set; }

    public int? ErletterTemplateInterviewTwoPlusRoundsId { get; set; }

    public bool LockCandidateEvalutionAndNotesWhenUserHasNotEvaluated { get; set; }

    public DateTime? CandidateStatusLastChangedTimestamp { get; set; }

    public DateTime? SendCandidatesMissingRejectionEmailLastSend { get; set; }

    public bool ViewRecruitmentCommitteeEvaluations { get; set; }

    public int CompletionOfActivityNotificationEmailCount { get; set; }

    public DateTime? CompletionOfActivityNotificationEmailLastSend { get; set; }

    public string ApplicationTemplateLanguage { get; set; } = null!;

    public bool AllowPrivateComments { get; set; }

    public int CalendarTypeId { get; set; }

    public int? ErsmsTemplateInterviewId { get; set; }

    public bool SendSmsInterviewRemembrances { get; set; }

    public int? ErjobBankId { get; set; }

    public int? ErjobBankClientSectionId { get; set; }

    public string? JobBankNewVacancyEmailSubject { get; set; }

    public string? JobBankNewVacancyEmailBody { get; set; }

    public bool IsReposting { get; set; }

    public bool NotSendRejectionsThemselveEmailSent { get; set; }

    public bool? JobBankSkipSendNewVancancyMail { get; set; }

    public DateTime StatusChangedTimeStamp { get; set; }

    public bool? IsLeadershipPosition { get; set; }

    public bool? IsBlindRecruitment { get; set; }

    public string? DraftData { get; set; }

    public virtual Client Client { get; set; } = null!;

    public virtual ICollection<Eractivitymember> Eractivitymembers { get; set; } = new List<Eractivitymember>();

    public virtual ICollection<Ercandidate> Ercandidates { get; set; } = new List<Ercandidate>();
}
