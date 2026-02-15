using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data.Entities;
using SignaturPortal.Tests.Helpers;

namespace SignaturPortal.Tests.MultiTenancy;

public class QueryFilterTests
{
    [Test]
    public async Task Client_QueryFilter_ReturnOnlyCurrentSite()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(siteId: 1);

        var clients = await db.Clients.ToListAsync();

        // Site A has clients 10 and 20
        await Assert.That(clients).Count().IsEqualTo(2);
        await Assert.That(clients.Select(c => c.SiteId).Distinct()).Count().IsEqualTo(1);
        await Assert.That(clients.First().SiteId).IsEqualTo(1);
    }

    [Test]
    public async Task Client_QueryFilter_SiteB_ReturnsOnlyClientZ()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(siteId: 2);

        var clients = await db.Clients.ToListAsync();

        await Assert.That(clients).Count().IsEqualTo(1);
        await Assert.That(clients.First().ClientId).IsEqualTo(30);
    }

    [Test]
    public async Task Eractivity_QueryFilter_ReturnOnlyCurrentClient()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(siteId: 1, clientId: 10);

        var activities = await db.Eractivities.ToListAsync();

        // Client 10 has activities 1 and 2
        await Assert.That(activities).Count().IsEqualTo(2);
        await Assert.That(activities.All(a => a.ClientId == 10)).IsTrue();
    }

    [Test]
    public async Task Ercandidate_QueryFilter_ScopedThroughEractivity()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(siteId: 1, clientId: 10);

        var candidates = await db.Ercandidates.ToListAsync();

        // Client 10's activities (1,2) have candidates Alice and Bob
        await Assert.That(candidates).Count().IsEqualTo(2);
        await Assert.That(candidates.Select(c => c.FirstName).OrderBy(n => n))
            .IsEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Test]
    public async Task AspnetRole_QueryFilter_IncludesSiteWideRoles()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(siteId: 1, clientId: 10);

        var roles = await db.AspnetRoles.ToListAsync();

        // Should include: SiteA_ClientX_Role (siteId=1, clientId=10) + SiteA_SiteWide_Role (siteId=1, clientId=null)
        // Should exclude: SiteB_ClientZ_Role (siteId=2), SiteA_Inactive (siteId=1, clientId=10 but not filtered by IsActive here)
        var roleNames = roles.Select(r => r.RoleName).ToList();
        await Assert.That(roleNames).Contains("SiteA_ClientX_Role");
        await Assert.That(roleNames).Contains("SiteA_SiteWide_Role");
        await Assert.That(roleNames).DoesNotContain("SiteB_ClientZ_Role");
    }

    [Test]
    public async Task QueryFilter_NullTenant_ReturnsAll()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext(); // no tenant = no filtering

        var clients = await db.Clients.ToListAsync();
        await Assert.That(clients).Count().IsEqualTo(3);

        var activities = await db.Eractivities.ToListAsync();
        await Assert.That(activities).Count().IsEqualTo(4);
    }
}
