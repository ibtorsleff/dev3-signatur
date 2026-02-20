using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;
using SignaturPortal.Infrastructure.Services;
using SignaturPortal.Tests.Helpers;

namespace SignaturPortal.Tests.Authorization;

public class PermissionServiceTests
{
    private const string TestUserName = "testuser";

    private static (PermissionService svc, SqliteConnection conn) CreateTestService(
        int siteId, int clientId, Action<SignaturDbContext> seedExtra)
    {
        var conn = SqliteCompatibleDbContextFactory.OpenConnection();

        var options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseSqlite(conn)
            .Options;

        // Create and seed
        SqliteCompatibleDbContextFactory.EnsureSchema(options);
        using (var seedDb = new SignaturDbContext(options))
        {

            var appId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            seedDb.Sites.Add(new Site { SiteId = siteId, SiteName = "Test", SiteUrls = "test.local", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow });
            seedDb.Clients.Add(new Client { ClientId = clientId, SiteId = siteId, CreateDate = DateTime.UtcNow });

            var user = new AspnetUser
            {
                UserId = userId,
                ApplicationId = appId,
                UserName = TestUserName,
                LoweredUserName = TestUserName.ToLowerInvariant(),
                LastActivityDate = DateTime.UtcNow,
            };
            seedDb.AspnetUsers.Add(user);

            // Permissions
            seedDb.Permissions.Add(new Permission { PermissionId = 2000, PermissionName = "RecruitmentAccess", PermissionGroupId = 1, PermissionTypeId = 1, SortOrder = 1, TextKey = "t", InfoTextKey = "i" });
            seedDb.Permissions.Add(new Permission { PermissionId = 2500, PermissionName = "AdminAccess", PermissionGroupId = 1, PermissionTypeId = 1, SortOrder = 2, TextKey = "t2", InfoTextKey = "i2" });

            seedExtra(seedDb);
            seedDb.SaveChanges();

            // Build factory + session stub
            var factory = new TestDbContextFactory(options);
            var session = new TestSessionContext(siteId, clientId);

            return (new PermissionService(factory), conn);
        }
    }

    [Test]
    public async Task HasPermission_ReturnsTrueWhenUserHasPermissionViaActiveRole()
    {
        var (svc, conn) = CreateTestService(1, 10, db =>
        {
            var appId = Guid.NewGuid();
            var role = new AspnetRole
            {
                RoleId = Guid.NewGuid(),
                ApplicationId = appId,
                RoleName = "Recruiter",
                LoweredRoleName = "recruiter",
                SiteId = 1,
                ClientId = 10,
                IsActive = true,
            };
            db.AspnetRoles.Add(role);
            db.SaveChanges();

            // Add user to role
            var user = db.AspnetUsers.Local.First(u => u.UserName == TestUserName);
            user.Roles.Add(role);

            // Add permission to role
            db.PermissionInRoles.Add(new PermissionInRole { RoleId = role.RoleId, PermissionId = 2000 });
        });
        using var _ = conn;

        var result = await svc.HasPermissionAsync(TestUserName, 2000);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasPermission_ReturnsFalseWhenRoleIsInactive()
    {
        var (svc, conn) = CreateTestService(1, 10, db =>
        {
            var appId = Guid.NewGuid();
            var role = new AspnetRole
            {
                RoleId = Guid.NewGuid(),
                ApplicationId = appId,
                RoleName = "InactiveRole",
                LoweredRoleName = "inactiverole",
                SiteId = 1,
                ClientId = 10,
                IsActive = false, // Inactive!
            };
            db.AspnetRoles.Add(role);
            db.SaveChanges();

            var user = db.AspnetUsers.Local.First(u => u.UserName == TestUserName);
            user.Roles.Add(role);

            db.PermissionInRoles.Add(new PermissionInRole { RoleId = role.RoleId, PermissionId = 2000 });
        });
        using var _ = conn;

        var result = await svc.HasPermissionAsync(TestUserName, 2000);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasPermission_ReturnsTrueForRoleOnDifferentTenant()
    {
        // PermissionService intentionally does NOT filter by tenant (CurrentSiteId stays null).
        // Rationale: users are site-specific at login time; once logged in, their username
        // identifies them uniquely and permission lookup sees all their roles.
        // Therefore a role on site=2/client=30 is still visible when checking permissions
        // for a session on site=1/client=10.
        var (svc, conn) = CreateTestService(1, 10, db =>
        {
            // Need site 2 + client 30 to exist
            db.Sites.Add(new Site { SiteId = 2, SiteName = "Other", SiteUrls = "other.local", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow });
            db.Clients.Add(new Client { ClientId = 30, SiteId = 2, CreateDate = DateTime.UtcNow });

            var appId = Guid.NewGuid();
            var role = new AspnetRole
            {
                RoleId = Guid.NewGuid(),
                ApplicationId = appId,
                RoleName = "OtherTenantRole",
                LoweredRoleName = "othertenantrole",
                SiteId = 2,    // Different site
                ClientId = 30, // Different client
                IsActive = true,
            };
            db.AspnetRoles.Add(role);
            db.SaveChanges();

            var user = db.AspnetUsers.Local.First(u => u.UserName == TestUserName);
            user.Roles.Add(role);

            db.PermissionInRoles.Add(new PermissionInRole { RoleId = role.RoleId, PermissionId = 2000 });
        });
        using var _ = conn;

        var result = await svc.HasPermissionAsync(TestUserName, 2000);
        await Assert.That(result).IsTrue();
    }

    /// <summary>Simple IDbContextFactory for tests.</summary>
    private class TestDbContextFactory : IDbContextFactory<SignaturDbContext>
    {
        private readonly DbContextOptions<SignaturDbContext> _options;
        public TestDbContextFactory(DbContextOptions<SignaturDbContext> options) => _options = options;
        public SignaturDbContext CreateDbContext() => new(_options);
    }

    /// <summary>Simple IUserSessionContext stub for tests.</summary>
    private class TestSessionContext : IUserSessionContext
    {
        public TestSessionContext(int siteId, int clientId)
        {
            SiteId = siteId;
            ClientId = clientId;
            IsInitialized = true;
        }
        public Guid? UserId => Guid.Empty;
        public int? SiteId { get; }
        public int? ClientId { get; }
        public string UserName => TestUserName;
        public int UserLanguageId => 1;
        public bool IsInitialized { get; }
        public bool IsClientUser => ClientId.HasValue && ClientId.Value > 0;
        public string? FullName => null;
    }
}
