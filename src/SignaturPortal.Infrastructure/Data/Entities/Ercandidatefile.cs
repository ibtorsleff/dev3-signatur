using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class Ercandidatefile
{
    public int BinaryFileId { get; set; }

    public int ErcandidateId { get; set; }

    public int ErcandidateFileConversionStatusId { get; set; }

    public string? ConvertedFileName { get; set; }

    public int? ConvertedFileSize { get; set; }

    public string? ConversionErrorMessage { get; set; }

    public string? ConversionErrorMessageDetails { get; set; }

    public int? EruploadCategoryClientId { get; set; }

    public string? FileException { get; set; }

    public virtual BinaryFile BinaryFile { get; set; } = null!;

    public virtual Ercandidate Ercandidate { get; set; } = null!;
}
