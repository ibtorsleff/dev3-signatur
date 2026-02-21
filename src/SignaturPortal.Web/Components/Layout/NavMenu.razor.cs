using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Components.Services;
using SignaturPortal.Web.Components.Shared;

namespace SignaturPortal.Web.Components.Layout;

public partial class NavMenu : IAsyncDisposable
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

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private NavMenuConfig _config = new();
    private bool _isInternal;
    private bool _canAccessDraftActivities;
    private bool _canAccessRecruitmentAdmin;
    private bool _canAccessRecruitmentStatistics;

    // --- Row 1 layout state ---
    // Estimated widths of the "More" and "Portals" dropdown buttons.
    // Both match the Row 1 item style: min-width 77px + padding + margin ≈ 85px.
    private const double MoreButtonWidth = 85.0;
    private const double PortalsButtonWidth = 85.0;

    // Index of the first Row1Item that does not fit. int.MaxValue = all items fit.
    private int _overflowStartIndex = int.MaxValue;
    // True when portal items are too wide to show individually and have been
    // collapsed into the "Portals" dropdown button.
    private bool _portalsCollapsed;
    // Cached natural widths of Row1Items and Row1RightItems — measured once
    // per route when all items are visible.
    private double[] _itemWidths = [];
    private double[] _portalWidths = [];
    // Total space available for left items + portal items (excluding logo and
    // Row 1 padding). Derived as: leftUlWidth + currentRightWidth.
    // This value is constant for any given viewport width regardless of whether
    // portals are expanded or collapsed.
    private double _availWidth;

    // Set to true when a route change requires re-measuring item widths.
    private bool _itemsNeedRemeasure;
    private DotNetObjectReference<NavMenu>? _dotNetRef;
    private IJSObjectReference? _navOverflowModule;

    private bool HasOverflow => _overflowStartIndex < _config.Row1Items.Count;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _navOverflowModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/navOverflow.js");
            await _navOverflowModule.InvokeVoidAsync("init", _dotNetRef, "nav-row1-left");
        }
        else if (_itemsNeedRemeasure)
        {
            _itemsNeedRemeasure = false;
            if (_navOverflowModule != null)
            {
                await _navOverflowModule.InvokeVoidAsync("remeasure", "nav-row1-left");
            }
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // User context is already loaded — just rebuild config and re-filter.
        UpdateNavConfig();
        ApplyUserVisibility();
        // Reset overflow state so all items are visible for re-measurement.
        _overflowStartIndex = int.MaxValue;
        _portalsCollapsed = false;
        _itemsNeedRemeasure = true;
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

    /// <summary>
    /// Called by the JS module with the left UL width, left item natural widths,
    /// and portal item natural widths (only available when portals are expanded).
    /// Stores widths, derives _availWidth, then recalculates layout.
    /// </summary>
    [JSInvokable]
    public async Task OnNavMeasured(double leftUlWidth, double[] itemWidths, double[] portalWidths)
    {
        _itemWidths = itemWidths;
        // Only update the portal width cache when the individual portal items are
        // visible in the DOM (portalWidths is empty when portals are collapsed).
        if (portalWidths.Length > 0)
            _portalWidths = portalWidths;

        // Derive the total available width from the left UL width and whatever
        // the right side is currently occupying.
        var currentRightWidth = _portalsCollapsed ? PortalsButtonWidth : _portalWidths.Sum();
        _availWidth = leftUlWidth + currentRightWidth;

        CalculateLayout();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called by the ResizeObserver whenever the left menu UL changes width.
    /// Recalculates layout using cached item widths.
    /// </summary>
    [JSInvokable]
    public async Task OnContainerResized(double newLeftUlWidth)
    {
        // Derive _availWidth the same way as in OnNavMeasured.
        var currentRightWidth = _portalsCollapsed ? PortalsButtonWidth : _portalWidths.Sum();
        _availWidth = newLeftUlWidth + currentRightWidth;

        CalculateLayout();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Determines _portalsCollapsed and _overflowStartIndex from _availWidth
    /// and the cached item widths.
    ///
    /// Three cases (in priority order):
    ///   1. Everything fits  → portals expanded, no More button
    ///   2. Portals collapse → portals button shown, all left items fit
    ///   3. Portals collapse + left items overflow → both More and Portals buttons shown
    /// </summary>
    private void CalculateLayout()
    {
        if (_itemWidths.Length == 0)
        {
            _portalsCollapsed = false;
            _overflowStartIndex = int.MaxValue;
            return;
        }

        var leftTotal = _itemWidths.Sum();
        var portalTotal = _portalWidths.Length > 0 ? _portalWidths.Sum() : 0.0;
        var hasPortals = _config.Row1RightItems.Count > 0;

        // Case 1: Everything fits — show all items and portal buttons individually.
        if (leftTotal + portalTotal <= _availWidth)
        {
            _portalsCollapsed = false;
            _overflowStartIndex = int.MaxValue;
            return;
        }

        // Case 2: Collapsing portals to a single button frees enough space for all left items.
        if (hasPortals && leftTotal + PortalsButtonWidth <= _availWidth)
        {
            _portalsCollapsed = true;
            _overflowStartIndex = int.MaxValue;
            return;
        }

        // Case 3: Even with portals collapsed, left items need to overflow.
        // Both the More button and the Portals button occupy the right side.
        _portalsCollapsed = hasPortals;
        var rightButtonsWidth = (hasPortals ? PortalsButtonWidth : 0.0) + MoreButtonWidth;

        double running = 0;
        for (var i = 0; i < _itemWidths.Length; i++)
        {
            running += _itemWidths[i];
            if (running + rightButtonsWidth > _availWidth)
            {
                _overflowStartIndex = i;
                return;
            }
        }

        _overflowStartIndex = int.MaxValue;
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

    public async ValueTask DisposeAsync()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _themeState.OnChange -= OnThemeStateChanged;
        _dotNetRef?.Dispose();

        if (_navOverflowModule != null)
        {
            try
            {
                await _navOverflowModule.InvokeVoidAsync("dispose");
                await _navOverflowModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            catch (TaskCanceledException) { }
        }
    }
}
