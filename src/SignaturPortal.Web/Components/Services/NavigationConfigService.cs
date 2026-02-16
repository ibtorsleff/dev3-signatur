using SignaturPortal.Web.Components.Layout;

namespace SignaturPortal.Web.Components.Services;

/// <summary>
/// Route-aware navigation configuration service.
/// Determines which Row 1 item is active, which Row 2 tabs to show,
/// and which Row 3 sub-tabs to display based on the current URL path.
/// Stateless — registered as singleton.
/// </summary>
public class NavigationConfigService : INavigationConfigService
{
    private enum NavigationArea
    {
        Default,
        Activities
    }

    public NavMenuConfig GetConfigForRoute(string path)
    {
        var area = ResolveArea(path);

        return new NavMenuConfig
        {
            PortalName = "Rekruttering",
            PortalUrl = "/activities",
            ThemeCssClass = "theme-recruiting",
            Row1Items = GetRow1Items(area),
            Row1RightItems = GetRow1RightItems(),
            Row2Items = GetRow2Items(path, area),
            Row3Items = GetRow3Items(path, area)
        };
    }

    private static NavigationArea ResolveArea(string path)
    {
        if (path.StartsWith("/activities", StringComparison.OrdinalIgnoreCase))
            return NavigationArea.Activities;

        return NavigationArea.Default;
    }

    private static List<NavMenuItem> GetRow1Items(NavigationArea area)
    {
        return
        [
            new() { Label = "Sagsliste", Url = "/activities", IconClass = "icon-activity-list", IsSelected = area == NavigationArea.Activities },
            new() { Label = "S\u00f8g", Url = "/Responsive/Recruiting/Search.aspx", IconClass = "icon-search" },
            new() { Label = "Jobbank", Url = "/Responsive/Recruiting/JobBank.aspx", IconClass = "icon-jobbank" },
            new() { Label = "Hj\u00e6lp", Url = "/Responsive/Recruiting/Help.aspx", IconClass = "icon-help" },
            new() { Label = "Admin", Url = "/Responsive/Recruiting/Admin.aspx", IconClass = "icon-admin" },
            new() { Label = "Statistik", Url = "/Responsive/Recruiting/StatisticsQuestionnaire.aspx", IconClass = "icon-statistics" },
        ];
    }

    private static List<NavMenuItem> GetRow1RightItems()
    {
        return
        [
            new() { Label = "Medarbejderportal", Url = "/Responsive/OnBoarding/Default.aspx", IconClass = "icon-employee-portal" },
            new() { Label = "Annonceportal", Url = "/Responsive/AdPortal/ActivityList.aspx", IconClass = "icon-ad-portal" },
        ];
    }

    private static List<NavMenuItem> GetRow2Items(string path, NavigationArea area)
    {
        if (area != NavigationArea.Activities)
            return [];

        // Normalize: remove trailing slash for consistent matching
        var normalizedPath = path.TrimEnd('/');

        var isDraft = normalizedPath.Equals("/activities/draft", StringComparison.OrdinalIgnoreCase);
        var isClosed = normalizedPath.Equals("/activities/closed", StringComparison.OrdinalIgnoreCase);
        // "Ongoing" is the default for /activities and any sub-path that isn't draft or closed
        // This includes detail pages like /activities/123
        var isOngoing = !isDraft && !isClosed;

        return
        [
            new() { Label = "Kladdesager", Url = "/activities/draft", IsSelected = isDraft },
            new() { Label = "Igangv\u00e6rende sager", Url = "/activities", IsSelected = isOngoing },
            new() { Label = "Afsluttede sager", Url = "/activities/closed", IsSelected = isClosed },
        ];
    }

    private static List<NavMenuItem> GetRow3Items(string path, NavigationArea area)
    {
        // Row 3 is not used for the activity list area.
        // Future phases will add Row 3 items for candidate detail pages.
        return [];
    }
}
