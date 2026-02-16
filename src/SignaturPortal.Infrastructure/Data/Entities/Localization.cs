namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for the Localization table.
/// Composite PK: (Key, LanguageId, SiteId).
/// Id is an identity column but NOT the primary key.
/// </summary>
public class Localization
{
    public int Id { get; set; }

    public string Area { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public int SiteId { get; set; }

    public bool Enabled { get; set; }

    public int LanguageId { get; set; }

    public int LocalizationTypeId { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public bool Approved { get; set; }
}
