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
    private ERActivityStatus _currentStatus = ERActivityStatus.OnGoing;
    private string _headlineText = "";
    private int _totalCount;
    private bool _showFilters;
    private string _pagerInfoFormat = "{first_item}-{last_item} of {all_items}";

    // Permission state (loaded once in OnInitializedAsync)
    private bool _isClientUser;
    private bool _canCreateActivity;

    // Client dropdown state (non-client users only)
    private List<ClientDropdownDto> _clients = new();
    private ClientDropdownDto? _selectedClient;  // drives the autocomplete display
    private int? _activeClientId;                // drives the grid filter (stable during search)

    // "More" filter panel state (non-client users only)
    private bool _showMoreFilters;
    private bool _filterOptionsNeedRefresh;

    // More filter selections
    private Guid? _createdByFilter;
    private Guid? _recruitmentResponsibleFilter;
    private int? _clientSectionFilter;

    // More filter dropdown options (populated from existing activities)
    private List<UserDropdownDto> _filterCreatedByUsers = new();
    private List<UserDropdownDto> _filterRecruitmentResponsibleUsers = new();
    private List<ClientSectionDropdownDto> _filterClientSections = new();

    // Column visibility: computed from current mode
    // CreatedBy: visible in Ongoing and Closed, hidden in Draft
    private bool _hideCreatedByColumn => _currentStatus == ERActivityStatus.Draft;
    // DraftResponsible: visible in Draft only
    private bool _hideDraftResponsibleColumn => _currentStatus != ERActivityStatus.Draft;
    // Visitors: visible in Ongoing and Closed, hidden in Draft, AND only if client has WebAdVisitorStatistics enabled
    // TODO: Replace _clientHasWebAdVisitorStatistics with actual check from ClientHlp.ClientWebAdVisitorStatisticsEnabled(siteId, clientId)
    //       This requires reading the Client.ObjectData XML config. See legacy: ActivityList.ascx.cs line 808.
    private bool _clientHasWebAdVisitorStatistics = true; // Hardcoded on — matches current client config
    private bool _hideVisitorsColumn => _currentStatus == ERActivityStatus.Draft || !_clientHasWebAdVisitorStatistics;
    // TemplateGroup: visible in Ongoing and Closed, hidden in Draft, AND only if client uses template groups
    // TODO: Replace _clientUsesTemplateGroups with actual check from ClientHlp.ClientRecruitmentUseTemplateGroups(siteId, clientId)
    //       This requires reading the Client.ObjectData XML config. See legacy: ActivityList.ascx.cs line 392.
    private bool _clientUsesTemplateGroups = false; // Hardcoded off — matches current client config
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
        _clientSectionFilter.HasValue;

    private string _moreButtonText => _showMoreFilters
        ? $"« {Localization.GetText("Less")}"
        : $"{Localization.GetText("More")} »";

    protected override async Task OnInitializedAsync()
    {
        _isClientUser = Session.IsClientUser;
        _canCreateActivity = await PermHelper.UserCanCreateActivityAsync();
        _pagerInfoFormat = $"{{first_item}}-{{last_item}} {Localization.GetText("Of")} {{all_items}}";

        if (!_isClientUser)
        {
            var siteId = Session.SiteId ?? 0;
            if (siteId > 0)
                _clients = await ClientService.GetClientsForSiteAsync(siteId);
            _selectedClient = _clients.Count > 0 ? _clients[0] : null;
            _activeClientId = _selectedClient?.ClientId;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
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
    }

    /// <summary>
    /// Clears all More panel filter selections without reloading the grid.
    /// </summary>
    private void ResetMoreFilters()
    {
        _createdByFilter = null;
        _recruitmentResponsibleFilter = null;
        _clientSectionFilter = null;
    }

    /// <summary>
    /// Toggles the More filter panel open/closed.
    /// The panel auto-expands when any more filter is active (matches legacy SetupMoreToolsOpenClosed).
    /// </summary>
    private void ToggleMoreFilters()
    {
        _showMoreFilters = !_showMoreFilters;
    }

    private async Task OnCreatedByFilterChanged(Guid? value)
    {
        _createdByFilter = value;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnRecruitmentResponsibleFilterChanged(Guid? value)
    {
        _recruitmentResponsibleFilter = value;
        await _dataGrid.ReloadServerData();
    }

    private async Task OnClientSectionFilterChanged(int? value)
    {
        _clientSectionFilter = value;
        await _dataGrid.ReloadServerData();
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

            // Build More panel filters (only for non-client users when at least one is set)
            ActivityListFilterDto? moreFilters = null;
            if (!_isClientUser && _hasActiveMoreFilters)
            {
                moreFilters = new ActivityListFilterDto
                {
                    CreatedByUserId = _createdByFilter,
                    RecruitmentResponsibleUserId = _recruitmentResponsibleFilter,
                    ClientSectionId = _clientSectionFilter
                };
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
    /// Client dropdown selection changed - reload filter options and data grid with new client filter.
    /// Null is ignored because Clearable="false" means null only fires while the user is
    /// typing (Strict="false" fires ValueChanged(null) when text doesn't match any item).
    /// </summary>
    private async Task OnClientChanged(ClientDropdownDto? client)
    {
        if (client == null) return;
        _selectedClient = client;
        _activeClientId = client.ClientId;
        ResetMoreFilters();
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
}
