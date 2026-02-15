using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Pages.Activities;

public partial class CandidateList
{
    [Parameter] public int ActivityId { get; set; }
    [Inject] private IActivityService ActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private MudDataGrid<CandidateListDto> _dataGrid = default!;
    private string _searchString = "";
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new("Activities", "/activities"),
            new("Activity", $"/activities/{ActivityId}"),
            new("Candidates", null, disabled: true)
        };
    }

    private async Task<GridData<CandidateListDto>> LoadServerData(GridState<CandidateListDto> state)
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

            if (!string.IsNullOrWhiteSpace(_searchString))
            {
                request.Filters.Add(new FilterDefinition("FullName", "contains", _searchString));
            }

            var response = await ActivityService.GetCandidatesAsync(ActivityId, request);

            return new GridData<CandidateListDto>
            {
                Items = response.Items,
                TotalItems = response.TotalCount
            };
        }
        catch (Exception)
        {
            Snackbar.Add("Error loading candidates. Please refresh the page.", Severity.Error);
            return new GridData<CandidateListDto>
            {
                Items = Array.Empty<CandidateListDto>(),
                TotalItems = 0
            };
        }
    }

    private async Task OnSearchChanged()
    {
        await _dataGrid.ReloadServerData();
    }

    private void NavigateToDetail(int candidateId)
    {
        Navigation.NavigateTo($"/activities/{ActivityId}/candidates/{candidateId}");
    }

    private void OnRowClick(DataGridRowClickEventArgs<CandidateListDto> args)
    {
        NavigateToDetail(args.Item.ErcandidateId);
    }
}
