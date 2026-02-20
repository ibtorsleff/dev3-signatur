using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.Authorization;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Enums;
using SignaturPortal.Infrastructure.Data;
using SignaturPortal.Infrastructure.Data.Entities;
using SignaturPortal.Infrastructure.Services;
using SignaturPortal.Tests.Helpers;

namespace SignaturPortal.Tests.Recruiting;

/// <summary>
/// Integration tests for Icon 1 (email warning) in ErActivityService.GetActivitiesAsync.
/// Verifies MembersMissingNotificationEmail is computed correctly for OnGoing activities
/// using the LEFT JOIN counting logic (NotificationMailSendToUser=false, excluding CreatedBy).
/// Uses SQLite in-memory for fast execution without SQL Server.
/// </summary>
public class ActivityListEmailWarningTests
{
    private const string TestUserName = "emailwarntest";
    private const int OnGoingStatusId = (int)ERActivityStatus.OnGoing; // 1
    private const int ClosedStatusId = (int)ERActivityStatus.Closed;   // 2

    // Fixed GUIDs for deterministic test data
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"); // creator / current user
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid UserC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private record TestSetup(ErActivityService Service, SqliteConnection Connection);

    /// <summary>
    /// Creates an in-memory SQLite database with one activity, seeds members via the callback,
    /// and wires up the ErActivityService with stub collaborators.
    /// </summary>
    private static TestSetup CreateService(
        Action<SignaturDbContext> seedMembers,
        int activityStatusId = OnGoingStatusId)
    {
        var conn = SqliteCompatibleDbContextFactory.OpenConnection();

        var options = new DbContextOptionsBuilder<SignaturDbContext>()
            .UseSqlite(conn)
            .Options;

        SqliteCompatibleDbContextFactory.EnsureSchema(options);
        using (var db = new SignaturDbContext(options))
        {

            db.Sites.Add(new Site { SiteId = 1, SiteName = "Test", SiteUrls = "t.local", ExternalSiteId = "-1", Enabled = true, LanguageId = 1, CreateDate = DateTime.UtcNow });
            db.Clients.Add(new Client { ClientId = 10, SiteId = 1, CreateDate = DateTime.UtcNow });
            db.SaveChanges();

            db.Eractivities.Add(new Eractivity
            {
                EractivityId = 1,
                ClientId = 10,
                EractivityStatusId = activityStatusId,
                CreatedBy = UserA,
                Responsible = UserA,
                ErapplicationTemplateId = 1,
                ErletterTemplateReceivedId = 1,
                ErletterTemplateInterviewId = 1,
                ErletterTemplateRejectedId = 1,
                ErnotifyRecruitmentCommitteeId = 1,
                ErletterTemplateRejectedAfterInterviewId = 1,
                Headline = "Test Activity",
                Jobtitle = "Test Job",
                JournalNo = "J1",
                ApplicationDeadline = DateTime.UtcNow.AddDays(30),
                CreateDate = DateTime.UtcNow,
                StatusChangedTimeStamp = DateTime.UtcNow,
                ApplicationTemplateLanguage = "3",
                EditedId = Guid.NewGuid(),
            });
            db.SaveChanges();

            seedMembers(db);
            db.SaveChanges();
        }

        var dbFactory = new LocalDbContextFactory(options);

        // Permissions: RecruitmentAccess(2000) + ViewActivitiesNotMemberOf(2025)
        // → canViewActivitiesNotMemberOf = true → no per-user WHERE filter on the activity query
        var permSvc = new StubPermissionService(
            (int)PortalPermission.RecruitmentPortalRecruitmentAccess,
            (int)PortalPermission.RecruitmentPortalViewActivitiesUserNotMemberOf);

        // Current user = UserA; UserId used in the email warning query (kept separate from CreatedBy check)
        var currentUserSvc = new StubCurrentUserService(
            new CurrentUserDto(UserA, "User A", TestUserName, null, false, true, 1, null, 1));

        // Non-client user: ClientId=null → CurrentClientId=null → no EF global client filter applied
        var session = new StubSessionContext(TestUserName);

        return new TestSetup(new ErActivityService(dbFactory, session, permSvc, currentUserSvc), conn);
    }

    // -----------------------------------------------------------------------

    [Test]
    public async Task EmailWarning_AllMembersHaveNotification_ReturnsZero()
    {
        // Members where NotificationMailSendToUser = true → none missing → count = 0
        var setup = CreateService(db =>
        {
            db.Eractivitymembers.AddRange(
                Member(1, 1, UserB, notificationMail: true),
                Member(2, 1, UserC, notificationMail: true));
        });
        using var _ = setup.Connection;

        var response = await setup.Service.GetActivitiesAsync(
            new GridRequest { PageSize = 25 },
            statusFilter: ERActivityStatus.OnGoing,
            includeEmailWarning: true);

        var item = response.Items.Single();
        await Assert.That(item.MembersMissingNotificationEmail).IsEqualTo(0);
    }

    [Test]
    public async Task EmailWarning_TwoMembersWithoutNotification_ReturnsTwoCount()
    {
        // Both members have NotificationMailSendToUser = false and are not the creator
        var setup = CreateService(db =>
        {
            db.Eractivitymembers.AddRange(
                Member(1, 1, UserB, notificationMail: false),
                Member(2, 1, UserC, notificationMail: false));
        });
        using var _ = setup.Connection;

        var response = await setup.Service.GetActivitiesAsync(
            new GridRequest { PageSize = 25 },
            statusFilter: ERActivityStatus.OnGoing,
            includeEmailWarning: true);

        var item = response.Items.Single();
        await Assert.That(item.MembersMissingNotificationEmail).IsEqualTo(2);
    }

    [Test]
    public async Task EmailWarning_CreatorMemberExcludedFromCount()
    {
        // UserA is both the activity creator (CreatedBy) and a member with NotificationMailSendToUser=false.
        // The SQL excludes members where UserId = CreatedBy, so UserA does NOT count.
        // UserB also has NotificationMailSendToUser=false and is NOT the creator, so UserB counts.
        // Expected: 1
        var setup = CreateService(db =>
        {
            db.Eractivitymembers.AddRange(
                Member(1, 1, UserA, notificationMail: false), // creator — excluded by SQL
                Member(2, 1, UserB, notificationMail: false)); // non-creator — counted
        });
        using var _ = setup.Connection;

        var response = await setup.Service.GetActivitiesAsync(
            new GridRequest { PageSize = 25 },
            statusFilter: ERActivityStatus.OnGoing,
            includeEmailWarning: true);

        var item = response.Items.Single();
        await Assert.That(item.MembersMissingNotificationEmail).IsEqualTo(1);
    }

    [Test]
    public async Task EmailWarning_NotPopulatedWhenFlagIsFalse()
    {
        // Even with members missing notifications, includeEmailWarning=false means the block is skipped
        var setup = CreateService(db =>
        {
            db.Eractivitymembers.AddRange(
                Member(1, 1, UserB, notificationMail: false),
                Member(2, 1, UserC, notificationMail: false));
        });
        using var _ = setup.Connection;

        var response = await setup.Service.GetActivitiesAsync(
            new GridRequest { PageSize = 25 },
            statusFilter: ERActivityStatus.OnGoing,
            includeEmailWarning: false); // not requested

        var item = response.Items.Single();
        await Assert.That(item.MembersMissingNotificationEmail).IsEqualTo(0);
    }

    [Test]
    public async Task EmailWarning_NotPopulatedForClosedActivity()
    {
        // The email warning block only runs for statusFilter == OnGoing.
        // With a Closed activity and statusFilter=Closed, the block is skipped.
        var setup = CreateService(db =>
        {
            db.Eractivitymembers.Add(Member(1, 1, UserB, notificationMail: false));
        }, activityStatusId: ClosedStatusId);
        using var _ = setup.Connection;

        var response = await setup.Service.GetActivitiesAsync(
            new GridRequest { PageSize = 25 },
            statusFilter: ERActivityStatus.Closed,
            includeEmailWarning: true);

        var item = response.Items.Single();
        await Assert.That(item.MembersMissingNotificationEmail).IsEqualTo(0);
    }

    // -----------------------------------------------------------------------

    private static Eractivitymember Member(int id, int activityId, Guid userId, bool notificationMail) => new()
    {
        EractivityMemberId = id,
        EractivityId = activityId,
        UserId = userId,
        EractivityMemberTypeId = 1,
        NotificationMailSendToUser = notificationMail,
    };

    private class LocalDbContextFactory : IDbContextFactory<SignaturDbContext>
    {
        private readonly DbContextOptions<SignaturDbContext> _options;
        public LocalDbContextFactory(DbContextOptions<SignaturDbContext> options) => _options = options;
        public SignaturDbContext CreateDbContext() => new(_options);
    }

    private class StubPermissionService : IPermissionService
    {
        private readonly IReadOnlySet<int> _permissions;
        public StubPermissionService(params int[] permissionIds)
            => _permissions = new HashSet<int>(permissionIds);
        public Task<bool> HasPermissionAsync(string userName, int permissionId, CancellationToken ct = default)
            => Task.FromResult(_permissions.Contains(permissionId));
        public Task<IReadOnlySet<int>> GetUserPermissionsAsync(string userName, CancellationToken ct = default)
            => Task.FromResult(_permissions);
    }

    private class StubCurrentUserService : ICurrentUserService
    {
        private readonly CurrentUserDto? _user;
        public StubCurrentUserService(CurrentUserDto? user) => _user = user;
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default)
            => Task.FromResult(_user);
        public Task<CurrentUserDto?> GetUserByNameAsync(string userName, CancellationToken ct = default)
            => Task.FromResult(_user);
    }

    private class StubSessionContext : IUserSessionContext
    {
        public StubSessionContext(string userName)
        {
            UserName = userName;
            IsInitialized = true;
        }
        public Guid? UserId => null;
        public int? SiteId => 1;
        public int? ClientId => null; // non-client user → CurrentClientId=null → no EF client filter
        public string UserName { get; }
        public int UserLanguageId => 1;
        public bool IsInitialized { get; }
        public bool IsClientUser => false;
        public string? FullName => null;
    }
}
