using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;

namespace SignaturPortal.Tests.Helpers;

/// <summary>
/// Creates SQLite-backed SignaturDbContext instances for integration tests.
/// Seeds multi-tenant data: Site A (1) with Clients 10/20, Site B (2) with Client 30.
/// </summary>
public class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SignaturDbContext> _options;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
        SeedData(db);
    }

    public SignaturDbContext CreateContext(int? siteId = null, int? clientId = null)
    {
        var db = new SignaturDbContext(_options)
        {
            CurrentSiteId = siteId,
            CurrentClientId = clientId,
        };
        return db;
    }

    private static void SeedData(SignaturDbContext db)
    {
        // Sites
        var siteA = new Site { SiteId = 1, SiteName = "Site A", SiteUrls = "a.test", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow };
        var siteB = new Site { SiteId = 2, SiteName = "Site B", SiteUrls = "b.test", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow };
        db.Sites.AddRange(siteA, siteB);

        // Clients
        var clientX = new Client { ClientId = 10, SiteId = 1, CreateDate = DateTime.UtcNow };
        var clientY = new Client { ClientId = 20, SiteId = 1, CreateDate = DateTime.UtcNow };
        var clientZ = new Client { ClientId = 30, SiteId = 2, CreateDate = DateTime.UtcNow };
        db.Clients.AddRange(clientX, clientY, clientZ);

        // Eractivity records per client
        var appId = Guid.NewGuid();
        var userGuid = Guid.NewGuid();

        var actX1 = CreateActivity(1, 10, userGuid);
        var actX2 = CreateActivity(2, 10, userGuid);
        var actY1 = CreateActivity(3, 20, userGuid);
        var actZ1 = CreateActivity(4, 30, userGuid);
        db.Eractivities.AddRange(actX1, actX2, actY1, actZ1);

        // Ercandidates
        db.Ercandidates.AddRange(
            CreateCandidate(1, 1, "Alice"),
            CreateCandidate(2, 2, "Bob"),
            CreateCandidate(3, 3, "Charlie"),
            CreateCandidate(4, 4, "Diana"));

        // Roles (for query filter tests)
        var roleApp = Guid.NewGuid();
        db.AspnetRoles.AddRange(
            new AspnetRole { RoleId = Guid.NewGuid(), ApplicationId = roleApp, RoleName = "SiteA_ClientX_Role", LoweredRoleName = "sitea_clientx_role", SiteId = 1, ClientId = 10, IsActive = true },
            new AspnetRole { RoleId = Guid.NewGuid(), ApplicationId = roleApp, RoleName = "SiteA_SiteWide_Role", LoweredRoleName = "sitea_sitewide_role", SiteId = 1, ClientId = null, IsActive = true },
            new AspnetRole { RoleId = Guid.NewGuid(), ApplicationId = roleApp, RoleName = "SiteB_ClientZ_Role", LoweredRoleName = "siteb_clientz_role", SiteId = 2, ClientId = 30, IsActive = true },
            new AspnetRole { RoleId = Guid.NewGuid(), ApplicationId = roleApp, RoleName = "SiteA_Inactive", LoweredRoleName = "sitea_inactive", SiteId = 1, ClientId = 10, IsActive = false });

        // Permissions
        db.Permissions.AddRange(
            new Permission { PermissionId = 2000, PermissionName = "RecruitmentAccess", PermissionGroupId = 1, PermissionTypeId = 1, SortOrder = 1, TextKey = "t1", InfoTextKey = "i1" },
            new Permission { PermissionId = 2500, PermissionName = "AdminAccess", PermissionGroupId = 1, PermissionTypeId = 1, SortOrder = 2, TextKey = "t2", InfoTextKey = "i2" });

        db.SaveChanges();
    }

    private static Eractivity CreateActivity(int id, int clientId, Guid userGuid) => new()
    {
        EractivityId = id,
        ClientId = clientId,
        Responsible = userGuid,
        CreatedBy = userGuid,
        EractivityStatusId = 1,
        ErapplicationTemplateId = 1,
        ErletterTemplateReceivedId = 1,
        ErletterTemplateInterviewId = 1,
        ErletterTemplateRejectedId = 1,
        ErnotifyRecruitmentCommitteeId = 1,
        ErletterTemplateRejectedAfterInterviewId = 1,
        Headline = $"Activity {id}",
        Jobtitle = $"Job {id}",
        JournalNo = $"J{id}",
        ApplicationDeadline = DateTime.UtcNow.AddDays(30),
        CreateDate = DateTime.UtcNow,
        StatusChangedTimeStamp = DateTime.UtcNow,
        ApplicationTemplateLanguage = "3",
        EditedId = Guid.NewGuid(),
    };

    private static Ercandidate CreateCandidate(int id, int activityId, string firstName) => new()
    {
        ErcandidateId = id,
        EractivityId = activityId,
        ErcandidateStatusId = 1,
        LanguageId = 3,
        FirstName = firstName,
        LastName = "Test",
        Address = "Addr",
        Email = $"{firstName.ToLower()}@test.dk",
        Telephone = "12345678",
        ZipCode = "1000",
        City = "Copenhagen",
        RegistrationDate = DateTime.UtcNow,
        StatusChangedTimeStamp = DateTime.UtcNow,
        IntId = Guid.NewGuid(),
    };

    public void Dispose()
    {
        _connection.Dispose();
    }
}
