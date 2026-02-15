using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Pages.Activities;

public partial class ActivityList
{
    [Inject] private IActivityService ActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private MudDataGrid<ActivityListDto> _dataGrid = default!;

    /// <summary>
    /// Server-side data loading callback for MudDataGrid.
    /// Maps MudBlazor GridState to our GridRequest DTO.
    /// </summary>
    private async Task<GridData<ActivityListDto>> LoadServerData(GridState<ActivityListDto> state)
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

        var response = await ActivityService.GetActivitiesAsync(request);

        return new GridData<ActivityListDto>
        {
            Items = response.Items,
            TotalItems = response.TotalCount
        };
    }

    /// <summary>
    /// Navigates to the activity detail page.
    /// </summary>
    private void NavigateToDetail(int activityId)
    {
        Navigation.NavigateTo($"/activities/{activityId}");
    }

    /// <summary>
    /// Row click handler - navigates to detail page.
    /// </summary>
    private void OnRowClick(DataGridRowClickEventArgs<ActivityListDto> args)
    {
        NavigateToDetail(args.Item.EractivityId);
    }
}
