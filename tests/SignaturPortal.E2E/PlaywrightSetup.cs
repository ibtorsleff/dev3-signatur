using Microsoft.Playwright;

namespace SignaturPortal.E2E;

/// <summary>
/// Configuration for Playwright E2E tests.
/// Provides test environment settings.
/// </summary>
public static class TestConfig
{
    /// <summary>
    /// Base URL for the Blazor app under test.
    /// Override via environment variable BLAZOR_BASE_URL.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("BLAZOR_BASE_URL")
        ?? "https://localhost:5001";
}
