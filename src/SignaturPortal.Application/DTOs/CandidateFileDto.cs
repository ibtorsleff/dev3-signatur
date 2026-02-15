namespace SignaturPortal.Application.DTOs;

/// <summary>
/// Candidate file metadata DTO.
/// Contains file info for display and download but NOT the binary data itself.
/// </summary>
public record CandidateFileDto
{
    public int BinaryFileId { get; init; }
    public string FileName { get; init; } = "";
    public int FileSize { get; init; }
    public string FileSizeFormatted => FormatFileSize(FileSize);

    private static string FormatFileSize(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };
}
