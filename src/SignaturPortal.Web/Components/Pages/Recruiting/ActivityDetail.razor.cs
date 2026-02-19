using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Pages.Recruiting;

public partial class ActivityDetail
{
    [Parameter] public int ActivityId { get; set; }

    [Inject] private IErActivityService ErActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ActivityDetailDto? _activity;
    private bool _notFound;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new("Activities", "/recruiting/activities"),
            new("Detail", null, disabled: true)
        };

        try
        {
            _activity = await ErActivityService.GetActivityDetailAsync(ActivityId);

            if (_activity == null)
            {
                _notFound = true;
            }
            else
            {
                // Update breadcrumb with activity headline
                _breadcrumbs[1] = new BreadcrumbItem(_activity.Headline, null, disabled: true);
            }
        }
        catch (Exception)
        {
            _notFound = true;
            _errorMessage = "An error occurred while loading the activity. Please try again.";
        }
    }

    private Color GetStatusColor(int statusId) => statusId switch
    {
        1 => Color.Success,   // Ongoing
        2 => Color.Default,   // Closed
        3 => Color.Error,     // Deleted
        4 => Color.Warning,   // Draft
        _ => Color.Default
    };

    private Color GetMemberTypeColor(int memberTypeId) => memberTypeId switch
    {
        1 => Color.Primary,   // Internal
        2 => Color.Secondary, // External
        3 => Color.Warning,   // External (Draft)
        _ => Color.Default
    };

    private string BoolDisplay(bool? value) => value switch
    {
        true => "\u2713",    // checkmark
        false => "\u2717",   // cross
        null => "-"
    };
}
