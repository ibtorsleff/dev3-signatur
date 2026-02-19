using SignaturPortal.Application.Enums;

namespace SignaturPortal.Web.Components.Layout;

public class NavMenuItem
{
    public string Label { get; set; } = "";
    public string? LabelKey { get; set; }
    public string Url { get; set; } = "";
    public string? IconClass { get; set; }
    public bool IsSelected { get; set; }
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
