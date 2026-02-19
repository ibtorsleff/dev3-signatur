using SignaturPortal.Application.Enums;
using SignaturPortal.Web.Components.Themes;

namespace SignaturPortal.Web.Components.Services;

public class ThemeService : IThemeService
{
    private static readonly Dictionary<PortalType, PortalThemeConfig> Themes = new()
    {
        [PortalType.Recruiting] = new(RecruitingPortalTheme.Create(), RecruitingPortalTheme.CssClass),
        [PortalType.AdPortal]   = new(AdPortalTheme.Create(), AdPortalTheme.CssClass),
        [PortalType.Onboarding] = new(OnboardingPortalTheme.Create(), OnboardingPortalTheme.CssClass),
    };

    public PortalThemeConfig GetTheme(PortalType portal) =>
        Themes.GetValueOrDefault(portal, Themes[PortalType.Recruiting]);
}
