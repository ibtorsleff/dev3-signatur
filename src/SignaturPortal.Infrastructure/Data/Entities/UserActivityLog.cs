using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class UserActivityLog
{
    public int UserLogId { get; set; }

    public Guid ActionUserId { get; set; }

    public Guid? TargetUserId { get; set; }

    public int? EntityTypeId { get; set; }

    public int? EntityId { get; set; }

    public DateTime TimeStamp { get; set; }

    public string Log { get; set; } = null!;

    public string? HeaderEmail { get; set; }

    public string? ContentEmail { get; set; }

    public Guid? OnBehalfOfUserId { get; set; }

    public string? ContentSms { get; set; }

    public int? EmailReceiverTypeId { get; set; }
}
