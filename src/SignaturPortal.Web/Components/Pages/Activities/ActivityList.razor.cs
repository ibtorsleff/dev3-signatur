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
    private int _selectedClientId = -1; // -1 = all clients (no filter)

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
            _dataGrid?.ReloadServerData();
        }
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

            var clientFilter = _isClientUser ? null : (_selectedClientId > 0 ? (int?)_selectedClientId : null);
            var response = await ActivityService.GetActivitiesAsync(request, _currentStatus, clientFilter);
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
    /// Client dropdown selection changed - reload the data grid with new client filter.
    /// </summary>
    private async Task OnClientChanged(int clientId)
    {
        _selectedClientId = clientId;
        await _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Create Activity button click - navigates to legacy ASPX page via YARP.
    /// Draft mode uses ActivityCreateDraft.aspx, other modes use ActivityCreateEdit.aspx.
    /// Non-client users must select a client first.
    /// </summary>
    private void OnCreateActivityClick()
    {
        if (!_isClientUser && _selectedClientId <= 0)
        {
            Snackbar.Add(Localization.GetText("PleaseSelectClient"), Severity.Warning);
            return;
        }

        var clientId = _isClientUser ? (Session.ClientId ?? 0) : _selectedClientId;

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
