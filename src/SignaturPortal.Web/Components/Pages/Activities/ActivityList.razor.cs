using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Enums;

namespace SignaturPortal.Web.Components.Pages.Activities;

public partial class ActivityList
{
    [Inject] private IActivityService ActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private ILocalizationService Localization { get; set; } = default!;
    [Inject] private IUserSessionContext Session { get; set; } = default!;
    [Inject] private IPermissionHelper PermHelper { get; set; } = default!;
    [Inject] private IClientService ClientService { get; set; } = default!;

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
    private bool _canCreateActivity;
    private bool _userHasExportPermission;
    private bool _clientExportConfigEnabled;

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

    // Flag set at the END of OnInitializedAsync to signal the grid should load.
    // OnAfterRenderAsync(firstRender: true) fires BEFORE OnInitializedAsync completes
    // for async lifecycle methods, so _activeClientId would be null at that point.
    // Setting this flag at the end of init and reloading on the next AfterRender
    // ensures the grid loads only after all client/session data is ready.
    private bool _gridNeedsLoad;

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
    // ClientSection ("Afdeling"): hidden when user is a client user (matches legacy dskClientTh visibility)
    private bool _hideClientSectionColumn => _isClientUser;
    // Actions (copy): visible in Ongoing and Closed, hidden in Draft, AND user must have CreateActivity permission
    private bool _hideActionsColumn => _currentStatus == ERActivityStatus.Draft || !_canCreateActivity;
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

    protected override async Task OnInitializedAsync()
    {
        _isClientUser = Session.IsClientUser;
        _canCreateActivity = await PermHelper.UserCanCreateActivityAsync();
        _userHasExportPermission = await PermHelper.UserCanExportActivityMembersAsync();
        _pagerInfoFormat = $"{{first_item}}-{{last_item}} {Localization.GetText("Of")} {{all_items}}";

        if (!_isClientUser)
        {
            // Internal users: client config is always enabled — only the permission check matters
            _clientExportConfigEnabled = true;

            var siteId = Session.SiteId ?? 0;
            if (siteId > 0)
                _clients = await ClientService.GetClientsForSiteAsync(siteId);
            _selectedClient = _clients.Count > 0 ? _clients[0] : null;
            _activeClientId = _selectedClient?.ClientId;
        }
        else
        {
            // Client users: check whether their client has export enabled
            var clientId = Session.ClientId ?? 0;
            if (clientId > 0)
                _clientExportConfigEnabled = await ClientService.GetExportActivityMembersEnabledAsync(clientId);
        }

        if (_activeClientId.HasValue)
        {
            _clientHasWebAdVisitorStatistics = await ClientService.GetWebAdVisitorStatisticsEnabledAsync(_activeClientId.Value);
            _clientUsesTemplateGroups = await ClientService.GetRecruitmentUseTemplateGroupsAsync(_activeClientId.Value);
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
        }
        else if (_filterOptionsNeedRefresh && !_isClientUser)
        {
            _filterOptionsNeedRefresh = false;
            await LoadFilterOptionsAsync();
            StateHasChanged();
        }
    }

    protected override void OnParametersSet()
    {
        var newStatus = Mode?.ToLowerInvariant() switch
        {
            "draft" => ERActivityStatus.Draft,
            "closed" => ERActivityStatus.Closed,
            _ => ERActivityStatus.OnGoing
        };

        _headlineText = newStatus switch
        {
            ERActivityStatus.Draft => Localization.GetText("ERecruitmentDraftActivities"),
            ERActivityStatus.Closed => Localization.GetText("ERecruitmentClosedActivities"),
            _ => Localization.GetText("ERecruitmentOngoingActivities")
        };

        if (newStatus != _currentStatus)
        {
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
    /// Loads filter dropdown options from the service for the current client and status.
    /// </summary>
    private async Task LoadFilterOptionsAsync()
    {
        var options = await ActivityService.GetActivityFilterOptionsAsync(_currentStatus, _activeClientId);
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

            var response = await ActivityService.GetActivitiesAsync(request, _currentStatus, clientFilter, moreFilters);
            _totalCount = response.TotalCount;

            return new GridData<ActivityListDto>
            {
                Items = response.Items,
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
            Navigation.NavigateTo($"/activities/{activityId}");
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

        var clientId = _isClientUser ? (Session.ClientId ?? 0) : (_activeClientId ?? 0);

        string url;
        if (_currentStatus == ERActivityStatus.Draft)
        {
            url = clientId > 0
                ? $"/Responsive/Recruiting/ActivityCreateDraft.aspx?ClientId={clientId}"
                : "/Responsive/Recruiting/ActivityCreateDraft.aspx";
        }
        else
        {
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
}
