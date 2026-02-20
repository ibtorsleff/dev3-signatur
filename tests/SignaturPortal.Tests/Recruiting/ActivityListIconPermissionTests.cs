using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;
using SignaturPortal.Infrastructure.Services;
using SignaturPortal.Tests.Helpers;

namespace SignaturPortal.Tests.Recruiting;

/// <summary>
/// Integration tests for the two new PermissionHelperService methods added for the action icons.
/// Verifies the AND logic for UserCanEditActivitiesNotMemberOfAsync and the
/// single-permission check for UserCanPublishWebAdAsync.
/// </summary>
public class ActivityListIconPermissionTests
{
    private const string TestUserName = "iconpermtest";

    // Permission IDs
    private const int RecruitmentAccess = (int)PortalPermission.RecruitmentPortalRecruitmentAccess;          // 2000
    private const int EditActivitiesNotMemberOf = (int)PortalPermission.RecruitmentPortalEditActivitiesUserNotMemberOf; // 2026
    private const int PublishWebAd = (int)PortalPermission.AdPortalPublishWebAd;                              // 1100

    private static (PermissionHelperService helper, SqliteConnection conn) CreateHelper(
        params int[] permissionIds)
    {
        var conn = SqliteCompatibleDbContextFactory.OpenConnection();

        var options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseSqlite(conn)
            .Options;

        SqliteCompatibleDbContextFactory.EnsureSchema(options);
        using (var db = new SignaturDbContext(options))
        {

            var appId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            db.Sites.Add(new Site { SiteId = 1, SiteName = "Test", SiteUrls = "t.local", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow });
            db.Clients.Add(new Client { ClientId = 10, SiteId = 1, CreateDate = DateTime.UtcNow });

            db.AspnetUsers.Add(new AspnetUser
            {
                UserId = userId,
                ApplicationId = appId,
                UserName = TestUserName,
                LoweredUserName = TestUserName.ToLower(),
                LastActivityDate = DateTime.UtcNow,
            });

            // Seed only the permission records that are needed
            var allPermIds = new[] { RecruitmentAccess, EditActivitiesNotMemberOf, PublishWebAd };
            foreach (var pid in allPermIds)
            {
                db.Permissions.Add(new Permission { PermissionId = pid, PermissionName = $"Perm{pid}", PermissionGroupId = 1, PermissionTypeId = 1, SortOrder = pid, TextKey = "t", InfoTextKey = "i" });
            }

            var role = new AspnetRole
            {
                RoleId = Guid.NewGuid(),
                ApplicationId = appId,
                RoleName = "TestRole",
                LoweredRoleName = "testrole",
                SiteId = 1,
                ClientId = 10,
                IsActive = true,
            };
            db.AspnetRoles.Add(role);
            db.SaveChanges();

            var user = db.AspnetUsers.Local.First(u => u.UserName == TestUserName);
            user.Roles.Add(role);

            // Assign only the requested permissions to the role
            foreach (var pid in permissionIds)
            {
                db.PermissionInRoles.Add(new PermissionInRole { RoleId = role.RoleId, PermissionId = pid });
            }

            db.SaveChanges();
        }

        var dbFactory = new TestDbContextFactory(options);
        var permSvc = new PermissionService(dbFactory);
        var session = new TestSessionContext(TestUserName);

        return (new PermissionHelperService(permSvc, session), conn);
    }

    // --- UserCanEditActivitiesNotMemberOfAsync ---

    [Test]
    public async Task EditActivitiesNotMemberOf_BothPermissions_ReturnsTrue()
    {
        var (helper, conn) = CreateHelper(RecruitmentAccess, EditActivitiesNotMemberOf);
        using var _ = conn;

        var result = await helper.UserCanEditActivitiesNotMemberOfAsync();

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task EditActivitiesNotMemberOf_OnlyRecruitmentAccess_ReturnsFalse()
    {
        // Missing EditActivitiesNotMemberOf — the AND requires both
        var (helper, conn) = CreateHelper(RecruitmentAccess);
        using var _ = conn;

        var result = await helper.UserCanEditActivitiesNotMemberOfAsync();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task EditActivitiesNotMemberOf_OnlyEditPermission_ReturnsFalse()
    {
        // Missing RecruitmentAccess — short-circuits to false
        var (helper, conn) = CreateHelper(EditActivitiesNotMemberOf);
        using var _ = conn;

        var result = await helper.UserCanEditActivitiesNotMemberOfAsync();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task EditActivitiesNotMemberOf_NoPermissions_ReturnsFalse()
    {
        var (helper, conn) = CreateHelper(); // no permissions
        using var _ = conn;

        var result = await helper.UserCanEditActivitiesNotMemberOfAsync();

        await Assert.That(result).IsFalse();
    }

    // --- UserCanPublishWebAdAsync ---

    [Test]
    public async Task PublishWebAd_HasPermission_ReturnsTrue()
    {
        var (helper, conn) = CreateHelper(PublishWebAd);
        using var _ = conn;

        var result = await helper.UserCanPublishWebAdAsync();

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task PublishWebAd_NoPermission_ReturnsFalse()
    {
        var (helper, conn) = CreateHelper(RecruitmentAccess); // has recruitment but not publish web ad
        using var _ = conn;

        var result = await helper.UserCanPublishWebAdAsync();

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task PublishWebAd_UninitializedSession_ReturnsFalse()
    {
        var conn = SqliteCompatibleDbContextFactory.OpenConnection();
        var options = new DbContextOptionsBuilder<SignaturDbContext>().UseSqlite(conn).Options;
        SqliteCompatibleDbContextFactory.EnsureSchema(options);

        var dbFactory = new TestDbContextFactory(options);
        var permSvc = new PermissionService(dbFactory);
        var uninitSession = new UninitializedSessionContext();

        var helper = new PermissionHelperService(permSvc, uninitSession);
        var result = await helper.UserCanPublishWebAdAsync();
        conn.Dispose();

        await Assert.That(result).IsFalse();
    }

    private class TestDbContextFactory : IDbContextFactory<SignaturDbContext>
    {
        private readonly DbContextOptions<SignaturDbContext> _options;
        public TestDbContextFactory(DbContextOptions<SignaturDbContext> options) => _options = options;
        public SignaturDbContext CreateDbContext() => new(_options);
    }

    private class TestSessionContext : IUserSessionContext
    {
        public TestSessionContext(string userName)
        {
            UserName = userName;
            IsInitialized = true;
        }
        public Guid? UserId => Guid.NewGuid();
        public int? SiteId => 1;
        public int? ClientId => 10;
        public string UserName { get; }
        public int UserLanguageId => 1;
        public bool IsInitialized { get; }
        public bool IsClientUser => false;
        public string? FullName => null;
    }

    private class UninitializedSessionContext : IUserSessionContext
    {
        public Guid? UserId => null;
        public int? SiteId => null;
        public int? ClientId => null;
        public string UserName => "";
        public int UserLanguageId => 1;
        public bool IsInitialized => false;
        public bool IsClientUser => false;
        public string? FullName => null;
    }
}
