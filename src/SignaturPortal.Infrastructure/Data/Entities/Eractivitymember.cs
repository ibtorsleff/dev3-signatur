using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Eractivitymember
{
    public int EractivityMemberId { get; set; }

    public int EractivityId { get; set; }

    public Guid UserId { get; set; }

    public int EractivityMemberTypeId { get; set; }

    public bool? ExtUserAllowCandidateManagement { get; set; }

    public bool? ExtUserAllowCandidateReview { get; set; }

    public bool? ExtUserAllowViewEditNotes { get; set; }

    public bool? NotificationMailSendToUser { get; set; }

    public virtual Eractivity Eractivity { get; set; } = null!;

    public virtual AspnetUser User { get; set; } = null!;
}
