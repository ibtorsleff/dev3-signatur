namespace SignaturPortal.Infrastructure.Data.Entities;

/// <summary>
/// Lookup entity for web ad visitor statistics.
/// Joined via ERActivity.WebAdId → WebAdVisitors.WebAdId.
/// </summary>
public partial class WebAdVisitor
{
    public int WebAdId { get; set; }
    public int Visitors { get; set; }
}
