using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Shared.Recruiting;

/// <summary>
/// Dialog for searching and selecting a user (recruitment responsible or alternative responsible).
/// Returns the selected UserDropdownDto on close.
/// </summary>
public partial class UserPickerDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string Title { get; set; } = "";

    [Parameter]
    public int ClientId { get; set; }

    [Inject]
    private IErActivityService ErActivityService { get; set; } = default!;

    [Inject]
    private ILocalizationService Localization { get; set; } = default!;

    private List<UserDropdownDto> _users = new();
    private UserDropdownDto? _selectedUser;
    private string _searchTerm = "";
    private bool _loading;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
    }

    private async Task OnSearchChangedAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            _users = await ErActivityService.GetUsersForPickerAsync(ClientId, _searchTerm);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnRowClickedAsync(DataGridRowClickEventArgs<UserDropdownDto> args)
    {
        _selectedUser = args.Item;
        await SelectUserAsync();
    }

    private Task SelectUserAsync()
    {
        MudDialog.Close(_selectedUser);
        return Task.CompletedTask;
    }

    private void Cancel() => MudDialog.Cancel();
}
