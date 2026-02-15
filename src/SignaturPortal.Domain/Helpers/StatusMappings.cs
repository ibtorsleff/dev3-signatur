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
}
