namespace SignaturPortal.Domain.Helpers;

public static class StatusMappings
{
    private static readonly Dictionary<int, string> ActivityStatusNames = new()
    {
        { 0, "All" },
        { 1, "Ongoing" },
        { 2, "Closed" },
        { 3, "Deleted" },
        { 4, "Draft" }
    };

    private static readonly Dictionary<int, string> ActivityMemberTypeNames = new()
    {
        { 1, "Internal" },
        { 2, "External" },
        { 3, "External (Draft)" }
    };

    // TODO Phase 5: Load candidate statuses from database with localization
    private static readonly Dictionary<int, string> CandidateStatusNames = new()
    {
        { 1, "Registered" },
        { 2, "Under Review" },
        { 3, "Interview" },
        { 4, "Hired" },
        { 5, "Rejected" }
    };

    /// <summary>
    /// Maps ERActivityStatusId to display name. Returns "Unknown" for unmapped values.
    /// Values match legacy ERActivityStatus table: 1=Ongoing, 2=Closed, 3=Deleted, 4=Draft.
    /// </summary>
    public static string GetActivityStatusName(int statusId)
        => ActivityStatusNames.TryGetValue(statusId, out var name) ? name : "Unknown";

    /// <summary>
    /// Maps ERActivityMemberTypeId to display name. Returns "Unknown" for unmapped values.
    /// Values match legacy ERActivityMemberType: 1=Internal, 2=External, 3=External (Draft).
    /// </summary>
    public static string GetActivityMemberTypeName(int memberTypeId)
        => ActivityMemberTypeNames.TryGetValue(memberTypeId, out var name) ? name : "Unknown";

    /// <summary>
    /// Maps ERCandidateStatusId to display name. Returns "Unknown" for unmapped values.
    /// TODO Phase 5: Replace with database-driven localized status lookup.
    /// </summary>
    public static string GetCandidateStatusName(int statusId)
        => CandidateStatusNames.TryGetValue(statusId, out var name) ? name : "Unknown";
}
