using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;
using SignaturPortal.Infrastructure.Interceptors;
using SignaturPortal.Tests.Helpers;

namespace SignaturPortal.Tests.MultiTenancy;

public class SaveChangesInterceptorTests
{
    private static (SignaturDbContext db, SqliteConnection conn) CreateDbWithInterceptor(int? clientId)
    {
        var conn = SqliteCompatibleDbContextFactory.OpenConnection();

        var options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(new TenantSaveChangesInterceptor())
            .Options;

        SqliteCompatibleDbContextFactory.EnsureSchema(options);
        var db = new SignaturDbContext(options) { CurrentClientId = clientId, CurrentSiteId = 1 };

        // Seed minimal data
        db.Sites.Add(new Site { SiteId = 1, SiteName = "Test", SiteUrls = "test.local", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow });
        db.Clients.Add(new Client { ClientId = 10, SiteId = 1, CreateDate = DateTime.UtcNow });
        db.Clients.Add(new Client { ClientId = 99, SiteId = 1, CreateDate = DateTime.UtcNow });
        // Temporarily remove tenant to seed
        db.CurrentClientId = null;
        db.CurrentSiteId = null;
        db.SaveChanges();
        db.CurrentClientId = clientId;
        db.CurrentSiteId = 1;

        return (db, conn);
    }

    [Test]
    public async Task SaveChanges_RejectsMismatchedClientId()
    {
        var (db, conn) = CreateDbWithInterceptor(clientId: 10);
        await using var _ = db;
        using var __ = conn;

        var userGuid = Guid.NewGuid();
        db.Eractivities.Add(new Eractivity
        {
            ClientId = 99, // WRONG — current tenant is 10
            Responsible = userGuid,
            CreatedBy = userGuid,
            EractivityStatusId = 1,
            ErapplicationTemplateId = 1,
            ErletterTemplateReceivedId = 1,
            ErletterTemplateInterviewId = 1,
            ErletterTemplateRejectedId = 1,
            ErnotifyRecruitmentCommitteeId = 1,
            ErletterTemplateRejectedAfterInterviewId = 1,
            Headline = "Wrong tenant",
            Jobtitle = "Job",
            JournalNo = "J",
            ApplicationDeadline = DateTime.UtcNow.AddDays(30),
            CreateDate = DateTime.UtcNow,
            StatusChangedTimeStamp = DateTime.UtcNow,
            ApplicationTemplateLanguage = "3",
            EditedId = Guid.NewGuid(),
        });

        var act = () => db.SaveChangesAsync();
        await Assert.That(act).ThrowsExactly<InvalidOperationException>()
            .And.HasMessageContaining("Tenant violation");
    }

    [Test]
    public async Task SaveChanges_AllowsMatchingClientId()
    {
        var (db, conn) = CreateDbWithInterceptor(clientId: 10);
        await using var _ = db;
        using var __ = conn;

        var userGuid = Guid.NewGuid();
        db.Eractivities.Add(new Eractivity
        {
            ClientId = 10, // Correct tenant
            Responsible = userGuid,
            CreatedBy = userGuid,
            EractivityStatusId = 1,
            ErapplicationTemplateId = 1,
            ErletterTemplateReceivedId = 1,
            ErletterTemplateInterviewId = 1,
            ErletterTemplateRejectedId = 1,
            ErnotifyRecruitmentCommitteeId = 1,
            ErletterTemplateRejectedAfterInterviewId = 1,
            Headline = "Correct tenant",
            Jobtitle = "Job",
            JournalNo = "J",
            ApplicationDeadline = DateTime.UtcNow.AddDays(30),
            CreateDate = DateTime.UtcNow,
            StatusChangedTimeStamp = DateTime.UtcNow,
            ApplicationTemplateLanguage = "3",
            EditedId = Guid.NewGuid(),
        });

        var saved = await db.SaveChangesAsync();
        await Assert.That(saved).IsGreaterThan(0);
    }

    [Test]
    public async Task SaveChanges_SkipsValidationWhenNoTenant()
    {
        var (db, conn) = CreateDbWithInterceptor(clientId: null); // No tenant = system context
        await using var _ = db;
        using var __ = conn;

        var userGuid = Guid.NewGuid();
        db.Eractivities.Add(new Eractivity
        {
            ClientId = 99, // Any client is fine when no tenant set
            Responsible = userGuid,
            CreatedBy = userGuid,
            EractivityStatusId = 1,
            ErapplicationTemplateId = 1,
            ErletterTemplateReceivedId = 1,
            ErletterTemplateInterviewId = 1,
            ErletterTemplateRejectedId = 1,
            ErnotifyRecruitmentCommitteeId = 1,
            ErletterTemplateRejectedAfterInterviewId = 1,
            Headline = "No tenant",
            Jobtitle = "Job",
            JournalNo = "J",
            ApplicationDeadline = DateTime.UtcNow.AddDays(30),
            CreateDate = DateTime.UtcNow,
            StatusChangedTimeStamp = DateTime.UtcNow,
            ApplicationTemplateLanguage = "3",
            EditedId = Guid.NewGuid(),
        });

        var saved = await db.SaveChangesAsync();
        await Assert.That(saved).IsGreaterThan(0);
    }
}
