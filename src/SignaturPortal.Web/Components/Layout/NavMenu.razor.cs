using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Components.Services;

namespace SignaturPortal.Web.Components.Layout;

public partial class NavMenu : IDisposable
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private INavigationConfigService NavConfigService { get; set; } = default!;

    [Inject]
    private IUserSessionContext Session { get; set; } = default!;

    [Inject]
    private ILocalizationService Localization { get; set; } = default!;

    [Inject]
    private ThemeStateService _themeState { get; set; } = default!;

    private NavMenuConfig _config = new();

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        _themeState.OnChange += OnThemeStateChanged;
        UpdateNavConfig();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateNavConfig();
        InvokeAsync(StateHasChanged);
    }

    private void OnThemeStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void UpdateNavConfig()
    {
        var uri = new Uri(Navigation.Uri);
        var path = uri.AbsolutePath;
        _config = NavConfigService.GetConfigForRoute(path);
    }

    private void ToggleDarkMode()
    {
        _themeState.Toggle();
    }

    private string ResolveLabel(NavMenuItem item)
    {
        if (!string.IsNullOrEmpty(item.LabelKey))
        {
            return Localization.GetText(item.LabelKey);
        }
        return item.Label;
    }

    private string ResolvePortalName()
    {
        if (!string.IsNullOrEmpty(_config.PortalNameKey))
        {
            return Localization.GetText(_config.PortalNameKey);
        }
        return _config.PortalName;
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _themeState.OnChange -= OnThemeStateChanged;
    }
}
