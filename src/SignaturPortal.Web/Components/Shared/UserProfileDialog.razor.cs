using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Shared;

/// <summary>
/// Code-behind for the user profile edit dialog.
/// Blazor migration of legacy UserProfile.ascx.cs.
///
/// Lifecycle:
///   OnInitializedAsync → load profile via IUserProfileService → populate form fields.
///   SaveAsync          → validate form → call UpdateProfileAsync → close / force re-login.
///
/// ForcedUpdate mode (from mandatory SSO profile completion):
///   - Cancel button hidden.
///   - Dialog options set by opener to disable escape key and backdrop click.
///
/// Session variables used by legacy:
///   Session["ImpersonatedBy"] (Guid) — read to detect impersonation after username change.
///   In Blazor this is readable only during SSR via System.Web.HttpContext.Current.Session.
///   For a dialog opened via SignalR (button click), it is not accessible.
///   The impersonation re-attach after username change is therefore deferred:
///   the user is directed to log in again after a username change.
/// </summary>
public partial class UserProfileDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// When true the Cancel button is hidden and the dialog cannot be closed with
    /// escape or backdrop click (DialogOptions are set by the opener).
    /// Used for mandatory profile completion triggered on login.
    /// </summary>
    [Parameter]
    public bool ForcedUpdate { get; set; }

    [Inject]
    private IUserProfileService ProfileService { get; set; } = default!;

    [Inject]
    private IUserSessionContext Session { get; set; } = default!;

    [Inject]
    private ILocalizationService Localization { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Component state
    // -------------------------------------------------------------------------

    private MudForm _form = default!;
    private UserProfileDto? _profile;
    private bool _loading = true;
    private bool _saving;
    private string? _loadError;

    // Form field bindings
    private string? _workArea;
    private string? _title;
    private string? _officePhone;
    private string? _cellPhone;
    private string? _email;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        if (!Session.UserId.HasValue)
        {
            _loadError = "Session not initialised.";
            _loading = false;
            return;
        }

        var siteId = Session.SiteId ?? 0;

        try
        {
            _profile = await ProfileService.GetProfileAsync(Session.UserId.Value, siteId);

            if (_profile is null)
            {
                _loadError = "Profile not found.";
            }
            else
            {
                _workArea    = _profile.WorkArea;
                _title       = _profile.Title;
                _officePhone = _profile.OfficePhone;
                _cellPhone   = _profile.CellPhone;
                _email       = _profile.Email;
            }
        }
        catch (Exception ex)
        {
            _loadError = $"Failed to load profile: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    private async Task SaveAsync()
    {
        if (_profile is null) return;

        await _form.Validate();
        if (!_form.IsValid) return;

        _saving = true;
        StateHasChanged();

        try
        {
            var command = new UpdateUserProfileCommand
            {
                UserId = _profile.UserId,
                SiteId = Session.SiteId ?? 0,
                // WorkArea / Title only sent when field is visible (internal users)
                WorkArea     = _profile.Config.ShowWorkArea ? _workArea?.Trim() : null,
                Title        = _profile.Config.ShowTitle    ? _title?.Trim()    : null,
                OfficePhone  = _officePhone?.Trim(),
                CellPhone    = _cellPhone?.Trim(),
                // Email only sent when editable; send null when disabled to skip update
                Email        = _profile.Config.EmailDisabled
                                   ? null
                                   : _email?.Trim().ToLower(),
            };

            var result = await ProfileService.UpdateProfileAsync(command);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? Localization.GetText("SaveFailed"), Severity.Error);
                return;
            }

            if (result.UsernameUpdated)
            {
                // Username changed → auth cookie references old username → force re-login.
                // Mirrors legacy: LogOut(false) + LogIn() in UserProfile.ascx.cs.
                var message = Localization.GetText(
                    "YourProfileHasBeenUpdatedUsernameChangedWithArgs", result.NewUserName);
                Snackbar.Add(message, Severity.Success);
                MudDialog.Close();
                Navigation.NavigateTo("/auth/logout", forceLoad: true);
            }
            else
            {
                Snackbar.Add(Localization.GetText("YourProfileHasBeenUpdated"), Severity.Success);
                MudDialog.Close();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    // -------------------------------------------------------------------------
    // Validation functions (called by MudTextField Validation property)
    // Mirror legacy DoValidate() in UserProfile.ascx.cs
    // -------------------------------------------------------------------------

    private IEnumerable<string> ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) yield break;

        // Allow digits, spaces, +, -, (, ), . — mirrors FLTextBox DataType="PhoneNumber"
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[\d\s\+\-\(\)\.]+$"))
            yield return Localization.GetText("InvalidPhoneNumber");
    }

    private IEnumerable<string> ValidateEmail(string? email)
    {
        if (_profile?.Config is null) yield break;
        if (_profile.Config.EmailDisabled) yield break;
        if (string.IsNullOrWhiteSpace(email)) yield break;

        // Basic format — mirrors FLTextBox DataType="Email"
        if (!email.Contains('@') || email.IndexOf('.', email.IndexOf('@')) < 0)
        {
            yield return Localization.GetText("InvalidEmail");
            yield break;
        }

        var emailDomain = GetEmailDomain(email);

        // Whitelist: email domain must be in allowed list (internal users only)
        // Mirrors: ClientHlp.ClientUserAllowedEmailDomains()
        if (_profile.Config.AllowedEmailDomains.Count > 0 && _profile.IsInternal)
        {
            if (!_profile.Config.AllowedEmailDomains.Contains(emailDomain, StringComparer.OrdinalIgnoreCase))
            {
                yield return Localization.GetText(
                    "InvalidEmailDomainWithArgs",
                    string.Join(", ", _profile.Config.AllowedEmailDomains));
                yield break;
            }
        }

        // Blacklist: email domain must NOT be in blocked list
        // Mirrors: ClientHlp.ClientUser[Internal/External]NotAllowedEmailDomains()
        if (_profile.Config.BlockedEmailDomains.Count > 0)
        {
            if (_profile.Config.BlockedEmailDomains.Contains(emailDomain, StringComparer.OrdinalIgnoreCase))
            {
                yield return Localization.GetText(
                    "InvalidEmailDomainInvalidDomainsXWithArgs",
                    string.Join(", ", _profile.Config.BlockedEmailDomains));
                yield break;
            }
        }

        // Work-email domain: new email must be in client's registered domains
        // Applies when email changes OR MustChangeEmailToWorkEmail is set
        // Mirrors: ClientHlp.ClientMailDomains() + MustChangeEmailToWorkEmail check
        if (_profile.Config.ClientMailDomains.Count > 0)
        {
            bool emailChanged = !string.Equals(
                _profile.Email, email, StringComparison.OrdinalIgnoreCase);

            if (emailChanged || _profile.Config.MustChangeEmailToWorkEmail)
            {
                if (!_profile.Config.ClientMailDomains.Contains(emailDomain, StringComparer.OrdinalIgnoreCase))
                {
                    yield return Localization.GetText(
                        "InvalidWorkEmailDomainWithArgs",
                        string.Join(", ", _profile.Config.ClientMailDomains));
                }
            }
        }
    }

    private static string GetEmailDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..].ToLower() : string.Empty;
    }
}
