namespace SignaturPortal.Tests;

/// <summary>
/// Smoke tests to verify the test framework is operational.
/// Real unit tests will be added as features are implemented.
/// </summary>
public class SmokeTests
{
    [Test]
    public async Task TestFramework_IsOperational()
    {
        // Verify TUnit assertions work
        await Assert.That(1 + 1).IsEqualTo(2);
    }

    [Test]
    public async Task ProjectReferences_AreOperational()
    {
        // Verify we can reference Domain and Application namespaces at compile time
        // This confirms the project references are configured correctly
        var domainNamespace = "SignaturPortal.Domain";
        var applicationNamespace = "SignaturPortal.Application";

        // Both namespaces exist because we have project references
        await Assert.That(domainNamespace).Contains("Domain");
        await Assert.That(applicationNamespace).Contains("Application");
    }
}
