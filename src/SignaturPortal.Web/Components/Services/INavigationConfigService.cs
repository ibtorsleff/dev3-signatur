using SignaturPortal.Web.Components.Layout;

namespace SignaturPortal.Web.Components.Services;

/// <summary>
/// Resolves navigation configuration based on the current route path.
/// Maps routes to the appropriate Row 1 active state, Row 2 tabs, and Row 3 sub-tabs.
/// </summary>
public interface INavigationConfigService
{
    /// <summary>
    /// Returns a fully populated NavMenuConfig for the given absolute URL path.
    /// </summary>
    /// <param name="path">Absolute path portion of the URL (e.g., "/recruiting/activities", "/recruiting/activities/closed").</param>
    NavMenuConfig GetConfigForRoute(string path);
}
