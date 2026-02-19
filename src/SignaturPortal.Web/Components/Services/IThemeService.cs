using MudBlazor;
using SignaturPortal.Application.Enums;

namespace SignaturPortal.Web.Components.Services;

public record PortalThemeConfig(MudTheme MudTheme, string CssClass);

public interface IThemeService
{
    PortalThemeConfig GetTheme(PortalType portal);
}
