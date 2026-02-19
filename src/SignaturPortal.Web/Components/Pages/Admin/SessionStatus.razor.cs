using Microsoft.AspNetCore.Components;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Pages.Admin;

// TODO: Restrict to admin role when PermissionService supports page-level auth
public partial class SessionStatus
{
    [Inject]
    private IUserSessionContext Session { get; set; } = default!;

    private bool? _disclaimerChecked;

    protected override void OnInitialized()
    {
        var raw = System.Web.HttpContext.Current?.Session?["SsoLoginDisclaimerChecked"];
        _disclaimerChecked = raw as bool?;
    }
}
