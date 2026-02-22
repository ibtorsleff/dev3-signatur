using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Enums;

namespace SignaturPortal.Web.Components.Pages.Recruiting;

public partial class ActivityList
{
    [Inject] private IErActivityService ErActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private ILocalizationService Localization { get; set; } = default!;
    [Inject] private IUserSessionContext Session { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = default!;
    [Inject] private IPermissionHelper PermHelper { get; set; } = default!;
    [Inject] private IClientService ClientService { get; set; } = default!;
    private MudMessageBox _noActivitiesDialog = default!;

    [Parameter] public string? Mode { get; set; }

    private MudDataGrid<ActivityListDto> _dataGrid = default!;
    private MudAutocomplete<ClientDropdownDto> _clientAutocomplete = default!;
    private MudAutocomplete<UserDropdownDto> _createdByAutocomplete = default!;
    private MudAutocomplete<UserDropdownDto> _recruitmentResponsibleAutocomplete = default!;
    private MudAutocomplete<ClientSectionDropdownDto> _clientSectionAutocomplete = default!;
    private MudAutocomplete<TemplateGroupDropdownDto> _templateGroupAutocomplete = default!;
    private MudAutocomplete<ClientSectionGroupDropdownDto> _clientSectionGroupAutocomplete = default!;
    private ERActivityStatus _currentStatus = ERActivityStatus.OnGoing;
    private string _headlineText = "";
    private int _totalCount;
    private bool _showFilters;
    private string _pagerInfoFormat = "{first_item}-{last_item} of {all_items}";

    // Permission state (loaded once in OnInitializedAsync)
    private bool _isClientUser;
    private bool _isInternalUser;
    private bool _canCreateActivity;
    private bool _canCreateDraftActivity;
    private bool _canAccessDraftActivities;
    private bool _clientDraftEnabled;
    private bool _userHasExportPermission;
    private bool _clientExportConfigEnabled;

    // Icon permission/config state
    private bool _canEditActivitiesNotMemberOf;
    private bool _canPublishWebAd;
    private bool _showWebAdStatus;       // IsClientUser AND client has ShowWebAdStatusInActivityList
    private bool _includeWebAdChanges;   // IsClientUser AND WebAdSendMailOnAdChanges AND _canPublishWebAd

    private bool _canExportActivityMembers =>
        _currentStatus == ERActivityStatus.OnGoing &&
        _userHasExportPermission &&
        _clientExportConfigEnabled;

    // Client dropdown state (non-client users only)
    private List<ClientDropdownDto> _clients = new();
    private ClientDropdownDto? _selectedClient;  // drives the autocomplete display
    private int? _activeClientId;                // drives the grid filter (stable during search)

    // "More" filter panel state (non-client users only)
    private bool _showMoreFilters;
    private bool _filterOptionsNeedRefresh;

    // More filter selections (DTO = drives autocomplete display; ID = drives grid filter)
    private UserDropdownDto? _createdByFilterUser;
    private Guid? _createdByFilter;
    private UserDropdownDto? _recruitmentResponsibleFilterUser;
    private Guid? _recruitmentResponsibleFilter;
    private ClientSectionDropdownDto? _clientSectionFilterSection;
    private int? _clientSectionFilter;
    private TemplateGroupDropdownDto? _templateGroupFilterItem;
    private int? _templateGroupFilter;
    private ClientSectionGroupDropdownDto? _clientSectionGroupFilterItem;
    private int? _clientSectionGroupFilter;

    // Closed-mode date range filter
    private DateTime? _closedDateFrom;
    private DateTime? _closedDateTo;

    // More filter dropdown options (populated from existing activities)
    private List<UserDropdownDto> _filterCreatedByUsers = new();
    private List<UserDropdownDto> _filterRecruitmentResponsibleUsers = new();
    private List<ClientSectionDropdownDto> _filterClientSections = new();
    private List<TemplateGroupDropdownDto> _filterTemplateGroups = new();
    private List<ClientSectionGroupDropdownDto> _filterClientSectionGroups = new();

    // Set in OnInitializedAsync when the external user's client has no recruitment portal enabled.
    // Triggers a direct force-logout (no dialog) in OnAfterRenderAsync.
    // Matches legacy ActivityList.aspx.cs:286.
    private bool _clientHasNoRecruitmentPortal;

    // Set in OnInitializedAsync when an external user has zero active activities.
    // Triggers the disclaimer dialog + force-logout in OnAfterRenderAsync.
    // Matches legacy ActivityList.aspx.cs:362 + ExternalUserNoActiveActivtiesTmr_OnTick.
    private bool _externalUserHasNoActiveActivities;

    // Flag set at the END of OnInitializedAsync to signal the grid should load.
    // OnAfterRenderAsync(firstRender: true) fires BEFORE OnInitializedAsync completes
    // for async lifecycle methods, so _activeClientId would be null at that point.
    // Setting this flag at the end of init and reloading on the next AfterRender
    // ensures the grid loads only after all client/session data is ready.
    private bool _gridNeedsLoad;

    // Whether the active client uses Fund applications (switches label to "RecruitingResponsableFund")
    private bool _clientIsFund;
    private string _recruitingResponsableKey => _clientIsFund ? "RecruitingResponsableFund" : "RecruitingResponsable";
    private string _noActivitiesDisclaimerKey => _clientIsFund
        ? "ExternalUserNotMemberOfAnyActiveRecruitmentActivitiesDisclaimerFund"
        : "ExternalUserNotMemberOfAnyActiveRecruitmentActivitiesDisclaimer";

    // Column visibility: computed from current mode
    // CreatedBy: visible in Ongoing and Closed, hidden in Draft
    private bool _hideCreatedByColumn => _currentStatus == ERActivityStatus.Draft;
    // DraftResponsible: visible in Draft only
    private bool _hideDraftResponsibleColumn => _currentStatus != ERActivityStatus.Draft;
    // Visitors: visible in Ongoing and Closed, hidden in Draft, AND only if client has WebAdVisitorStatistics enabled
    private bool _clientHasWebAdVisitorStatistics;
    private bool _hideVisitorsColumn => _currentStatus == ERActivityStatus.Draft || !_clientHasWebAdVisitorStatistics;
    // TemplateGroup: visible in Ongoing and Closed, hidden in Draft, AND only if client uses template groups
    private bool _clientUsesTemplateGroups;
    private bool _hideTemplateGroupColumn => _currentStatus == ERActivityStatus.Draft || !_clientUsesTemplateGroups;
    // DraftArea: visible in Draft only, AND area type must be configured.
    //   Type 2 (ERTemplateGroup): always shown when in Draft mode.
    //   Type 1 (ClientSection): only shown when client has section hierarchy enabled.
    //   Matches legacy ActivityList.aspx.cs:1300-1302 ShowDraftAreaColumn condition.
    private int _draftAreaTypeId;
    private bool _draftSectionHierarchyEnabled;
    private string _draftAreaHeaderKey = "ERDraftListAreaHeader";
    private string _draftResponsibleHeaderKey = "ERDraftResponsibleHeader";
    private bool _hideDraftAreaColumn =>
        _currentStatus != ERActivityStatus.Draft ||
        !((_draftAreaTypeId == 1 && _draftSectionHierarchyEnabled) || _draftAreaTypeId == 2);
    // ClientSection ("Afdeling"): hidden when user is a client user (matches legacy dskClientTh visibility)
    private bool _hideClientSectionColumn => _isClientUser;
    // Matches legacy ActivityList.aspx.cs:496-499: Draft mode uses RecruitmentPortalCreateDraftActivities,
    // other modes use RecruitmentPortalCreateActivity.
    private bool _canCreateActivityInCurrentMode =>
        _currentStatus == ERActivityStatus.Draft ? _canCreateDraftActivity : _canCreateActivity;

    // Actions (copy): visible in Ongoing and Closed, hidden in Draft, AND user must have CreateActivity permission
    private bool _hideActionsColumn => _currentStatus == ERActivityStatus.Draft || !_canCreateActivity;
    // Icon column visibility — individual icons are conditionally rendered within the actions column
    private bool _hideEmailWarningIconColumn => _isClientUser || !_canEditActivitiesNotMemberOf;
    private bool _hideWebAdStatusIconColumn => !_showWebAdStatus;
    private bool _hideWebAdChangesIconColumn => !_includeWebAdChanges;
    // Whether to show candidate count in Headline
    private bool _showCandidateCount => _currentStatus != ERActivityStatus.Draft;

    private bool _hasActiveMoreFilters =>
        _createdByFilter.HasValue ||
        _recruitmentResponsibleFilter.HasValue ||
        _clientSectionFilter.HasValue ||
        _templateGroupFilter.HasValue ||
        _clientSectionGroupFilter.HasValue;

    private string _moreButtonText => _showMoreFilters
        ? $"« {Localization.GetText("Less")}"
        : $"{Localization.GetText("More")} »";

    // Mobile list state
    private List<ActivityListDto> _mobileItems = [];
    private int _mobileCurrentPage;
    private int _mobilePageSize = 25;
    private bool _mobileDataLoaded;
    private HashSet<int> _expandedActivityIds = [];

    protected override async Task OnInitializedAsync()
    {
        _isClientUser = Session.IsClientUser;
        var currentUser = await CurrentUserService.GetCurrentUserAsync();
        _isInternalUser = currentUser?.IsInternal ?? false;
        _canCreateActivity = await PermHelper.UserCanCreateActivityAsync();
        _canCreateDraftActivity = await PermHelper.UserCanCreateDraftActivityAsync();
        _canAccessDraftActivities = await PermHelper.UserCanAccessRecruitmentDraftActivitiesAsync();
        _userHasExportPermission = await PermHelper.UserCanExportActivityMembersAsync();
        _canEditActivitiesNotMemberOf = await PermHelper.UserCanEditActivitiesNotMemberOfAsync();
        _canPublishWebAd = await PermHelper.UserCanPublishWebAdAsync();

        // Preload client-level draft flag for the session client (used in OnParametersSet Draft guard).
        // Matches legacy: _isClientLoggedOn && ClientRecruitmentDraftEnabled(siteId, clientId).
        var sessionClientId = Session.ClientId ?? 0;
        _clientDraftEnabled = sessionClientId > 0
            ? await ClientService.GetRecruitmentDraftEnabledAsync(sessionClientId)
            : true; // No client in session — client-level draft check is skipped
        _pagerInfoFormat = $"{{first_item}}-{{last_item}} {Localization.GetText("Of")} {{all_items}}";

        if (!_isClientUser)
        {
            // Internal users: client config is always enabled — only the permission check matters
            _clientExportConfigEnabled = true;

            // Derive initial status from Mode parameter (OnParametersSet runs after OnInitializedAsync).
            // Draft mode filters clients to those with RecruitmentDraftEnabled — matches legacy
            // ClientsGet with filters RecruitmentEnabled + RecruitmentDraftEnabled (ActivityList.aspx.cs:539-540).
            var initialStatus = Mode?.ToLowerInvariant() switch
            {
                "draft" => ERActivityStatus.Draft,
                "closed" => ERActivityStatus.Closed,
                _ => ERActivityStatus.OnGoing
            };
            await LoadClientsAsync(initialStatus);
        }
        else
        {
            // Client users: check whether their client has export enabled, and whether it is a Fund client
            var clientId = Session.ClientId ?? 0;
            if (clientId > 0)
            {
                _clientExportConfigEnabled = await ClientService.GetExportActivityMembersEnabledAsync(clientId);
                _clientIsFund = await ClientService.GetRecruitmentIsFundAsync(clientId);
                _showWebAdStatus = await ClientService.GetRecruitmentShowWebAdStatusInActivityListAsync(clientId);
                _includeWebAdChanges = _showWebAdStatus
                    && await ClientService.GetWebAdSendMailOnAdChangesAsync(clientId)
                    && _canPublishWebAd;
            }
        }

        if (_activeClientId.HasValue)
        {
            _clientHasWebAdVisitorStatistics = await ClientService.GetWebAdVisitorStatisticsEnabledAsync(_activeClientId.Value);
            _clientUsesTemplateGroups = await ClientService.GetRecruitmentUseTemplateGroupsAsync(_activeClientId.Value);
            _clientIsFund = await ClientService.GetRecruitmentIsFundAsync(_activeClientId.Value);
            var draftSettings = await ClientService.GetRecruitmentDraftSettingsAsync(_activeClientId.Value);
            _draftAreaTypeId = draftSettings.UserResponsabilityAreaTypeId;
            _draftSectionHierarchyEnabled = draftSettings.SectionHierarchyEnabled;
            _draftAreaHeaderKey = draftSettings.ListAreaHeaderTextId;
            _draftResponsibleHeaderKey = draftSettings.DraftResponsibleHeaderTextId;
        }

        // Guards: external user access checks (matches legacy ActivityList.aspx.cs:286 and :362).
        // Checked on first load only. Guard 1 takes precedence — if it fires, guard 2 is skipped.
        if (_isClientUser && Session.UserId.HasValue)
        {
            var clientId = Session.ClientId ?? 0;

            // Guard 1: client has no recruitment portal — force-logout immediately, no dialog.
            if (clientId > 0 && !await ClientService.GetRecruitmentEnabledAsync(clientId))
            {
                _clientHasNoRecruitmentPortal = true;
            }
            else
            {
                // Guard 2: external user has no active activities — show disclaimer then force-logout.
                var activeCount = await ErActivityService.GetUserActiveActivitiesCountAsync(Session.UserId.Value);
                _externalUserHasNoActiveActivities = activeCount == 0;
            }
        }

        // Signal that all state is ready; OnAfterRenderAsync will trigger the initial grid load.
        _gridNeedsLoad = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_gridNeedsLoad)
        {
            // _gridNeedsLoad is set at the end of OnInitializedAsync, so _activeClientId
            // is guaranteed to be populated before this fires.
            _gridNeedsLoad = false;
            await _dataGrid.ReloadServerData();

            if (!_isClientUser)
            {
                await LoadFilterOptionsAsync();
                StateHasChanged();
            }

            // Guard 1: client has no recruitment portal — log and force-logout immediately.
            if (_clientHasNoRecruitmentPortal && Session.UserId.HasValue)
            {
                await ErActivityService.LogClientNoRecruitmentPortalForceLogoutAsync(Session.UserId.Value);
                Navigation.NavigateTo("/Login.aspx", forceLoad: true);
                return;
            }

            // Guard 2: show disclaimer and log out external users with no active activities.
            // The dialog must open after the first render (grid reload ensures the DOM is ready).
            if (_externalUserHasNoActiveActivities)
                await ShowExternalUserNoActivitiesDialogAsync();
        }
        else if (_filterOptionsNeedRefresh && !_isClientUser)
        {
            _filterOptionsNeedRefresh = false;
            await LoadFilterOptionsAsync();
            StateHasChanged();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var newStatus = Mode?.ToLowerInvariant() switch
        {
            "draft" => ERActivityStatus.Draft,
            "closed" => ERActivityStatus.Closed,
            _ => ERActivityStatus.OnGoing
        };

        // Guard: Closed mode is for internal users only — redirect non-internal users to home.
        // Matches legacy: if (!_currentUser.IsInternal) ResponseRedirect("/") when Mode = Closed.
        // Uses IsInternal (not IsClientUser) because internal users can also have a ClientId.
        if (newStatus == ERActivityStatus.Closed && !_isInternalUser)
        {
            Navigation.NavigateTo("/");
            return;
        }

        // Guard: Draft mode requires all three conditions (matches legacy ActivityList.aspx.cs:385-388):
        //   1. User must be internal
        //   2. If a client is in session: that client must have RecruitmentDraftEnabled
        //   3. User must have UserCanAccessRecruitmentDraftActivities permission
        if (newStatus == ERActivityStatus.Draft &&
            (!_isInternalUser ||
             (Session.IsClientUser && !_clientDraftEnabled) ||
             !_canAccessDraftActivities))
        {
            Navigation.NavigateTo("/");
            return;
        }

        _headlineText = newStatus switch
        {
            ERActivityStatus.Draft => Localization.GetText("ERecruitmentDraftActivities"),
            ERActivityStatus.Closed => Localization.GetText("ERecruitmentClosedActivities"),
            _ => Localization.GetText("ERecruitmentOngoingActivities")
        };

        if (newStatus != _currentStatus)
        {
            // Reload client list when switching to/from Draft — the client list is filtered
            // differently in Draft mode (RecruitmentDraftEnabled required).
            if (!_isClientUser && (newStatus == ERActivityStatus.Draft || _currentStatus == ERActivityStatus.Draft))
                await LoadClientsAsync(newStatus);

            _currentStatus = newStatus;
            ResetMoreFilters();
            if (_currentStatus == ERActivityStatus.Closed)
            {
                _closedDateFrom = DateTime.Today.AddYears(-1);
                _closedDateTo = DateTime.Today;
            }
            _filterOptionsNeedRefresh = true;
            _dataGrid?.ReloadServerData();
        }
    }

    /// <summary>
    /// Loads the client dropdown list filtered by the given activity status.
    /// Draft mode: only clients with RecruitmentDraftEnabled — matches legacy ActivityList.aspx.cs:539-540.
    /// Other modes: all recruitment-enabled clients.
    /// </summary>
    private async Task LoadClientsAsync(ERActivityStatus status)
    {
        var siteId = Session.SiteId ?? 0;
        if (siteId <= 0) return;

        _clients = status == ERActivityStatus.Draft
            ? await ClientService.GetClientsForSiteWithDraftEnabledAsync(siteId)
            : await ClientService.GetClientsForSiteAsync(siteId);

        _selectedClient = _clients.Count > 0 ? _clients[0] : null;
        _activeClientId = _selectedClient?.ClientId;
    }

    /// <summary>
    /// Loads filter dropdown options from the service for the current client and status.
    /// </summary>
    private async Task LoadFilterOptionsAsync()
    {
        var options = await ErActivityService.GetActivityFilterOptionsAsync(_currentStatus, _activeClientId);
        _filterCreatedByUsers = options.CreatedByUsers;
        _filterRecruitmentResponsibleUsers = options.RecruitmentResponsibleUsers;
        _filterClientSections = options.ClientSections;
        _filterTemplateGroups = options.TemplateGroups;
        _filterClientSectionGroups = options.ClientSectionGroups;
    }

    /// <summary>
    /// Clears all More panel filter selections without reloading the grid.
    /// </summary>
    private void ResetMoreFilters()
    {
        _createdByFilterUser = null;
        _createdByFilter = null;
        _recruitmentResponsibleFilterUser = null;
        _recruitmentResponsibleFilter = null;
        _clientSectionFilterSection = null;
        _clientSectionFilter = null;
        _templateGroupFilterItem = null;
        _templateGroupFilter = null;
        _clientSectionGroupFilterItem = null;
        _clientSectionGroupFilter = null;
        _closedDateFrom = null;
        _closedDateTo = null;
    }

    /// <summary>
    /// Toggles the More filter panel open/closed.
    /// The panel auto-expands when any more filter is active (matches legacy SetupMoreToolsOpenClosed).
    /// </summary>
    private void ToggleMoreFilters()
    {
        _showMoreFilters = !_showMoreFilters;
    }

    private async Task OnCreatedByFilterChanged(UserDropdownDto? user)
    {
        _createdByFilterUser = user;
        _createdByFilter = user?.UserId;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnRecruitmentResponsibleFilterChanged(UserDropdownDto? user)
    {
        _recruitmentResponsibleFilterUser = user;
        _recruitmentResponsibleFilter = user?.UserId;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnClientSectionFilterChanged(ClientSectionDropdownDto? section)
    {
        _clientSectionFilterSection = section;
        _clientSectionFilter = section?.ClientSectionId;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnCreatedByDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _createdByAutocomplete.SelectAsync();
    }

    private async Task OnRecruitmentResponsibleDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _recruitmentResponsibleAutocomplete.SelectAsync();
    }

    private async Task OnClientSectionDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _clientSectionAutocomplete.SelectAsync();
    }

    private async Task OnTemplateGroupFilterChanged(TemplateGroupDropdownDto? group)
    {
        _templateGroupFilterItem = group;
        _templateGroupFilter = group?.TemplateGroupId;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnTemplateGroupDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _templateGroupAutocomplete.SelectAsync();
    }

    private async Task OnClientSectionGroupFilterChanged(ClientSectionGroupDropdownDto? group)
    {
        _clientSectionGroupFilterItem = group;
        _clientSectionGroupFilter = group?.ClientSectionGroupId;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnClientSectionGroupDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _clientSectionGroupAutocomplete.SelectAsync();
    }

    private Task<IEnumerable<TemplateGroupDropdownDto>> SearchTemplateGroups(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<TemplateGroupDropdownDto>>(_filterTemplateGroups);
        return Task.FromResult<IEnumerable<TemplateGroupDropdownDto>>(
            _filterTemplateGroups.Where(g => g.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    private Task<IEnumerable<ClientSectionGroupDropdownDto>> SearchClientSectionGroups(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<ClientSectionGroupDropdownDto>>(_filterClientSectionGroups);
        return Task.FromResult<IEnumerable<ClientSectionGroupDropdownDto>>(
            _filterClientSectionGroups.Where(g => g.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task OnClosedDateFromChanged(DateTime? date)
    {
        _closedDateFrom = date;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnClosedDateToChanged(DateTime? date)
    {
        _closedDateTo = date;
        await _dataGrid.ReloadServerData();
    }

    private Task<IEnumerable<UserDropdownDto>> SearchCreatedByUsers(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<UserDropdownDto>>(_filterCreatedByUsers);
        return Task.FromResult<IEnumerable<UserDropdownDto>>(
            _filterCreatedByUsers.Where(u => u.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    private Task<IEnumerable<UserDropdownDto>> SearchRecruitmentResponsibleUsers(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<UserDropdownDto>>(_filterRecruitmentResponsibleUsers);
        return Task.FromResult<IEnumerable<UserDropdownDto>>(
            _filterRecruitmentResponsibleUsers.Where(u => u.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    private Task<IEnumerable<ClientSectionDropdownDto>> SearchClientSections(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<ClientSectionDropdownDto>>(_filterClientSections);
        return Task.FromResult<IEnumerable<ClientSectionDropdownDto>>(
            _filterClientSections.Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Server-side data loading callback for MudDataGrid.
    /// Maps MudBlazor GridState to our GridRequest DTO.
    /// </summary>
    private async Task<GridData<ActivityListDto>> LoadServerData(GridState<ActivityListDto> state)
    {
        try
        {
            var request = new GridRequest
            {
                Page = state.Page,
                PageSize = state.PageSize,
                Sorts = state.SortDefinitions
                    .Select(s => new SortDefinition(s.SortBy, s.Descending))
                    .ToList()
            };

            // Map MudDataGrid filter definitions to our GridRequest filters
            // Note: MudBlazor 8.x uses FilterDefinitions property on GridState
            if (state.FilterDefinitions != null)
            {
                foreach (var filterDef in state.FilterDefinitions)
                {
                    if (filterDef.Value != null)
                    {
                        request.Filters.Add(new FilterDefinition(
                            filterDef.Column?.PropertyName ?? "",
                            filterDef.Operator ?? "contains",
                            filterDef.Value
                        ));
                    }
                }
            }

            // Non-client users must have a client selected; return empty if none available
            if (!_isClientUser && _activeClientId == null)
                return new GridData<ActivityListDto> { Items = Array.Empty<ActivityListDto>(), TotalItems = 0 };

            var clientFilter = _isClientUser ? null : _activeClientId;

            // Build More panel filters (only for non-client users when at least one is set).
            // Closed mode always includes the date range filter even when More panel filters are inactive.
            ActivityListFilterDto? moreFilters = null;
            if (!_isClientUser)
            {
                var isClosedMode = _currentStatus == ERActivityStatus.Closed;
                if (_hasActiveMoreFilters || isClosedMode)
                {
                    moreFilters = new ActivityListFilterDto
                    {
                        CreatedByUserId = _createdByFilter,
                        RecruitmentResponsibleUserId = _recruitmentResponsibleFilter,
                        ClientSectionId = _clientSectionFilter,
                        TemplateGroupId = _templateGroupFilter,
                        ClientSectionGroupId = _clientSectionGroupFilter,
                        DateFrom = isClosedMode ? _closedDateFrom : null,
                        DateTo = isClosedMode ? _closedDateTo : null
                    };
                }
            }

            var activeDraftAreaTypeId = _currentStatus == ERActivityStatus.Draft ? _draftAreaTypeId : 0;
            var response = await ErActivityService.GetActivitiesAsync(
                request, _currentStatus, clientFilter, moreFilters, activeDraftAreaTypeId,
                includeEmailWarning: !_isClientUser && _canEditActivitiesNotMemberOf,
                includeWebAdStatus: _showWebAdStatus,
                includeWebAdChanges: _includeWebAdChanges);
            _totalCount = response.TotalCount;
            var items = response.Items.ToList();
            _mobileItems = items;
            _mobileCurrentPage = state.Page;
            _mobilePageSize = state.PageSize;
            _mobileDataLoaded = true;

            return new GridData<ActivityListDto>
            {
                Items = items,
                TotalItems = response.TotalCount
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] LoadServerData failed: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[ERROR] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Snackbar.Add(Localization.GetText("ErrorLoadingActivities"), Severity.Error);
            _mobileItems = [];
            _mobileDataLoaded = true;
            return new GridData<ActivityListDto>
            {
                Items = Array.Empty<ActivityListDto>(),
                TotalItems = 0
            };
        }
    }

    /// <summary>
    /// Toggles column filter row visibility.
    /// </summary>
    private void ToggleFilters()
    {
        _showFilters = !_showFilters;
    }

    /// <summary>
    /// Navigates to the activity detail page or legacy candidate list based on configuration.
    /// </summary>
    private void NavigateToDetail(int activityId)
    {
        var useLegacyUrls = Configuration.GetValue<bool>("ERActivityListUseLegacyUrls");

        if (useLegacyUrls)
        {
            // Navigate to legacy CandidateList.aspx when ERActivityListUseLegacyUrls is enabled
            // forceLoad: true triggers a full page load so YARP can proxy to legacy app
            Navigation.NavigateTo($"/Responsive/Recruiting/CandidateList.aspx?ErId={activityId}", forceLoad: true);
        }
        else
        {
            // Navigate to Blazor activity detail page (SPA navigation)
            Navigation.NavigateTo($"/recruiting/activities/{activityId}");
        }
    }

    /// <summary>
    /// Row click handler - navigates to detail page.
    /// </summary>
    private void OnRowClick(DataGridRowClickEventArgs<ActivityListDto> args)
    {
        NavigateToDetail(args.Item.EractivityId);
    }

    /// <summary>
    /// Navigates to the legacy ActivityCreateEdit.aspx in copy mode via YARP.
    /// forceLoad: true triggers a full page load so YARP can proxy to the legacy app.
    /// </summary>
    private void CopyActivity(int activityId)
    {
        Navigation.NavigateTo($"/Responsive/Recruiting/ActivityCreateEdit.aspx?ErIdCopy={activityId}", forceLoad: true);
    }

    /// <summary>
    /// Client dropdown selection changed - reload filter options and data grid with new client filter.
    /// Null is ignored because Clearable="false" means null only fires while the user is
    /// typing (Strict="false" fires ValueChanged(null) when text doesn't match any item).
    /// </summary>
    private async Task OnClientChanged(ClientDropdownDto? client)
    {
        if (client == null) return;
        _selectedClient = client;
        _activeClientId = client.ClientId;
        _clientHasWebAdVisitorStatistics = await ClientService.GetWebAdVisitorStatisticsEnabledAsync(client.ClientId);
        _clientUsesTemplateGroups = await ClientService.GetRecruitmentUseTemplateGroupsAsync(client.ClientId);
        _clientIsFund = await ClientService.GetRecruitmentIsFundAsync(client.ClientId);
        var draftSettings = await ClientService.GetRecruitmentDraftSettingsAsync(client.ClientId);
        _draftAreaTypeId = draftSettings.UserResponsabilityAreaTypeId;
        _draftSectionHierarchyEnabled = draftSettings.SectionHierarchyEnabled;
        _draftAreaHeaderKey = draftSettings.ListAreaHeaderTextId;
        _draftResponsibleHeaderKey = draftSettings.DraftResponsibleHeaderTextId;
        ResetMoreFilters();
        if (_currentStatus == ERActivityStatus.Closed)
        {
            _closedDateFrom = DateTime.Today.AddYears(-1);
            _closedDateTo = DateTime.Today;
        }
        await LoadFilterOptionsAsync();
        await _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Selects all text when the dropdown opens so the user can type immediately
    /// without manually deleting the existing text. The grid filter (_activeClientId)
    /// is unaffected — the grid keeps showing the last confirmed client while searching.
    /// </summary>
    private async Task OnClientDropdownOpenChanged(bool isOpen)
    {
        if (isOpen)
            await _clientAutocomplete.SelectAsync();
    }

    private Task<IEnumerable<ClientDropdownDto>> SearchClients(string searchText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult<IEnumerable<ClientDropdownDto>>(_clients);
        return Task.FromResult<IEnumerable<ClientDropdownDto>>(
            _clients.Where(c => c.ClientName.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Create Activity button click - navigates to legacy ASPX page via YARP.
    /// Draft mode uses ActivityCreateDraft.aspx, other modes use ActivityCreateEdit.aspx.
    /// Non-client users must select a client first.
    /// </summary>
    private void OnCreateActivityClick()
    {
        if (!_isClientUser && _activeClientId == null)
        {
            Snackbar.Add(Localization.GetText("PleaseSelectClient"), Severity.Warning);
            return;
        }

        string url;
        if (_currentStatus == ERActivityStatus.Draft)
        {
            // Legacy: client users navigate without ClientId (session has it); internal users pass ClientId.
            // Matches ActivityList.aspx.cs:639-642.
            if (_isClientUser)
                url = "/Responsive/Recruiting/ActivityCreateDraft.aspx";
            else
                url = $"/Responsive/Recruiting/ActivityCreateDraft.aspx?ClientId={_activeClientId ?? 0}";
        }
        else
        {
            var clientId = _isClientUser ? (Session.ClientId ?? 0) : (_activeClientId ?? 0);
            url = clientId > 0
                ? $"/Responsive/Recruiting/ActivityCreateEdit.aspx?ClientId={clientId}"
                : "/Responsive/Recruiting/ActivityCreateEdit.aspx";
        }

        Navigation.NavigateTo(url, forceLoad: true);
    }

    /// <summary>
    /// Button text varies based on current activity mode.
    /// </summary>
    private string _createButtonText => _currentStatus == ERActivityStatus.Draft
        ? Localization.GetText("ERCreateNewDraftActivity")
        : Localization.GetText("CreateNewActivity");

    /// <summary>
    /// Navigates to the Excel export endpoint with current filter state as query parameters.
    /// The endpoint generates and returns the .xlsx file as a file download.
    /// Only available in OnGoing mode when the user has the export permission.
    /// </summary>
    private void OnExportActivityMembersClick()
    {
        var clientId = _isClientUser ? (Session.ClientId ?? 0) : (_activeClientId ?? 0);
        if (clientId <= 0)
        {
            Snackbar.Add(Localization.GetText("PleaseSelectClient"), Severity.Warning);
            return;
        }

        var clientName = _isClientUser ? "" : (_selectedClient?.ClientName ?? "");
        var mode = _currentStatus.ToString().ToLower();

        var qp = new List<string>
        {
            $"clientId={clientId}",
            $"mode={Uri.EscapeDataString(mode)}",
            $"showTemplateGroups={(_clientUsesTemplateGroups ? "true" : "false")}",
            $"showClientSectionGroups={(_filterClientSectionGroups.Count > 0 ? "true" : "false")}"
        };

        if (!string.IsNullOrEmpty(clientName))
            qp.Add($"clientName={Uri.EscapeDataString(clientName)}");

        if (_createdByFilter.HasValue)
        {
            qp.Add($"createdById={_createdByFilter.Value}");
            if (!string.IsNullOrEmpty(_createdByFilterUser?.DisplayName))
                qp.Add($"createdByName={Uri.EscapeDataString(_createdByFilterUser.DisplayName)}");
        }

        if (_recruitmentResponsibleFilter.HasValue)
        {
            qp.Add($"responsibleId={_recruitmentResponsibleFilter.Value}");
            if (!string.IsNullOrEmpty(_recruitmentResponsibleFilterUser?.DisplayName))
                qp.Add($"responsibleName={Uri.EscapeDataString(_recruitmentResponsibleFilterUser.DisplayName)}");
        }

        if (_clientSectionFilter.HasValue)
        {
            qp.Add($"clientSectionId={_clientSectionFilter.Value}");
            if (!string.IsNullOrEmpty(_clientSectionFilterSection?.Name))
                qp.Add($"clientSectionName={Uri.EscapeDataString(_clientSectionFilterSection.Name)}");
        }

        if (_templateGroupFilter.HasValue)
        {
            qp.Add($"templateGroupId={_templateGroupFilter.Value}");
            if (!string.IsNullOrEmpty(_templateGroupFilterItem?.Name))
                qp.Add($"templateGroupName={Uri.EscapeDataString(_templateGroupFilterItem.Name)}");
        }

        if (_clientSectionGroupFilter.HasValue)
        {
            qp.Add($"clientSectionGroupId={_clientSectionGroupFilter.Value}");
            if (!string.IsNullOrEmpty(_clientSectionGroupFilterItem?.Name))
                qp.Add($"clientSectionGroupName={Uri.EscapeDataString(_clientSectionGroupFilterItem.Name)}");
        }

        Navigation.NavigateTo($"/api/activities/export-members?{string.Join("&", qp)}", forceLoad: true);
    }

    // WebAdStatusId values matching legacy WebAdStatus enum
    private const int WebAdStatusDraft = 1;
    private const int JobnetStatusDraft = 1;

    private string _actionsColumnWidth => "36px";

    /// <summary>
    /// Navigates to the activity's Ad tab in the legacy app via YARP.
    /// Matches legacy ListActionType.GoToActivityAdTab → ActivityAd.aspx redirect.
    /// </summary>
    private void NavigateToActivityAdTab(int activityId)
    {
        Navigation.NavigateTo(
            $"/Responsive/Recruiting/ActivityAd.aspx?ErId={activityId}",
            forceLoad: true);
    }

    private string GetWebAdStatusIconSrc(ActivityListDto item)
    {
        if (item.EractivityStatusId != (int)ERActivityStatus.OnGoing) return string.Empty;

        // Sub-rule 1: No WebAdId → missing
        if (!item.WebAdId.HasValue || item.WebAdId.Value <= 0)
            return "/Responsive/images/responsive/list/web-ad-missing_36x36.png";

        // Sub-rules 2-3: Has WebAd media but WebAdId not present (dead code after sub-rule 1, kept for safety)
        if (item.HasWebAdMedia && (!item.WebAdId.HasValue || item.WebAdId.Value <= 0))
            return "/Responsive/images/responsive/list/web-ad-missing_36x36.png";

        // Sub-rule 4: Has Jobnet media but no published Jobnet ad
        if (item.HasJobnetMedia && (!item.JobnetWebAdId.HasValue || item.JobnetWebAdId.Value <= 0))
            return "/Responsive/images/responsive/list/web-ad-missing_36x36.png";

        // Sub-rules 5-7: Draft status
        var webAdDraft = item.HasWebAdMedia && item.WebAdStatusId == WebAdStatusDraft;
        var jobnetDraft = item.HasJobnetMedia && item.JobnetStatusId == JobnetStatusDraft;
        if (webAdDraft || jobnetDraft)
            return "/Responsive/images/responsive/list/web-ad-draft_36x36.png";

        return string.Empty;
    }

    private string GetWebAdStatusTooltipKey(ActivityListDto item)
    {
        if (!item.WebAdId.HasValue || item.WebAdId.Value <= 0)
            return "NoWebAd";

        if (item.HasWebAdMedia && (!item.WebAdId.HasValue || item.WebAdId.Value <= 0))
            return item.HasJobnetMedia ? "NoWebAdAndJobnetAd" : "NoWebAd";

        if (item.HasJobnetMedia && (!item.JobnetWebAdId.HasValue || item.JobnetWebAdId.Value <= 0))
            return "NoJobnetAd";

        var webAdDraft = item.HasWebAdMedia && item.WebAdStatusId == WebAdStatusDraft;
        var jobnetDraft = item.HasJobnetMedia && item.JobnetStatusId == JobnetStatusDraft;
        if (webAdDraft && jobnetDraft) return "WebAdAndJobnetAdInDraftStatus";
        if (webAdDraft) return "WebAdInDraftStatus";
        if (jobnetDraft) return "JobnetAdInDraftStatus";

        return string.Empty;
    }

    // Matches legacy: singular/plural keys × job/fund application type.
    // Legacy: applicationType == JobApplication ? singular/plural job keys : singular/plural fund keys.
    private string GetEmailWarningTooltip(ActivityListDto item)
    {
        var count = item.MembersMissingNotificationEmail;
        string key;
        if (_clientIsFund)
            key = count == 1 ? "NotificationMailNotSendToXMemberWithArgsFund" : "NotificationMailNotSendToXMembersWithArgsFund";
        else
            key = count == 1 ? "NotificationMailNotSendToXMemberWithArgs" : "NotificationMailNotSendToXMembersWithArgs";
        return string.Format(Localization.GetText(key), count);
    }

    // Builds a structured multi-line tooltip matching legacy WebAdChangesTooltipGet:
    //   Line 1: "Web-annoncen er ændret" (WebAdHasBeenChanged)
    //   Line 2: "Følgende felter er ændret:" (FollowingFieldHaveBeenChanged)
    //   Lines 3+: "- fieldname" for each changed field
    // Mail change links from legacy (interactive JS onclick) are omitted.
    private string GetWebAdChangesTooltip(ActivityListDto item)
    {
        if (string.IsNullOrEmpty(item.WebAdChangeSummary)) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Localization.GetText("WebAdHasBeenChanged"));
        sb.AppendLine(Localization.GetText("FollowingFieldHaveBeenChanged") + ":");
        foreach (var field in item.WebAdChangeSummary.Split('\n'))
        {
            if (!string.IsNullOrEmpty(field))
                sb.AppendLine($"- {field}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns the combined CSS class(es) for a data grid row based on the activity's state.
    /// activity-row-cleaned  → gray italic text when the activity is archived/cleaned.
    /// activity-row-needs-review → orange ID cell: member has unread/unevaluated candidates, deadline not exceeded.
    /// activity-row-overdue       → red ID cell:    member has unread/unevaluated candidates, deadline exceeded.
    /// activity-row-reviewed      → gray ID cell:   member has no pending candidates, activity still active.
    /// Color applies only to OnGoing activities where the current user is a member.
    /// </summary>
    private string GetActivityRowCombinedClass(ActivityListDto item)
    {
        var colorClass = GetActivityRowColorClass(item);
        var cleanedClass = item.IsCleaned ? "activity-row-cleaned" : string.Empty;

        return colorClass.Length > 0 && cleanedClass.Length > 0
            ? $"{cleanedClass} {colorClass}"
            : colorClass.Length > 0 ? colorClass : cleanedClass;
    }

    /// <summary>
    /// Builds the tooltip text for the ID cell, matching legacy ActivityList.ascx.cs tooltip logic.
    /// Only shown for OnGoing activities where the current user is a member.
    ///
    /// Evaluation mode (CandidateEvaluationEnabled):
    ///   "YouNeedEvaluateXOfYCandidatesWithArgs" + optional deadline suffix
    /// Read mode:
    ///   "YouHaveXOfYUnreadCandidatesWithArgs" + optional deadline suffix
    /// Deadline suffix (omitted when ContinuousPosting):
    ///   Exceeded  → ". ApplicationDeadlineExceeded."
    ///   Not yet   → ". ApplicationDeadlineNotExceeded."
    /// </summary>
    private string GetIdCellTooltip(ActivityListDto item)
    {
        if (item.EractivityStatusId != (int)ERActivityStatus.OnGoing) return string.Empty;
        if (!item.IsUserMember) return string.Empty;

        var extraTooltip = string.Empty;
        if (!item.ContinuousPosting)
        {
            extraTooltip = item.ApplicationDeadline < DateTime.Today
                ? $". {Localization.GetText("ApplicationDeadlineExceeded")}."
                : $". {Localization.GetText("ApplicationDeadlineNotExceeded")}.";
        }

        // Use string.Format directly to avoid overload ambiguity:
        // GetText(string, int, int) resolves to GetText(key, languageId, params object[]) rather
        // than GetText(key, params object[]), treating the first int as a language ID.
        var formatKey = item.CandidateEvaluationEnabled
            ? "YouNeedEvaluateXOfYCandidatesWithArgs"
            : "YouHaveXOfYUnreadCandidatesWithArgs";
        var countArg = item.CandidateEvaluationEnabled
            ? item.CandidateMissingEvaluationCount
            : item.CandidateNotReadCount;

        return string.Format(Localization.GetText(formatKey), countArg, item.ActiveCandidateCount) + extraTooltip;
    }

    private static string GetActivityRowColorClass(ActivityListDto item)
    {
        if (item.EractivityStatusId != (int)ERActivityStatus.OnGoing) return string.Empty;
        if (!item.IsUserMember) return string.Empty;

        var hasUnread = item.CandidateEvaluationEnabled
            ? item.CandidateMissingEvaluationCount > 0
            : item.CandidateNotReadCount > 0;

        var deadlineExceeded = !item.ContinuousPosting && item.ApplicationDeadline < DateTime.Today;

        if (hasUnread)
            return deadlineExceeded ? "activity-row-overdue" : "activity-row-needs-review";

        return deadlineExceeded ? string.Empty : "activity-row-reviewed";
    }

    /// <summary>
    /// Shows the "no active activities" disclaimer to external users and redirects to login.
    /// Matches legacy ExternalUserNoActiveActivtiesTmr_OnTick + MessageBoxActionEn.ExternalUserNoActivities.
    /// </summary>
    private async Task ShowExternalUserNoActivitiesDialogAsync()
    {
        await _noActivitiesDialog.ShowAsync(options: new DialogOptions { BackdropClick = false });

        if (Session.UserId.HasValue)
            await ErActivityService.LogExternalUserForceLogoutAsync(Session.UserId.Value);

        Navigation.NavigateTo("/Login.aspx", forceLoad: true);
    }

    // ── Mobile card helpers ─────────────────────────────────────────────────

    private int TotalMobilePages =>
        _mobilePageSize > 0 ? (int)Math.Ceiling((double)_totalCount / _mobilePageSize) : 1;

    private void MobilePrevPage() => _dataGrid.NavigateTo(MudBlazor.Page.Previous);
    private void MobileNextPage() => _dataGrid.NavigateTo(MudBlazor.Page.Next);

    private void ToggleMobileCard(int activityId)
    {
        if (!_expandedActivityIds.Remove(activityId))
            _expandedActivityIds.Add(activityId);
    }

    private void OnMobileCardClick(ActivityListDto item)
    {
        NavigateToDetail(item.EractivityId);
    }

    private string GetMobileCardHeadlineClass(ActivityListDto item)
    {
        var classes = new List<string> { "headline" };
        if (item.EractivityStatusId == (int)ERActivityStatus.Closed)
            classes.Add("item-closed");
        if (item.IsCleaned)
            classes.Add("item-cleaned");
        return string.Join(" ", classes);
    }

    private string GetMobileHeadlineText(ActivityListDto item)
        => _showCandidateCount && item.CandidateCount > 0
            ? $"{item.Headline} ({item.CandidateCount})"
            : item.Headline;

    private string GetDeadlineDisplay(ActivityListDto item)
        => item.ContinuousPosting ? "-" : item.ApplicationDeadline.ToString("dd-MM-yyyy");

    private string GetStatusDisplayText(ActivityListDto item)
        => item.EractivityStatusId switch
        {
            (int)ERActivityStatus.OnGoing => Localization.GetText("OnGoing"),
            (int)ERActivityStatus.Closed  => Localization.GetText("Closed"),
            (int)ERActivityStatus.Draft   => Localization.GetText("Draft"),
            _                             => item.EractivityStatusId.ToString()
        };

    /// <summary>
    /// Mobile tooltip — mirrors legacy ActivityList.ascx.cs mobile card tooltip logic:
    /// Members: detailed unread/evaluation tooltip (same as desktop ID cell).
    /// All users: falls back to simple total count using XCandidateWithArgs / XCandidatesWithArgs.
    /// </summary>
    private string GetMobileCardTooltip(ActivityListDto item)
    {
        if (item.EractivityStatusId != (int)ERActivityStatus.OnGoing)
            return string.Empty;

        // Member-specific detailed tooltip (same as desktop ID cell)
        if (item.IsUserMember)
        {
            var memberTooltip = GetIdCellTooltip(item);
            if (!string.IsNullOrEmpty(memberTooltip))
                return memberTooltip;
        }

        // Fallback for all users: simple total count
        if (item.CandidateCount <= 0)
            return string.Empty;

        var key = item.CandidateCount == 1 ? "XCandidateWithArgs" : "XCandidatesWithArgs";
        return string.Format(Localization.GetText(key), item.CandidateCount);
    }
}
