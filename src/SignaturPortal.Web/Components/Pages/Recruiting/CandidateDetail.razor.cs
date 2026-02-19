using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;

namespace SignaturPortal.Web.Components.Pages.Recruiting;

public partial class CandidateDetail
{
    [Parameter] public int ActivityId { get; set; }
    [Parameter] public int CandidateId { get; set; }
    [Inject] private IActivityService ActivityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private CandidateDetailDto? _candidate;
    private bool _loading = true;
    private bool _notFound;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private int? _downloadingFileId;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _candidate = await ActivityService.GetCandidateDetailAsync(ActivityId, CandidateId);
            _loading = false;

            if (_candidate != null)
            {
                _breadcrumbs = new List<BreadcrumbItem>
                {
                    new("Activities", "/recruiting/activities"),
                    new(_candidate.ActivityHeadline, $"/recruiting/activities/{ActivityId}"),
                    new("Candidates", $"/recruiting/activities/{ActivityId}/candidates"),
                    new(_candidate.FullName, null, disabled: true)
                };
            }
            else
            {
                _notFound = true;
            }
        }
        catch (Exception)
        {
            _loading = false;
            _notFound = true;
            _errorMessage = "An error occurred while loading the candidate. Please try again.";
        }
    }

    private async Task DownloadFileAsync(int binaryFileId, string fileName)
    {
        if (_downloadingFileId.HasValue)
            return;

        _downloadingFileId = binaryFileId;

        try
        {
            var fileData = await ActivityService.GetCandidateFileDataAsync(CandidateId, binaryFileId);

            if (fileData == null)
            {
                Snackbar.Add("File not found or access denied.", Severity.Error);
                return;
            }

            var fileStream = new MemoryStream(fileData.Value.FileData);
            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await JSRuntime.InvokeVoidAsync("downloadFileFromStream", fileData.Value.FileName, streamRef);

            Snackbar.Add($"Downloaded {fileName}", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error downloading file: {ex.Message}", Severity.Error);
        }
        finally
        {
            _downloadingFileId = null;
        }
    }
}
