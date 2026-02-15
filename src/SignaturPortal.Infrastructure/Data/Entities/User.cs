using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public int SiteId { get; set; }

    public string? ObjectData { get; set; }

    public string? ObjectDataHistory { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsInternal { get; set; }

    public int? ClientId { get; set; }

    public bool? Enabled { get; set; }

    public string? FullName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? Title { get; set; }

    public string? OfficePhone { get; set; }

    public string? CellPhone { get; set; }

    public string? ExtUserId { get; set; }

    public Guid? ForgotPasswordId { get; set; }

    public DateTime? ForgotPasswordTimestamp { get; set; }

    public string? WorkArea { get; set; }

    public string? EmployeeNumber { get; set; }

    public Guid? KombitUuid { get; set; }
}
