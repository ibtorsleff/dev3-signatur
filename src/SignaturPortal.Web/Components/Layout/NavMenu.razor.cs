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
    private IPermissionHelper PermissionHelper { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    private NavMenuConfig _config = new();
    private bool _isInternal;
    private bool _canAccessDraftActivities;
    private bool _canAccessRecruitmentAdmin;
    private bool _canAccessRecruitmentStatistics;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        _themeState.OnChange += OnThemeStateChanged;

        // Use Session.UserId (not the auth principal) so that impersonation is respected:
        // GetCurrentUserAsync() reads the auth cookie which stays as the real admin during impersonation,
        // while Session reflects the effective user. IPermissionHelper uses session context for the same reason.
        if (Session.UserId.HasValue)
        {
            var sessionUser = await CurrentUserService.GetUserByIdAsync(Session.UserId.Value);
            _isInternal = sessionUser?.IsInternal ?? false;
        }
        _canAccessDraftActivities = await PermissionHelper.UserCanAccessRecruitmentDraftActivitiesAsync();
        _canAccessRecruitmentAdmin = await PermissionHelper.UserCanAccessRecruitmentAdminAsync();
        _canAccessRecruitmentStatistics = await PermissionHelper.UserCanAccessRecruitmentStatisticsAsync();

        UpdateNavConfig();
        ApplyUserVisibility();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // User context is already loaded — just rebuild config and re-filter.
        UpdateNavConfig();
        ApplyUserVisibility();
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

    /// <summary>
    /// Removes items the current user is not allowed to see.
    /// Called after UpdateNavConfig, once user context is known.
    /// </summary>
    private void ApplyUserVisibility()
    {
        _config.Row1Items = _config.Row1Items
            .Where(item => (!item.RequiresInternal || _isInternal)
                        && (!item.RequiresAdminAccess || _canAccessRecruitmentAdmin)
                        && (!item.RequiresStatisticsAccess || _canAccessRecruitmentStatistics))
            .ToList();

        _config.Row2Items = _config.Row2Items
            .Where(item => !item.RequiresDraftAccess || _canAccessDraftActivities)
            .ToList();
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

    private async Task OpenImpersonateDialogAsync()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = false,
        };
        await DialogService.ShowAsync<ImpersonateDialog>(
            Localization.GetText("Impersonate"), options);
    }

    private void OnImpersonateToggled(bool newValue)
    {
        if (!newValue)
        {
            var returnPath = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
            Navigation.NavigateTo(
                $"/Default.aspx?StopImpersonate=1&ReturnUrl={Uri.EscapeDataString(returnPath)}",
                forceLoad: true);
        }
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
