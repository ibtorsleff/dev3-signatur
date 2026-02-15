using System;
using System.Collections.Generic;

namespace SignaturPortal.Infrastructure.Data.Entities;

public partial class BinaryFile
{
    public int BinaryFileId { get; set; }

    public string FileName { get; set; } = null!;

    public int FileSize { get; set; }

    public byte[]? FileData { get; set; }

    public virtual ICollection<Ercandidatefile> Ercandidatefiles { get; set; } = new List<Ercandidatefile>();
}
