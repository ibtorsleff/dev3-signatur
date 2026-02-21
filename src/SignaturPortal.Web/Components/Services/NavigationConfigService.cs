using SignaturPortal.Application.Enums;
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
    public NavMenuConfig GetConfigForRoute(string path)
    {
        var portal = ResolvePortal(path);

        return new NavMenuConfig
        {
            PortalType = portal,
            PortalName = "Rekruttering",
            PortalNameKey = "ERecruitmentPortal",
            PortalUrl = "/recruiting/activities",
            ThemeCssClass = portal switch
            {
                PortalType.AdPortal   => "theme-adportal",
                PortalType.Onboarding => "theme-onboarding",
                _                     => "theme-recruitingportal",
            },
            Row1Items = GetRow1Items(path),
            Row1RightItems = GetRow1RightItems(),
            Row2Items = GetRow2Items(path),
            Row3Items = GetRow3Items(path, portal)
        };
    }

    private static PortalType ResolvePortal(string path)
    {
        var segment = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                          .FirstOrDefault() ?? "";
        return segment.ToLowerInvariant() switch
        {
            "recruiting"  => PortalType.Recruiting,
            "adportal"    => PortalType.AdPortal,
            "onboarding"  => PortalType.Onboarding,
            _             => PortalType.Recruiting,
        };
    }

    private static List<NavMenuItem> GetRow1Items(string path)
    {
        var isRecruiting = path.StartsWith("/recruiting", StringComparison.OrdinalIgnoreCase);
        return
        [
            new() { LabelKey = "ActivityList", Label = "Sagsliste", Url = "/recruiting/activities", IconClass = "icon-activity-list", IsSelected = isRecruiting },
            new() { LabelKey = "Search", Label = "S\u00f8g", Url = "/Responsive/Recruiting/Search.aspx", IconClass = "icon-search" },
            new() { LabelKey = "JobBank", Label = "Jobbank", Url = "/Responsive/Recruiting/JobBank.aspx", IconClass = "icon-jobbank" },
            new() { LabelKey = "Help", Label = "Hj\u00e6lp", Url = "/Responsive/Recruiting/Help.aspx", IconClass = "icon-help" },
            new() { LabelKey = "Admin", Label = "Admin", Url = "/Responsive/Recruiting/Admin.aspx", IconClass = "icon-admin", RequiresInternal = true, RequiresAdminAccess = true },
            new() { LabelKey = "Statistics", Label = "Statistik", Url = "/Responsive/Recruiting/StatisticsQuestionnaire.aspx", IconClass = "icon-statistics", RequiresInternal = true, RequiresStatisticsAccess = true },
        ];
    }

    private static List<NavMenuItem> GetRow1RightItems()
    {
        return
        [
            new() { LabelKey = "OBEmployeePortal", Label = "Medarbejderportal", Url = "/Responsive/OnBoarding/Default.aspx", IconClass = "icon-employee-portal", IsPortalSwitcher = true },
            new() { LabelKey = "AdPortal", Label = "Annonceportal", Url = "/Responsive/AdPortal/ActivityList.aspx", IconClass = "icon-ad-portal", IsPortalSwitcher = true },
        ];
    }

    private static List<NavMenuItem> GetRow2Items(string path)
    {
        if (!path.StartsWith("/recruiting", StringComparison.OrdinalIgnoreCase))
            return [];

        // Normalize: remove trailing slash for consistent matching
        var normalizedPath = path.TrimEnd('/');

        var isDraft = normalizedPath.Equals("/recruiting/activities/draft", StringComparison.OrdinalIgnoreCase);
        var isClosed = normalizedPath.Equals("/recruiting/activities/closed", StringComparison.OrdinalIgnoreCase);
        // "Ongoing" is the default for /recruiting/activities and any sub-path that isn't draft or closed
        // This includes detail pages like /recruiting/activities/123
        var isOngoing = !isDraft && !isClosed;

        return
        [
            new() { LabelKey = "ERecruitmentDraftActivities", Label = "Kladdesager", Url = "/recruiting/activities/draft", IsSelected = isDraft, RequiresDraftAccess = true },
            new() { LabelKey = "ERecruitmentOngoingActivities", Label = "Igangv\u00e6rende sager", Url = "/recruiting/activities", IsSelected = isOngoing },
            new() { LabelKey = "ERecruitmentClosedActivities", Label = "Afsluttede sager", Url = "/recruiting/activities/closed", IsSelected = isClosed },
        ];
    }

    private static List<NavMenuItem> GetRow3Items(string path, PortalType portal)
    {
        // Row 3 is not used for the activity list area.
        // Future phases will add Row 3 items for candidate detail pages.
        return [];
    }
}
