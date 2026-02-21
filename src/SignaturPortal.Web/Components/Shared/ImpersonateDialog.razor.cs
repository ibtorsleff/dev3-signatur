using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Shared;

/// <summary>
/// Code-behind for the impersonate user search dialog.
/// Blazor migration of legacy Impersonate.ascx.
///
/// Lifecycle:
///   OnInitializedAsync → load client dropdown (non-client users only).
///   SearchAsync        → query IImpersonateService (max 51 rows) → populate grid or show warning.
///   OnUserRowClick     → navigate with forceLoad to Default.aspx?DoImpersonate={userId}.
///                        The legacy page handles the auth-cookie swap and redirects back.
/// </summary>
public partial class ImpersonateDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IImpersonateService ImpersonateService { get; set; } = default!;

    [Inject]
    private IClientService ClientService { get; set; } = default!;

    [Inject]
    private IUserSessionContext Session { get; set; } = default!;

    [Inject]
    private ILocalizationService Localization { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Component state
    // -------------------------------------------------------------------------

    private string _searchText = string.Empty;
    private int? _selectedClientId;
    private bool _loading;
    private bool _hasSearched;
    private bool _tooManyResults;
    private int _tooManyCount;
    private IReadOnlyList<ImpersonateUserDto> _users = [];
    private IReadOnlyList<ClientDropdownDto> _clients = [];

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        if (!Session.IsClientUser)
        {
            var siteId = Session.SiteId ?? 0;
            _clients = await ClientService.GetClientsForSiteAsync(siteId);
        }
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return;

        _loading = true;
        _hasSearched = true;
        _tooManyResults = false;
        _tooManyCount = 0;
        _users = [];
        StateHasChanged();

        try
        {
            var results = await ImpersonateService.SearchUsersAsync(_searchText, _selectedClientId);
            _tooManyResults = results.Count > 50;
            if (_tooManyResults)
                _tooManyCount = await ImpersonateService.CountUsersAsync(_searchText, _selectedClientId);
            _users = _tooManyResults ? [] : results;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ClearFilter()
    {
        _searchText = string.Empty;
        _selectedClientId = null;
        _hasSearched = false;
        _tooManyResults = false;
        _tooManyCount = 0;
        _users = [];
    }

    private async Task OnSearchKeyUp(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
            await SearchAsync();
    }

    private void OnUserRowClick(DataGridRowClickEventArgs<ImpersonateUserDto> args)
    {
        var returnPath = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
        Navigation.NavigateTo(
            $"/Default.aspx?DoImpersonate={args.Item.UserId}&ReturnUrl={Uri.EscapeDataString(returnPath)}",
            forceLoad: true);
    }

    private void Cancel() => MudDialog.Cancel();
}
