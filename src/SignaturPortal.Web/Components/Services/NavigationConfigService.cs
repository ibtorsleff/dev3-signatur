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
            PortalName = portal switch
            {
                PortalType.AdPortal   => "Annonceportal",
                PortalType.Onboarding => "Medarbejderportal",
                _                     => "Rekruttering",
            },
            PortalNameKey = portal switch
            {
                PortalType.AdPortal   => "AdPortal",
                PortalType.Onboarding => "OBEmployeePortal",
                _                     => "ERecruitmentPortal",
            },
            PortalUrl = portal switch
            {
                PortalType.AdPortal   => "/Responsive/AdPortal/ActivityList.aspx",
                PortalType.Onboarding => "/Responsive/OnBoarding/Default.aspx",
                _                     => "/recruiting/activities",
            },
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
            new() { LabelKey = "Help", Label = "Hj\u00e6lp", Url = "/Responsive/Recruiting/Help.aspx", IconClass = "icon-help", OverflowPriority = 1 },
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
        if (path.StartsWith("/recruiting", StringComparison.OrdinalIgnoreCase))
            return GetRecruitingRow2Items(path);

        if (path.StartsWith("/adportal", StringComparison.OrdinalIgnoreCase))
            return GetAdPortalRow2Items(path);

        if (path.StartsWith("/onboarding", StringComparison.OrdinalIgnoreCase))
            return GetOnboardingRow2Items(path);

        return [];
    }

    private static List<NavMenuItem> GetRecruitingRow2Items(string path)
    {
        var normalizedPath = path.TrimEnd('/');

        // Activity detail/edit: /recruiting/activities/{numeric-id}[/...]
        // Switch to activity-context tabs instead of the list-level Draft/Ongoing/Closed tabs.
        var activityId = ExtractActivityId(normalizedPath);
        if (activityId != null)
            return GetActivityDetailRow2Items(activityId, normalizedPath);

        var isDraft = normalizedPath.Equals("/recruiting/activities/draft", StringComparison.OrdinalIgnoreCase);
        var isClosed = normalizedPath.Equals("/recruiting/activities/closed", StringComparison.OrdinalIgnoreCase);
        var isOngoing = !isDraft && !isClosed;

        return
        [
            new() { LabelKey = "ERecruitmentDraftActivities", Label = "Kladdesager", Url = "/recruiting/activities/draft", IsSelected = isDraft, RequiresDraftAccess = true },
            new() { LabelKey = "ERecruitmentOngoingActivities", Label = "Igangv\u00e6rende sager", Url = "/recruiting/activities", IsSelected = isOngoing },
            new() { LabelKey = "ERecruitmentClosedActivities", Label = "Afsluttede sager", Url = "/recruiting/activities/closed", IsSelected = isClosed },
        ];
    }

    /// <summary>
    /// Extracts the numeric activity ID from a path like /recruiting/activities/120581.
    /// Returns null for list paths (/recruiting/activities, /recruiting/activities/draft, etc.).
    /// </summary>
    private static string? ExtractActivityId(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3
            && segments[0].Equals("recruiting", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("activities", StringComparison.OrdinalIgnoreCase)
            && segments[2].All(char.IsDigit))
        {
            return segments[2];
        }
        return null;
    }

    /// <summary>
    /// Row 2 tabs for an individual activity (master data, advertisement, notes, candidates).
    /// Uses the same localization keys as the legacy RecruitingTabs control.
    /// Non-Blazor tabs fall through to the legacy app via YARP.
    /// </summary>
    private static List<NavMenuItem> GetActivityDetailRow2Items(string activityId, string normalizedPath)
    {
        // Activity tab is selected for both the detail view and the edit view (master data section).
        var isActivityTab = normalizedPath.Equals($"/recruiting/activities/{activityId}", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Equals($"/recruiting/activities/{activityId}/edit", StringComparison.OrdinalIgnoreCase);

        return
        [
            new() { LabelKey = "Activity",       Label = "Sag",       Url = $"/recruiting/activities/{activityId}",                              IsSelected = isActivityTab },
            new() { LabelKey = "CandidateList",  Label = "Kandidater", Url = $"/Responsive/Recruiting/CandidateList.aspx?ErId={activityId}" },
            new() { LabelKey = "Advertisement",  Label = "Annonce",    Url = $"/Responsive/Recruiting/ActivityCreateEditAd.aspx?ErId={activityId}" },
            new() { LabelKey = "ActivityNotes",  Label = "Sagsnoter",  Url = $"/Responsive/Recruiting/ActivityNotes.aspx?ErId={activityId}" },
        ];
    }

    private static List<NavMenuItem> GetAdPortalRow2Items(string path)
    {
        var normalizedPath = path.TrimEnd('/');

        // Future Blazor routes will refine this matching. For now the activity list
        // is selected whenever we are anywhere under /adportal.
        var isMediaStatistics = normalizedPath.Equals("/adportal/media-statistics", StringComparison.OrdinalIgnoreCase);
        var isActivities = !isMediaStatistics;

        return
        [
            new() { LabelKey = "Activities", Label = "Aktiviteter", Url = "/Responsive/AdPortal/ActivityList.aspx", IsSelected = isActivities },
            new() { LabelKey = "MediaStatistics", Label = "Mediestatistik", Url = "/Responsive/AdPortal/MediaStatisticsActivityList.aspx", IsSelected = isMediaStatistics, RequiresAdPortalMediaStatisticsAccess = true },
        ];
    }

    private static List<NavMenuItem> GetOnboardingRow2Items(string path)
    {
        var normalizedPath = path.TrimEnd('/');

        // Future Blazor routes will refine this matching.
        var isTasks = normalizedPath.Equals("/onboarding/tasks", StringComparison.OrdinalIgnoreCase);
        var isTemplates = normalizedPath.Equals("/onboarding/templates", StringComparison.OrdinalIgnoreCase);
        var isLetterTemplates = normalizedPath.Equals("/onboarding/letter-templates", StringComparison.OrdinalIgnoreCase);
        var isUsers = normalizedPath.Equals("/onboarding/users", StringComparison.OrdinalIgnoreCase);
        var isQuestionnaires = normalizedPath.Equals("/onboarding/questionnaires", StringComparison.OrdinalIgnoreCase);
        // Employees is the default tab for any /onboarding/* path that isn't one of the above.
        var isEmployees = !isTasks && !isTemplates && !isLetterTemplates && !isUsers && !isQuestionnaires;

        return
        [
            new() { LabelKey = "OBEmployees", Label = "Medarbejdere", Url = "/Responsive/Onboarding/ProcessList.aspx", IsSelected = isEmployees },
            new() { LabelKey = "OBTasks", Label = "Opgaver", Url = "/Responsive/Onboarding/ProcessTaskList.aspx", IsSelected = isTasks },
            new() { LabelKey = "OBTemplates", Label = "Skabeloner", Url = "/Responsive/Onboarding/TemplateList.aspx", IsSelected = isTemplates, RequiresOnboardingTemplatesAccess = true },
            new() { LabelKey = "OBLetterTemplates", Label = "Brevskabeloner", Url = "/Responsive/Onboarding/LetterTemplateList.aspx", IsSelected = isLetterTemplates, RequiresOnboardingLetterTemplatesAccess = true },
            new() { LabelKey = "Users", Label = "Brugere", Url = "/Responsive/Onboarding/UserList.aspx", IsSelected = isUsers, RequiresOnboardingUsersAccess = true },
            new() { LabelKey = "QNQuestionnaires", Label = "Sp\u00f8rgeskemaer", Url = "/Responsive/Onboarding/QuestionnaireList.aspx", IsSelected = isQuestionnaires, RequiresOnboardingQuestionnairesAccess = true },
        ];
    }

    private static List<NavMenuItem> GetRow3Items(string path, PortalType portal)
    {
        // Row 3 is not used for the activity list area.
        // Future phases will add Row 3 items for candidate detail pages.
        return [];
    }
}
