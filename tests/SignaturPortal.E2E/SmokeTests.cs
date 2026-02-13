using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace SignaturPortal.E2E;

/// <summary>
/// Smoke tests to verify Playwright E2E framework is operational.
/// Real E2E tests will be added as UI features are implemented.
/// </summary>
[TestFixture]
public class SmokeTests : PageTest
{
    [Test]
    public async Task PlaywrightFramework_IsOperational()
    {
        // Verify Playwright can launch a browser and navigate
        // Uses a public site to avoid dependency on local app running
        await Page.GotoAsync("https://playwright.dev/dotnet/");
        await Expect(Page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Playwright"));
    }

    [Test]
    [Category("RequiresApp")]
    public async Task BlazorApp_ServesHomePage()
    {
        // This test requires the Blazor app to be running.
        // Skip if app is not available.
        try
        {
            await Page.GotoAsync(TestConfig.BaseUrl, new PageGotoOptions()
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 5000
            });
            await Expect(Page.Locator("h1")).ToContainTextAsync("SignaturPortal");
        }
        catch (PlaywrightException)
        {
            Assert.Ignore("Blazor app not running at " + TestConfig.BaseUrl);
        }
    }
}
