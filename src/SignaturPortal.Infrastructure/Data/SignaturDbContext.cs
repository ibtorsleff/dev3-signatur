using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data.Entities;

namespace SignaturPortal.Infrastructure.Data;

public partial class SignaturDbContext : DbContext
{
    public SignaturDbContext(DbContextOptions<SignaturDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspnetMembership> AspnetMemberships { get; set; }

    public virtual DbSet<AspnetRole> AspnetRoles { get; set; }

    public virtual DbSet<AspnetUser> AspnetUsers { get; set; }

    public virtual DbSet<Client> Clients { get; set; }

    public virtual DbSet<Eractivity> Eractivities { get; set; }

    public virtual DbSet<Ercandidate> Ercandidates { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Site> Sites { get; set; }

    public virtual DbSet<UserActivityLog> UserActivityLogs { get; set; }

    public virtual DbSet<PermissionInRole> PermissionInRoles { get; set; }

    public virtual DbSet<Eractivitymember> Eractivitymembers { get; set; }

    public virtual DbSet<BinaryFile> BinaryFiles { get; set; }

    public virtual DbSet<Ercandidatefile> Ercandidatefiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_CI_AS");

        modelBuilder.Entity<AspnetMembership>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__aspnet_Membershi__37703C52")
                .IsClustered(false);

            entity.ToTable("aspnet_Membership");

            entity.HasIndex(e => e.IsApproved, "IX_IsApproved_UserId>");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Comment).HasColumnType("ntext");
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FailedPasswordAnswerAttemptWindowStart).HasColumnType("datetime");
            entity.Property(e => e.FailedPasswordAttemptWindowStart).HasColumnType("datetime");
            entity.Property(e => e.LastLockoutDate).HasColumnType("datetime");
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.LastPasswordChangedDate).HasColumnType("datetime");
            entity.Property(e => e.LoweredEmail).HasMaxLength(256);
            entity.Property(e => e.MobilePin)
                .HasMaxLength(16)
                .HasColumnName("MobilePIN");
            entity.Property(e => e.Password).HasMaxLength(128);
            entity.Property(e => e.PasswordAnswer).HasMaxLength(128);
            entity.Property(e => e.PasswordQuestion).HasMaxLength(256);
            entity.Property(e => e.PasswordSalt).HasMaxLength(128);

            entity.HasOne(d => d.User).WithOne(p => p.AspnetMembership)
                .HasForeignKey<AspnetMembership>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__aspnet_Me__UserI__5D60DB10");
        });

        modelBuilder.Entity<AspnetRole>(entity =>
        {
            entity.HasKey(e => e.RoleId)
                .HasName("PK__aspnet_Roles__3A4CA8FD")
                .IsClustered(false);

            entity.ToTable("aspnet_Roles");

            entity.HasIndex(e => new { e.SiteId, e.ClientId, e.IsActive }, "IX_Aspnet_Rols_SiteId_ClientId_IsActive");

            entity.Property(e => e.RoleId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ClientCanUse).HasDefaultValue(true, "DF_aspnet_Roles_ClientCanUse");
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.LoweredRoleName).HasMaxLength(256);
            entity.Property(e => e.RoleName).HasMaxLength(256);
            entity.Property(e => e.SiteId)
                .HasDefaultValue(1)
                .HasColumnName("siteId");
        });

        modelBuilder.Entity<AspnetUser>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__aspnet_Users__32AB8735")
                .IsClustered(false);

            entity.ToTable("aspnet_Users");

            entity.HasIndex(e => e.UserName, "IX_User_UserName");

            entity.HasIndex(e => e.LoweredUserName, "aspnet_Users_LoweredUserName");

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LastActivityDate).HasColumnType("datetime");
            entity.Property(e => e.LoweredUserName).HasMaxLength(256);
            entity.Property(e => e.MobileAlias)
                .HasMaxLength(16)
                .HasDefaultValueSql("(NULL)");
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspnetUsersInRole",
                    r => r.HasOne<AspnetRole>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__aspnet_Us__RoleI__4B422AD5"),
                    l => l.HasOne<AspnetUser>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__aspnet_Us__UserI__4C364F0E"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId").HasName("PK__aspnet_UsersInRo__208CD6FA");
                        j.ToTable("aspnet_UsersInRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_aspnet_UsersInRoles_RoleId");
                    });
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Client");

            entity.HasIndex(e => e.ObjectData, "XML_IX_User");

            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())", "DF_Client_CreateDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.ObjectData).HasColumnType("xml");
            entity.Property(e => e.ObjectDataHistory).HasColumnType("xml");

            entity.HasOne(d => d.Site).WithMany(p => p.Clients)
                .HasForeignKey(d => d.SiteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Client_Site");
        });

        modelBuilder.Entity<Eractivity>(entity =>
        {
            entity.ToTable("ERActivity");

            entity.HasIndex(e => new { e.EractivityStatusId, e.SendDailyStatusEmailEnabled, e.ApplicationDeadline }, "ERActivity_ERActivityStatusId_SendDailyStatusEmailEnabled_ApplicationDeadline");

            entity.HasIndex(e => new { e.ClientId, e.EractivityStatusId, e.ApplicationDeadline }, "IX_ClientId,ERActivityStatusId,ApplicationDeadline");

            entity.HasIndex(e => new { e.ClientId, e.EractivityStatusId, e.StatusChangedTimeStamp }, "IX_ClientId_ERActivityStatusId_StatusChangedTimeStamp");

            entity.HasIndex(e => new { e.ClientId, e.JobnetOccupationId }, "IX_ClientId_JobnetOccupationId");

            entity.HasIndex(e => e.EractivityStatusId, "IX_ERActivityStatusId");

            entity.HasIndex(e => e.EractivityStatusId, "IX_ERActivityStatusId_ERActivityId_ActivityId_Responsible_CreatedBy_CandidateEvaluationEnabled");

            entity.HasIndex(e => new { e.ClientId, e.EractivityStatusId }, "IX_ERActivity_ClientId_ERActivityStatusId");

            entity.HasIndex(e => new { e.ClientId, e.EractivityStatusId, e.IsCleaned }, "IX_ERActivity_ClientId_ERActivityStatusId_IsCleaned");

            entity.HasIndex(e => e.WebAdId, "IX_ERActivity_WebAdId");

            entity.HasIndex(e => e.WebAdId, "IX_WebAdId");

            entity.Property(e => e.EractivityId).HasColumnName("ERActivityId");
            entity.Property(e => e.ApplicationDeadline).HasColumnType("datetime");
            entity.Property(e => e.ApplicationTemplateLanguage)
                .HasMaxLength(10)
                .HasDefaultValueSql("((3))", "DF_ERActivity_ApplicationTemplateLanguage");
            entity.Property(e => e.CandidateStatusLastChangedTimestamp).HasColumnType("datetime");
            entity.Property(e => e.CompletionOfActivityNotificationEmailLastSend).HasColumnType("datetime");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())", "DF__ERActivit__Creat__7953D99F")
                .HasColumnType("datetime");
            entity.Property(e => e.DraftData).HasColumnType("xml");
            entity.Property(e => e.EditedId).HasDefaultValueSql("(newid())", "DF_ERActivity_EditedGuid");
            entity.Property(e => e.EractivityStatusId).HasColumnName("ERActivityStatusId");
            entity.Property(e => e.ErapplicationTemplateId).HasColumnName("ERApplicationTemplateId");
            entity.Property(e => e.ErjobBankClientSectionId).HasColumnName("ERJobBankClientSectionId");
            entity.Property(e => e.ErjobBankId).HasColumnName("ERJobBankId");
            entity.Property(e => e.ErletterTemplateInterviewId).HasColumnName("ERLetterTemplateInterviewId");
            entity.Property(e => e.ErletterTemplateInterviewTwoPlusRoundsId).HasColumnName("ERLetterTemplateInterviewTwoPlusRoundsId");
            entity.Property(e => e.ErletterTemplateReceivedId).HasColumnName("ERLetterTemplateReceivedId");
            entity.Property(e => e.ErletterTemplateRejectedAfterInterviewId).HasColumnName("ERLetterTemplateRejectedAfterInterviewId");
            entity.Property(e => e.ErletterTemplateRejectedId).HasColumnName("ERLetterTemplateRejectedId");
            entity.Property(e => e.ErnotifyRecruitmentCommitteeId).HasColumnName("ERNotifyRecruitmentCommitteeId");
            entity.Property(e => e.ErsmsTemplateInterviewId).HasColumnName("ERSmsTemplateInterviewId");
            entity.Property(e => e.ErtemplateGroupId)
                .HasDefaultValue(0, "DF__ERActivit__ERTem__18427513")
                .HasColumnName("ERTemplateGroupId");
            entity.Property(e => e.Headline).HasMaxLength(255);
            entity.Property(e => e.HireDate).HasColumnType("datetime");
            entity.Property(e => e.HireDateFreeText).HasMaxLength(100);
            entity.Property(e => e.InterviewRounds).HasDefaultValue(1, "DF__ERActivit__Inter__5A502F92");
            entity.Property(e => e.JobBankNewVacancyEmailBody).HasColumnType("text");
            entity.Property(e => e.JobBankNewVacancyEmailSubject).HasMaxLength(256);
            entity.Property(e => e.Jobtitle).HasMaxLength(255);
            entity.Property(e => e.JournalNo).HasMaxLength(50);
            entity.Property(e => e.SendCandidatesMissingRejectionEmailLastSend).HasColumnType("datetime");
            entity.Property(e => e.SendDailyStatusEmailLastSend).HasColumnType("datetime");
            entity.Property(e => e.SendMembersMissingNotificationEmailLastSend).HasColumnType("datetime");
            entity.Property(e => e.StatusChangedTimeStamp)
                .HasDefaultValueSql("(getdate())", "DF_ERActivity_StatusChangedTimeStamp")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Client).WithMany(p => p.Eractivities)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERActivity_Client");
        });

        modelBuilder.Entity<Ercandidate>(entity =>
        {
            entity.ToTable("ERCandidate");

            entity.HasIndex(e => e.EractivityId, "IX_ERActivityId");

            entity.HasIndex(e => e.WelcomeEmailCandidateLogId, "IX_ERCandidateLog_WelcomeEmailCandidateLogId");

            entity.HasIndex(e => new { e.ErcandidateStatusId, e.IsDeleted, e.StatusChangedTimeStamp }, "IX_ERCandidateStatusId_IsDeleted_StatusChangedTimeStamp_ERActivityId");

            entity.HasIndex(e => e.IsDeleted, "IX_IsDeleted_ERActivityId_ZipCode");

            entity.Property(e => e.ErcandidateId).HasColumnName("ERCandidateId");
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.CandidateExportData).HasColumnType("xml");
            entity.Property(e => e.City).HasMaxLength(50);
            entity.Property(e => e.ConcentHeadline).HasColumnType("text");
            entity.Property(e => e.ConfirmedDate).HasColumnType("datetime");
            entity.Property(e => e.CprNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DeleteWarningSentTimestamp).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EractivityId).HasColumnName("ERActivityId");
            entity.Property(e => e.ErcandidateStatusId).HasColumnName("ERCandidateStatusId");
            entity.Property(e => e.ErjobBankCandidateId).HasColumnName("ERJobBankCandidateId");
            entity.Property(e => e.ErjobProfileId).HasColumnName("ERJobProfileId");
            entity.Property(e => e.FirstName).HasMaxLength(75);
            entity.Property(e => e.IntId).HasDefaultValueSql("(newid())", "DF_ERCandidate_IntId");
            entity.Property(e => e.InterviewAppointmentApprovedCandidateResponse).HasMaxLength(500);
            entity.Property(e => e.InterviewAppointmentApprovedTimeStamp).HasColumnType("datetime");
            entity.Property(e => e.InterviewBookingLinkSendTimeStamp).HasColumnType("datetime");
            entity.Property(e => e.InterviewConfirmationLinkSendTimeStamp).HasColumnType("datetime");
            entity.Property(e => e.InterviewRoundNo).HasDefaultValueSql("(NULL)", "DF__ERCandida__Inter__5D2C9C3D");
            entity.Property(e => e.LanguageId).HasDefaultValue(3, "DF_ERCandidate_LanguageId");
            entity.Property(e => e.LastName).HasMaxLength(75);
            entity.Property(e => e.MitIdUuid).HasMaxLength(50);
            entity.Property(e => e.RegistrationDate).HasColumnType("datetime");
            entity.Property(e => e.StatusChangedTimeStamp)
                .HasDefaultValueSql("(getdate())", "DF_ERCandidate_StatusChangedTimeStamp")
                .HasColumnType("datetime");
            entity.Property(e => e.Telephone).HasMaxLength(50);
            entity.Property(e => e.ZipCode).HasMaxLength(10);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permission");

            entity.Property(e => e.PermissionId).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.InfoTextKey).HasMaxLength(100);
            entity.Property(e => e.PermissionName).HasMaxLength(75);
            entity.Property(e => e.TextKey).HasMaxLength(100);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("Site");

            entity.Property(e => e.SiteId).ValueGeneratedNever();
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())", "DF_Site_CreateDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.Enabled).HasDefaultValue(true, "DF_Site_Enabled");
            entity.Property(e => e.ExternalSiteId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("((-1))", "DF__Site__ExternalSi__038683F8");
            entity.Property(e => e.LanguageId).HasDefaultValue(1, "DF_Site_LanguageId");
            entity.Property(e => e.ObjectData).HasColumnType("xml");
            entity.Property(e => e.PlugInDll)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.PrimarySiteUrl)
                .HasMaxLength(128)
                .IsUnicode(false);
            entity.Property(e => e.SiteName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SiteUrls)
                .HasMaxLength(512)
                .IsUnicode(false)
                .HasColumnName("SiteURLs");
        });

        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            entity.HasKey(e => e.UserLogId).HasName("PK__UserActi__7F8B81310076350C");

            entity.ToTable("UserActivityLog");

            entity.HasIndex(e => e.ActionUserId, "IX_UserActivityLog_ActionUserId");

            entity.HasIndex(e => new { e.EntityTypeId, e.EntityId }, "IX_UserActivityLog_EntityTypeId_EntityId");

            entity.HasIndex(e => e.TargetUserId, "IX_UserActivityLog_TargetUserId");

            entity.Property(e => e.ContentEmail).HasColumnType("text");
            entity.Property(e => e.ContentSms).HasColumnType("text");
            entity.Property(e => e.HeaderEmail).HasMaxLength(500);
            entity.Property(e => e.Log).HasColumnType("text");
            entity.Property(e => e.TimeStamp).HasColumnType("datetime");
        });

        modelBuilder.Entity<Eractivitymember>(entity =>
        {
            entity.HasKey(e => e.EractivityMemberId);

            entity.ToTable("ERActivityMember");

            entity.HasIndex(e => new { e.EractivityId, e.EractivityMemberTypeId, e.ExtUserAllowCandidateReview }, "ERActivityMember_IDX01");

            entity.HasIndex(e => e.UserId, "IX_UserId_ExtUserId");

            entity.Property(e => e.EractivityMemberId).HasColumnName("ERActivityMemberId");
            entity.Property(e => e.EractivityId).HasColumnName("ERActivityId");
            entity.Property(e => e.EractivityMemberTypeId).HasColumnName("ERActivityMemberTypeId");

            entity.HasOne(d => d.Eractivity).WithMany(p => p.Eractivitymembers)
                .HasForeignKey(d => d.EractivityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERActivityMember_ERActivity");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERActivityMember_User");
        });

        modelBuilder.Entity<BinaryFile>(entity =>
        {
            entity.HasKey(e => e.BinaryFileId);

            entity.ToTable("BinaryFile");

            entity.Property(e => e.BinaryFileId).ValueGeneratedOnAdd();
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.FileData).HasColumnType("varbinary(max)");
        });

        modelBuilder.Entity<Ercandidatefile>(entity =>
        {
            entity.HasKey(e => new { e.BinaryFileId, e.ErcandidateId });

            entity.ToTable("ERCandidateFile");

            entity.HasIndex(e => e.ErcandidateId, "IX_ERCandidateId");

            entity.Property(e => e.ErcandidateId).HasColumnName("ERCandidateId");
            entity.Property(e => e.ErcandidateFileConversionStatusId).HasColumnName("ERCandidateFileConversionStatusId");
            entity.Property(e => e.EruploadCategoryClientId).HasColumnName("ERUploadCategoryClientId");
            entity.Property(e => e.ConvertedFileName).HasMaxLength(255);
            entity.Property(e => e.ConversionErrorMessage).HasMaxLength(255);
            entity.Property(e => e.ConversionErrorMessageDetails).HasMaxLength(1000);
            entity.Property(e => e.FileException).HasMaxLength(500);

            entity.HasOne(d => d.BinaryFile).WithMany(p => p.Ercandidatefiles)
                .HasForeignKey(d => d.BinaryFileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERCandidateFile_BinaryFile");

            entity.HasOne(d => d.Ercandidate).WithMany()
                .HasForeignKey(d => d.ErcandidateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ERCandidateFile_ERCandidate");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
