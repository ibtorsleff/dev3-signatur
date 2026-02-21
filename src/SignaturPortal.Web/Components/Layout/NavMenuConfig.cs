using SignaturPortal.Application.Enums;

namespace SignaturPortal.Web.Components.Layout;

public class NavMenuItem
{
    public string Label { get; set; } = "";
    public string? LabelKey { get; set; }
    public string Url { get; set; } = "";
    public string? IconClass { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>Hidden for external/client users; only shown to internal staff.</summary>
    public bool RequiresInternal { get; set; }

    /// <summary>Marks a portal-switcher item (right-side). Used by Phase 2 overflow to keep these grouped separately in the "More" dropdown.</summary>
    public bool IsPortalSwitcher { get; set; }

    /// <summary>Hidden unless the user has recruitment draft access.</summary>
    public bool RequiresDraftAccess { get; set; }

    /// <summary>Hidden unless the user has recruitment admin access (matches legacy UserCanAccessRecruitmentAdmin).</summary>
    public bool RequiresAdminAccess { get; set; }

    /// <summary>Hidden unless the user has at least one statistics permission (recruitment, questionnaire, or media).</summary>
    public bool RequiresStatisticsAccess { get; set; }

    /// <summary>
    /// Controls the order in which items are moved into the overflow "More" menu
    /// when Row 1 is too narrow to show all items. Lower value = drops sooner.
    /// Items with equal priority drop in reverse visual order (rightmost first).
    /// Default is 10.
    /// </summary>
    public int OverflowPriority { get; set; } = 10;
}

public class NavMenuConfig
{
    public PortalType PortalType { get; set; } = PortalType.Recruiting;
    public string PortalName { get; set; } = "";
    public string? PortalNameKey { get; set; }
    public string PortalUrl { get; set; } = "/";
    public List<NavMenuItem> Row1Items { get; set; } = [];
    public List<NavMenuItem> Row1RightItems { get; set; } = [];
    public List<NavMenuItem> Row2Items { get; set; } = [];
    public List<NavMenuItem> Row3Items { get; set; } = [];
    public string ThemeCssClass { get; set; } = "theme-recruitingportal";
}
