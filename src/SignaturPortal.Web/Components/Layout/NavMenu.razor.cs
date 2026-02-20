using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Components.Services;
using SignaturPortal.Web.Components.Shared;

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

    [Inject]
    private ICurrentUserService CurrentUserService { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    private NavMenuConfig _config = new();
    private bool _isInternal;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        _themeState.OnChange += OnThemeStateChanged;
        UpdateNavConfig();

        var currentUser = await CurrentUserService.GetCurrentUserAsync();
        _isInternal = currentUser?.IsInternal ?? false;
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

    private async Task OpenProfileDialogAsync()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
        };
        await DialogService.ShowAsync<UserProfileDialog>(
            Localization.GetText("Profile"), options);
    }

    private void ToggleDarkMode()
    {
        _themeState.Toggle();
    }

    private void NavigateToImpersonate()
    {
        Navigation.NavigateTo("/User/Impersonate.aspx", forceLoad: true);
    }

    private void NavigateToLogout()
    {
        Navigation.NavigateTo("/auth/logout", forceLoad: true);
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
