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

    [Parameter] public string? Mode { get; set; }

    private MudDataGrid<ActivityListDto> _dataGrid = default!;
    private ERActivityStatus _currentStatus = ERActivityStatus.OnGoing;
    private string _headlineText = "Igangværende sager";
    private int _totalCount;
    private bool _showFilters;

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
    // Actions (copy): visible in Ongoing and Closed, hidden in Draft
    // TODO: Also check user has RecruitmentPortalCreateActivity permission. See legacy: ActivityList.ascx.cs.
    private bool _hideActionsColumn => _currentStatus == ERActivityStatus.Draft;
    // Whether to show candidate count in Headline
    private bool _showCandidateCount => _currentStatus != ERActivityStatus.Draft;

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
            ERActivityStatus.Draft => "Kladdesager",
            ERActivityStatus.Closed => "Afsluttede sager",
            _ => "Igangværende sager"
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

            var response = await ActivityService.GetActivitiesAsync(request, _currentStatus);
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
            Snackbar.Add("Error loading activities. Please refresh the page.", Severity.Error);
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
}
