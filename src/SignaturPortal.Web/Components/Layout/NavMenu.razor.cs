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

    private NavMenuConfig _config = new();

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        UpdateNavConfig();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateNavConfig();
        InvokeAsync(StateHasChanged);
    }

    private void UpdateNavConfig()
    {
        var uri = new Uri(Navigation.Uri);
        var path = uri.AbsolutePath;
        _config = NavConfigService.GetConfigForRoute(path);
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
