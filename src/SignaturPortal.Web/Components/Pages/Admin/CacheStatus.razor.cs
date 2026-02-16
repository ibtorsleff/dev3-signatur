using Microsoft.AspNetCore.Components;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Localization;

namespace SignaturPortal.Web.Components.Pages.Admin;

// TODO: Restrict to admin role when PermissionService supports page-level auth
public partial class CacheStatus
{
    [Inject]
    private LocalizationCacheWarmupService WarmupService { get; set; } = default!;

    [Inject]
    private ILocalizationService L { get; set; } = default!;

    private bool _isReloading;
    private string? _successMessage;
    private string? _errorMessage;

    private async Task OnReloadCache()
    {
        _isReloading = true;
        _successMessage = null;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var count = await WarmupService.ReloadAsync();
            _successMessage = $"Cache reloaded successfully. {count} entries loaded.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Cache reload failed: {ex.Message}";
        }
        finally
        {
            _isReloading = false;
            StateHasChanged();
        }
    }
}
