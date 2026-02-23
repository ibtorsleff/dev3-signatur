using Microsoft.AspNetCore.Components;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Web.Components.Shared.Recruiting;

namespace SignaturPortal.Web.Components.Pages.Recruiting;

/// <summary>
/// Activity create / edit form.
/// Supports two routes:
///   /recruiting/activities/new            → create mode
///   /recruiting/activities/{id}/edit      → edit mode
///
/// Session variables (written during SSR in OnInitializedAsync):
///   ERecruitmentCreateActivity    → true in create mode, null in edit mode
///   ERecruitingVisitedActivityId  → activityId in edit mode (not set in create)
/// </summary>
public partial class ActivityCreateEdit
{
    [Parameter] public int? ActivityId { get; set; }
    [SupplyParameterFromQuery] public int? ClientId { get; set; }

    [Inject] private IErActivityService ErActivityService { get; set; } = default!;
    [Inject] private IClientService ClientService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILocalizationService Localization { get; set; } = default!;
    [Inject] private IUserSessionContext Session { get; set; } = default!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = default!;

    // -------------------------------------------------------------------------
    // Component state
    // -------------------------------------------------------------------------

    private readonly ActivityFormModel _form = new();
    private ActivityFormOptionsDto _options = new();
    private ActivityClientConfigDto _clientConfig = new();
    private ActivityEditDto? _editData;

    private bool _loading = true;
    private bool _saving;
    private bool _cascadeLoading;
    private string? _loadError;
    private string? _saveError;
    private bool _isClientUser;
    private Guid _currentUserId;
    private string? _currentUserDisplayName;

    // Sidebar display models (kept in sync with _form)
    private UserDropdownDto? _responsibleUser;
    private List<UserDropdownDto> _alternativeUsers = new();
    private List<HiringTeamMemberDto> _committeeMembers = new();

    // Autocomplete refs (for resetting)
    private MudAutocomplete<ClientDropdownDto> _clientAutocomplete = default!;
    private MudAutocomplete<ClientSectionDropdownDto> _clientSectionAutocomplete = default!;

    // Selected display objects (drive autocomplete display)
    private ClientDropdownDto? _selectedClient;
    private ClientSectionDropdownDto? _selectedClientSection;
    private SimpleOptionDto? _selectedOccupation;

    // Client dropdown backing
    private List<ClientDropdownDto> _clients = new();

    // Application deadline time component (separate from date for MudTimePicker binding)
    private TimeSpan? _applicationDeadlineTime;

    private bool IsEditMode => ActivityId.HasValue;

    private bool IsDeletedActivity => IsEditMode && _editData?.StatusId == 3;
    private bool IsDraftActivity   => IsEditMode && _editData?.StatusId == 4;

    // Template inputs disabled when: cascade loading, no client, or template groups enabled but none selected yet
    private bool TemplateInputsDisabled =>
        _cascadeLoading ||
        !_form.ClientId.HasValue ||
        (_clientConfig.UseTemplateGroups && !_form.TemplateGroupId.HasValue);

    // InterviewRounds: show if client has the flag, or in edit mode if activity already has rounds set
    private bool ShowInterviewRoundsDropdown =>
        _clientConfig.MultipleInterviewRoundsEnabled ||
        (IsEditMode && (_editData?.InterviewRounds ?? 0) > 1);

    // ExtendedEvaluation: both client evaluation flags required; in edit mode also show if already enabled on activity
    private bool ShowExtendedEvaluation =>
        _clientConfig.CandidateEvaluationEnabled &&
        (_clientConfig.ExtendedEvaluationEnabled || (IsEditMode && _form.ExtendedEvaluationEnabled));

    // RecruitmentType visibility helpers — derived from client config flags (match legacy truth table)
    private bool ShowLeadershipPositionDropdown =>
        _clientConfig.LeadershipPositionEnabled &&
        (!_clientConfig.BlindRecruitmentEnabled || !_clientConfig.LeadershipPositionLimitedCandidateAccessEnabled);

    private bool ShowBlindRecruitmentDropdown =>
        _clientConfig.BlindRecruitmentEnabled &&
        (!_clientConfig.LeadershipPositionEnabled || !_clientConfig.LeadershipPositionLimitedCandidateAccessEnabled);

    private bool ShowCombinedRecruitmentTypeDropdown =>
        _clientConfig.BlindRecruitmentEnabled &&
        _clientConfig.LeadershipPositionEnabled &&
        _clientConfig.LeadershipPositionLimitedCandidateAccessEnabled;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var currentUser = await CurrentUserService.GetCurrentUserAsync();
            _currentUserId = currentUser?.UserId ?? Guid.Empty;
            _currentUserDisplayName = currentUser?.FullName ?? currentUser?.UserName;
            _isClientUser = !(currentUser?.IsInternal ?? true);

            // Write session variables (SSR only)
            if (IsEditMode && ActivityId.HasValue)
            {
                System.Web.HttpContext.Current?.Session?.Remove("ERecruitmentCreateActivity");
                if (System.Web.HttpContext.Current?.Session != null)
                    System.Web.HttpContext.Current.Session["ERecruitingVisitedActivityId"] = ActivityId.Value;
            }
            else
            {
                if (System.Web.HttpContext.Current?.Session != null)
                {
                    System.Web.HttpContext.Current.Session["ERecruitmentCreateActivity"] = true;
                    System.Web.HttpContext.Current.Session.Remove("ERecruitingVisitedActivityId");
                }
            }

            // Load clients for dropdown (non-client users only)
            if (!_isClientUser)
            {
                _clients = await ClientService.GetClientsForSiteAsync(Session.SiteId ?? 0);
            }

            if (IsEditMode && ActivityId.HasValue)
            {
                await LoadEditModeAsync(ActivityId.Value);
            }
            else
            {
                await LoadCreateModeAsync();
            }
        }
        catch (Exception ex)
        {
            _loadError = $"Failed to load form: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    // -------------------------------------------------------------------------
    // Load helpers
    // -------------------------------------------------------------------------

    private async Task LoadCreateModeAsync()
    {
        // Pre-select client from query string or session
        int? preselectedClientId = ClientId ?? (_isClientUser ? Session.ClientId : null);

        if (preselectedClientId.HasValue)
        {
            _selectedClient = _clients.FirstOrDefault(c => c.ClientId == preselectedClientId.Value);
            _form.ClientId = preselectedClientId.Value;
            await LoadClientDependentDataAsync(preselectedClientId.Value);
            if (_clientConfig.SendDailyStatusMailEnabled && _clientConfig.SendDailyStatusMailDefaultOn)
                _form.SendDailyStatusMail = true;
        }

        // Default status = Ongoing; HireDate default = Date (legacy has no NoHireDate option)
        _form.StatusId = 1;
        _form.HireDateType = 1;
    }

    private async Task LoadEditModeAsync(int activityId)
    {
        _editData = await ErActivityService.GetActivityForEditAsync(activityId);
        if (_editData == null)
        {
            _loadError = "Activity not found or access denied.";
            return;
        }

        // Pre-populate _selectedClient for the autocomplete
        _selectedClient = _clients.FirstOrDefault(c => c.ClientId == _editData.ClientId)
            ?? new ClientDropdownDto(_editData.ClientId, _editData.ClientName, true);

        // Load client-dependent data
        await LoadClientDependentDataAsync(_editData.ClientId);

        // Map edit data to form model
        MapEditDataToForm(_editData);
    }

    private async Task LoadClientDependentDataAsync(int clientId)
    {
        _clientConfig = await ClientService.GetActivityClientConfigAsync(clientId);
        _options = await ErActivityService.GetActivityFormOptionsAsync(clientId, _form.TemplateGroupId);
        AutoSelectSingleTemplateOptions();
    }

    // Mirrors legacy FLDropDown.DataBindControl: when a required dropdown has exactly
    // one item and nothing is selected yet, auto-select that item.
    private void AutoSelectSingleTemplateOptions()
    {
        if (_options.TemplateGroups.Count == 1 && !_form.TemplateGroupId.HasValue)
            _form.TemplateGroupId = _options.TemplateGroups[0].TemplateGroupId;
        if (_options.ApplicationTemplates.Count == 1 && !_form.ApplicationTemplateId.HasValue)
            _form.ApplicationTemplateId = _options.ApplicationTemplates[0].Id;
        if (_options.EmailTemplatesReceived.Count == 1 && !_form.EmailTemplateReceivedId.HasValue)
            _form.EmailTemplateReceivedId = _options.EmailTemplatesReceived[0].Id;
        if (_options.EmailTemplatesInterview.Count == 1 && !_form.EmailTemplateInterviewId.HasValue)
            _form.EmailTemplateInterviewId = _options.EmailTemplatesInterview[0].Id;
        if (_options.EmailTemplatesInterview2Plus.Count == 1 && !_form.EmailTemplateInterview2PlusId.HasValue)
            _form.EmailTemplateInterview2PlusId = _options.EmailTemplatesInterview2Plus[0].Id;
        if (_options.EmailTemplatesRejected.Count == 1 && !_form.EmailTemplateRejectedId.HasValue)
            _form.EmailTemplateRejectedId = _options.EmailTemplatesRejected[0].Id;
        if (_options.EmailTemplatesRejectedAfterInterview.Count == 1 && !_form.EmailTemplateRejectedAfterInterviewId.HasValue)
            _form.EmailTemplateRejectedAfterInterviewId = _options.EmailTemplatesRejectedAfterInterview[0].Id;
        if (_options.EmailTemplatesNotifyCommittee.Count == 1 && !_form.EmailTemplateNotifyCommitteeId.HasValue)
            _form.EmailTemplateNotifyCommitteeId = _options.EmailTemplatesNotifyCommittee[0].Id;
        if (_options.SmsTemplates.Count == 1 && !_form.SmsTemplateInterviewId.HasValue)
            _form.SmsTemplateInterviewId = _options.SmsTemplates[0].Id;
    }

    private void MapEditDataToForm(ActivityEditDto data)
    {
        _form.StatusId = data.StatusId;
        _form.ClientId = data.ClientId;
        _form.Headline = data.Headline;
        _form.JobTitle = data.JobTitle;
        _form.JobnetOccupationId = data.JobnetOccupationId;
        _form.ClientSectionGroupId = data.ClientSectionGroupId;
        _form.ClientSectionId = data.ClientSectionId;
        _form.Reposting = data.Reposting;
        // Populate raw LP/Blind fields for separate-dropdown cases
        _form.LeadershipPositionId = data.LeadershipPositionId;
        _form.BlindRecruitmentId = data.BlindRecruitmentId;
        // Translate to combined RecruitmentTypeId for the combined-dropdown case
        // 1=Yes, 2=No for each. LeadershipPosition=Yes → type 2; BlindRecruitment=Yes → type 3; otherwise → type 1 (Normal)
        _form.RecruitmentTypeId = data.LeadershipPositionId == 1 ? 2 :
                                  data.BlindRecruitmentId == 1 ? 3 :
                                  (data.LeadershipPositionId.HasValue || data.BlindRecruitmentId.HasValue) ? 1 : null;
        _form.LockCandidateEvaluation = data.LockCandidateEvaluation;
        _form.ViewCommitteeEvaluations = data.ViewCommitteeEvaluations;
        _form.SendEmailOnNewCandidate = data.SendEmailOnNewCandidate;
        _form.SendDailyStatusMail = data.SendDailyStatusMail;
        _form.ContinuousPosting = data.ContinuousPosting;
        _form.ApplicationDeadline = data.ApplicationDeadline;
        _applicationDeadlineTime = data.ApplicationDeadline?.TimeOfDay;
        _form.HireDateType = data.HireDateType;
        _form.HireDate = data.HireDate;
        _form.HireDateFreeText = data.HireDateFreeText;
        _form.InterviewRounds = data.InterviewRounds;
        _form.CalendarTypeId = data.CalendarTypeId;
        _form.ClosedCalendarEnabled = data.CalendarTypeId == 2;
        _form.OpenCalendarEnabled = data.CalendarTypeId == 1;
        _form.ExtendedEvaluationEnabled = data.ExtendedEvaluationEnabled;
        _form.SendSmsInterviewReminders = data.SendSmsInterviewReminders;
        _form.TemplateGroupId = data.TemplateGroupId;
        _form.ApplicationTemplateId = data.ApplicationTemplateId;
        _form.EmailTemplateReceivedId = data.EmailTemplateReceivedId;
        _form.EmailTemplateInterviewId = data.EmailTemplateInterviewId;
        _form.EmailTemplateInterview2PlusId = data.EmailTemplateInterview2PlusId;
        _form.EmailTemplateRejectedId = data.EmailTemplateRejectedId;
        _form.EmailTemplateRejectedAfterInterviewId = data.EmailTemplateRejectedAfterInterviewId;
        _form.EmailTemplateNotifyCommitteeId = data.EmailTemplateNotifyCommitteeId;
        _form.SmsTemplateInterviewId = data.SmsTemplateInterviewId;
        _form.RecruitmentResponsibleUserId = data.RecruitmentResponsibleUserId;
        _form.AlternativeResponsibleUserIds = data.AlternativeResponsibles.Select(u => u.UserId).ToList();
        _form.InterviewDurationId = data.InterviewDurationId;

        // Sync sidebar display models
        if (data.RecruitmentResponsibleUserId.HasValue && data.RecruitmentResponsibleName != null)
        {
            _responsibleUser = new UserDropdownDto
            {
                UserId = data.RecruitmentResponsibleUserId.Value,
                DisplayName = data.RecruitmentResponsibleName
            };
        }

        _alternativeUsers = data.AlternativeResponsibles.ToList();
        _committeeMembers = data.CommitteeMembers.ToList();

        // Sync autocomplete display for client section
        if (data.ClientSectionId.HasValue && !string.IsNullOrEmpty(data.ClientSectionName))
        {
            _selectedClientSection = new ClientSectionDropdownDto
            {
                ClientSectionId = data.ClientSectionId.Value,
                Name = data.ClientSectionName
            };
        }

        // Sync occupation
        if (data.JobnetOccupationId.HasValue)
        {
            _selectedOccupation = _options.JobnetOccupations
                .FirstOrDefault(o => o.Id == data.JobnetOccupationId.Value);
        }
    }

    // -------------------------------------------------------------------------
    // Form events — client cascade
    // -------------------------------------------------------------------------

    private async Task OnClientChangedAsync(ClientDropdownDto? client)
    {
        // MudAutocomplete fires ValueChanged on first render when Value is already set.
        // Ignore the spurious callback if the client hasn't actually changed.
        if (client?.ClientId == _form.ClientId) return;

        _selectedClient = client;
        _form.ClientId = client?.ClientId;

        // Clear client-dependent selections
        _form.ClientSectionGroupId = null;
        _form.ClientSectionId = null;
        _selectedClientSection = null;
        _selectedOccupation = null;
        _form.JobnetOccupationId = null;
        _form.TemplateGroupId = null;
        _form.ApplicationTemplateId = null;
        _form.EmailTemplateReceivedId = null;
        _form.EmailTemplateInterviewId = null;
        _form.EmailTemplateInterview2PlusId = null;
        _form.EmailTemplateRejectedId = null;
        _form.EmailTemplateRejectedAfterInterviewId = null;
        _form.EmailTemplateNotifyCommitteeId = null;
        _form.SmsTemplateInterviewId = null;

        if (client != null)
        {
            _cascadeLoading = true;
            StateHasChanged();
            try
            {
                await LoadClientDependentDataAsync(client.ClientId);
                if (!IsEditMode && _clientConfig.SendDailyStatusMailEnabled && _clientConfig.SendDailyStatusMailDefaultOn)
                    _form.SendDailyStatusMail = true;
            }
            finally
            {
                _cascadeLoading = false;
            }
        }
        else
        {
            _clientConfig = new ActivityClientConfigDto();
            _options = new ActivityFormOptionsDto();
        }
    }

    private async Task OnSectionGroupChangedAsync(int? groupId)
    {
        _form.ClientSectionGroupId = groupId;
        _form.ClientSectionId = null;
        _selectedClientSection = null;
        if (_clientSectionAutocomplete != null)
            await _clientSectionAutocomplete.ResetAsync();
    }

    private Task OnOccupationChangedAsync(SimpleOptionDto? occupation)
    {
        _selectedOccupation = occupation;
        _form.JobnetOccupationId = occupation?.Id;
        return Task.CompletedTask;
    }

    private Task OnClientSectionChangedAsync(ClientSectionDropdownDto? section)
    {
        _selectedClientSection = section;
        _form.ClientSectionId = section?.ClientSectionId;
        return Task.CompletedTask;
    }

    private async Task OnTemplateGroupChangedAsync(int? templateGroupId)
    {
        _form.TemplateGroupId = templateGroupId;
        _form.ApplicationTemplateId = null;
        _form.EmailTemplateReceivedId = null;
        _form.EmailTemplateInterviewId = null;
        _form.EmailTemplateInterview2PlusId = null;
        _form.EmailTemplateRejectedId = null;
        _form.EmailTemplateRejectedAfterInterviewId = null;
        _form.EmailTemplateNotifyCommitteeId = null;

        if (_form.ClientId.HasValue)
        {
            // Reload templates filtered by template group
            var updatedOptions = await ErActivityService.GetActivityFormOptionsAsync(
                _form.ClientId.Value, templateGroupId);
            _options = _options with
            {
                ApplicationTemplates                 = updatedOptions.ApplicationTemplates,
                EmailTemplatesReceived               = updatedOptions.EmailTemplatesReceived,
                EmailTemplatesInterview              = updatedOptions.EmailTemplatesInterview,
                EmailTemplatesInterview2Plus         = updatedOptions.EmailTemplatesInterview2Plus,
                EmailTemplatesRejected               = updatedOptions.EmailTemplatesRejected,
                EmailTemplatesRejectedAfterInterview = updatedOptions.EmailTemplatesRejectedAfterInterview,
                EmailTemplatesNotifyCommittee        = updatedOptions.EmailTemplatesNotifyCommittee,
            };

            // Yield to let Blazor render the new MudSelectItem components so they register
            // with their parent MudSelect before we set the selected values. Without this,
            // simultaneously changing the items list and the selected value causes MudSelect
            // to fall back to ToString() (showing raw int IDs like "48", "49", "50").
            await Task.Yield();

            AutoSelectSingleTemplateOptions();
        }
    }

    private Task OnLanguagesChangedAsync(IEnumerable<int> selectedIds)
    {
        _form.SelectedLanguageIds = selectedIds.ToList();
        return Task.CompletedTask;
    }

    private void OnApplicationDeadlineTimeChanged(TimeSpan? time)
    {
        _applicationDeadlineTime = time;
        if (_form.ApplicationDeadline.HasValue && time.HasValue)
            _form.ApplicationDeadline = _form.ApplicationDeadline.Value.Date + time.Value;
        else if (_form.ApplicationDeadline.HasValue)
            _form.ApplicationDeadline = _form.ApplicationDeadline.Value.Date;
    }

    // -------------------------------------------------------------------------
    // Autocomplete search functions
    // -------------------------------------------------------------------------

    private Task<IEnumerable<ClientDropdownDto>> SearchClientsAsync(string search, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(search))
            return Task.FromResult<IEnumerable<ClientDropdownDto>>(_clients);

        var filtered = _clients.Where(c =>
            c.ClientName.Contains(search, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(filtered);
    }

    private Task<IEnumerable<SimpleOptionDto>> SearchOccupationsAsync(string search, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(search))
            return Task.FromResult(Enumerable.Empty<SimpleOptionDto>());

        return Task.FromResult(
            _options.JobnetOccupations
                .Where(o => o.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<IEnumerable<ClientSectionDropdownDto>> SearchClientSectionsAsync(string search, CancellationToken ct)
    {
        if (!_form.ClientId.HasValue) return Enumerable.Empty<ClientSectionDropdownDto>();

        var results = await ErActivityService.GetClientSectionsForFormAsync(_form.ClientId.Value, search, ct);
        return results;
    }

    // -------------------------------------------------------------------------
    // Sidebar actions
    // -------------------------------------------------------------------------

    private async Task OnChangeResponsibleAsync()
    {
        var result = await OpenUserPickerAsync(Localization.GetText("RecruitingResponsable"));
        if (result != null)
        {
            _responsibleUser = result;
            _form.RecruitmentResponsibleUserId = result.UserId;
        }
    }

    private async Task OnAddAlternativeResponsibleAsync()
    {
        var result = await OpenUserPickerAsync(Localization.GetText("RecruitingResponsableAlternative"));
        if (result != null && !_form.AlternativeResponsibleUserIds.Contains(result.UserId))
        {
            _alternativeUsers.Add(result);
            _form.AlternativeResponsibleUserIds.Add(result.UserId);
        }
    }

    private void RemoveAlternativeResponsible(Guid userId)
    {
        _alternativeUsers.RemoveAll(u => u.UserId == userId);
        _form.AlternativeResponsibleUserIds.Remove(userId);
    }

    private async Task<UserDropdownDto?> OpenUserPickerAsync(string title)
    {
        if (!_form.ClientId.HasValue) return null;

        var parameters = new DialogParameters<UserPickerDialog>
        {
            { x => x.Title, title },
            { x => x.ClientId, _form.ClientId.Value }
        };

        var dialog = await DialogService.ShowAsync<UserPickerDialog>(
            title,
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not UserDropdownDto user) return null;
        return user;
    }

    // -------------------------------------------------------------------------
    // Save / Delete / Close
    // -------------------------------------------------------------------------

    private async Task OnSaveAsync()
    {
        _saveError = null;
        _saving = true;
        StateHasChanged();

        try
        {
            var command = BuildSaveCommand();

            if (IsEditMode && ActivityId.HasValue)
            {
                await ErActivityService.UpdateActivityAsync(ActivityId.Value, command);
                Navigation.NavigateTo("/recruiting/activities");
            }
            else
            {
                var newId = await ErActivityService.CreateActivityAsync(command);
                Navigation.NavigateTo($"/recruiting/activities/{newId}/edit");
            }
        }
        catch (Exception ex)
        {
            _saveError = $"{Localization.GetText("SaveFailed")}: {ex.Message}";
            _saving = false;
        }
    }

    private Task OnCancelAsync()
    {
        if (IsEditMode && ActivityId.HasValue)
            Navigation.NavigateTo($"/recruiting/activities/{ActivityId.Value}");
        else
            Navigation.NavigateTo("/recruiting/activities");
        return Task.CompletedTask;
    }

    private async Task OnDeleteAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            Localization.GetText("Delete"),
            Localization.GetText("ConfirmDelete"),
            yesText: Localization.GetText("Delete"),
            noText: Localization.GetText("Cancel"),
            options: new DialogOptions { MaxWidth = MaxWidth.ExtraSmall });

        if (confirmed != true) return;

        _saving = true;
        StateHasChanged();

        try
        {
            await ErActivityService.DeleteActivityAsync(ActivityId!.Value);
            Navigation.NavigateTo("/recruiting/activities");
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
            _saving = false;
        }
    }

    private async Task OnCloseActivityAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            Localization.GetText("CloseActivity"),
            Localization.GetText("ConfirmCloseActivity"),
            yesText: Localization.GetText("CloseActivity"),
            noText: Localization.GetText("Cancel"),
            options: new DialogOptions { MaxWidth = MaxWidth.ExtraSmall });

        if (confirmed != true) return;

        _saving = true;
        StateHasChanged();

        try
        {
            await ErActivityService.CloseActivityAsync(ActivityId!.Value);
            Navigation.NavigateTo("/recruiting/activities");
        }
        catch (Exception ex)
        {
            _saveError = ex.Message;
            _saving = false;
        }
    }

    // -------------------------------------------------------------------------
    // Build command
    // -------------------------------------------------------------------------

    private ActivitySaveCommand BuildSaveCommand()
    {
        return new ActivitySaveCommand
        {
            StatusId = _form.StatusId ?? 1,
            ClientId = _form.ClientId ?? Session.ClientId ?? 0,
            Headline = _form.Headline,
            JobTitle = _form.JobTitle,
            JobnetOccupationId = _form.JobnetOccupationId,
            ClientSectionGroupId = _form.ClientSectionGroupId,
            ClientSectionId = _form.ClientSectionId,
            Reposting = _form.Reposting,
            // RecruitmentType fields depend on which dropdown(s) are visible:
            //   Combined → translate RecruitmentTypeId to LP/Blind flags
            //   LP-only  → use LeadershipPositionId directly; BlindRecruitmentId null
            //   Blind-only → use BlindRecruitmentId directly; LeadershipPositionId null
            //   Both separate → use both directly; RecruitmentTypeId null
            //   Neither → all null
            RecruitmentTypeId = ShowCombinedRecruitmentTypeDropdown ? _form.RecruitmentTypeId : null,
            LeadershipPositionId = ShowCombinedRecruitmentTypeDropdown
                ? _form.RecruitmentTypeId switch { 2 => 1, 1 or 3 => 2, _ => null }
                : ShowLeadershipPositionDropdown ? _form.LeadershipPositionId : null,
            BlindRecruitmentId = ShowCombinedRecruitmentTypeDropdown
                ? _form.RecruitmentTypeId switch { 3 => 1, 1 or 2 => 2, _ => null }
                : ShowBlindRecruitmentDropdown ? _form.BlindRecruitmentId : null,
            LockCandidateEvaluation = _form.LockCandidateEvaluation,
            ViewCommitteeEvaluations = _form.ViewCommitteeEvaluations,
            SendEmailOnNewCandidate = _form.SendEmailOnNewCandidate,
            SendDailyStatusMail = _form.SendDailyStatusMail,
            ContinuousPosting = _form.ContinuousPosting,
            ApplicationDeadline = _form.ApplicationDeadline,
            HireDateType = _form.HireDateType,
            HireDate = _form.HireDate,
            HireDateFreeText = _form.HireDateFreeText,
            InterviewRounds = _form.InterviewRounds,
            CalendarTypeId = _form.ClosedCalendarEnabled && _form.OpenCalendarEnabled
                ? _form.CalendarTypeId  // Both checked: use dropdown selection
                : _form.ClosedCalendarEnabled ? 2
                : _form.OpenCalendarEnabled ? 1
                : 0,
            ClosedCalendarEnabled = _form.ClosedCalendarEnabled,
            OpenCalendarEnabled = _form.OpenCalendarEnabled,
            InterviewDurationId = _form.InterviewDurationId,
            SendSmsInterviewReminders = _form.SendSmsInterviewReminders,
            ExtendedEvaluationEnabled = _form.ExtendedEvaluationEnabled,
            SelectedLanguageIds = _form.SelectedLanguageIds,
            TemplateGroupId = _form.TemplateGroupId,
            ApplicationTemplateId = _form.ApplicationTemplateId,
            EmailTemplateReceivedId = _form.EmailTemplateReceivedId,
            EmailTemplateInterviewId = _form.EmailTemplateInterviewId,
            EmailTemplateInterview2PlusId = _form.EmailTemplateInterview2PlusId,
            EmailTemplateRejectedId = _form.EmailTemplateRejectedId,
            EmailTemplateRejectedAfterInterviewId = _form.EmailTemplateRejectedAfterInterviewId,
            EmailTemplateNotifyCommitteeId = _form.EmailTemplateNotifyCommitteeId,
            SmsTemplateInterviewId = _form.SmsTemplateInterviewId,
            RecruitmentResponsibleUserId = _form.RecruitmentResponsibleUserId,
            AlternativeResponsibleUserIds = _form.AlternativeResponsibleUserIds,
            SavedByUserId = _currentUserId,
            SiteId = Session.SiteId ?? 0
        };
    }
}
