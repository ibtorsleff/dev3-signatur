using ClosedXML.Excel;
using Microsoft.AspNetCore.SystemWebAdapters;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Domain.Enums;
using System.Web.SessionState;

namespace SignaturPortal.Web.Endpoints;

public static class ActivityExportEndpoints
{
    public static IEndpointRouteBuilder MapActivityExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/activities/export-members", HandleExportAsync)
            .RequireAuthorization("RecruitmentAccess")
            .RequireSystemWebAdapterSession(new SessionAttribute
            {
                SessionBehavior = SessionStateBehavior.ReadOnly
            });

        return endpoints;
    }

    private static async Task<IResult> HandleExportAsync(
        IActivityService activityService,
        IPermissionHelper permHelper,
        ILocalizationService localization,
        int clientId,
        string mode = "ongoing",
        string? clientName = null,
        Guid? createdById = null,
        string? createdByName = null,
        Guid? responsibleId = null,
        string? responsibleName = null,
        int? clientSectionId = null,
        string? clientSectionName = null,
        int? templateGroupId = null,
        string? templateGroupName = null,
        int? clientSectionGroupId = null,
        string? clientSectionGroupName = null,
        string? dateFrom = null,
        string? dateTo = null,
        bool showTemplateGroups = false,
        bool showClientSectionGroups = false,
        CancellationToken ct = default)
    {
        var canExport = await permHelper.UserCanExportActivityMembersAsync(ct);
        if (!canExport)
            return Results.Forbid();

        var status = mode.ToLowerInvariant() switch
        {
            "closed" => ERActivityStatus.Closed,
            "draft" => ERActivityStatus.Draft,
            _ => ERActivityStatus.OnGoing
        };

        ActivityListFilterDto? moreFilters = null;
        DateTime? parsedDateFrom = null;
        DateTime? parsedDateTo = null;

        if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var df))
            parsedDateFrom = df;
        if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var dt))
            parsedDateTo = dt;

        if (createdById.HasValue || responsibleId.HasValue || clientSectionId.HasValue ||
            templateGroupId.HasValue || clientSectionGroupId.HasValue ||
            parsedDateFrom.HasValue || parsedDateTo.HasValue)
        {
            moreFilters = new ActivityListFilterDto
            {
                CreatedByUserId = createdById,
                RecruitmentResponsibleUserId = responsibleId,
                ClientSectionId = clientSectionId,
                TemplateGroupId = templateGroupId,
                ClientSectionGroupId = clientSectionGroupId,
                DateFrom = parsedDateFrom,
                DateTo = parsedDateTo
            };
        }

        var members = await activityService.GetActivityMembersForExportAsync(clientId, status, moreFilters, ct);

        var timestamp = DateTime.Now;
        using var workbook = new XLWorkbook();

        BuildCriteriaSheet(workbook, localization, timestamp, clientName, mode,
            createdByName, responsibleName, clientSectionName,
            templateGroupName, showTemplateGroups,
            clientSectionGroupName, showClientSectionGroups,
            parsedDateFrom, parsedDateTo);

        BuildMembersSheet(workbook, localization, members);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();

        var fileName = $"ActivityMembers_{timestamp:yyyyddMM_HHmmss}.xlsx";
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static void BuildCriteriaSheet(
        XLWorkbook workbook,
        ILocalizationService localization,
        DateTime timestamp,
        string? clientName,
        string mode,
        string? createdByName,
        string? responsibleName,
        string? clientSectionName,
        string? templateGroupName,
        bool showTemplateGroups,
        string? clientSectionGroupName,
        bool showClientSectionGroups,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var sheet = workbook.Worksheets.Add(localization.GetText("Criterias"));

        sheet.Cell(1, 1).Value = localization.GetText("Criteria");
        sheet.Cell(1, 2).Value = localization.GetText("Value");
        ApplyHeaderStyle(sheet.Row(1));

        int row = 2;

        void AddRow(string label, string value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.Font.Bold = true;
            sheet.Cell(row, 2).Style.Font.FontColor = XLColor.FromHtml("#606060");
            row++;
        }

        AddRow(localization.GetText("PrintDate"), timestamp.ToString("dd-MM-yyyy HH:mm:ss"));

        if (!string.IsNullOrEmpty(clientName))
            AddRow(localization.GetText("Client"), clientName);

        var statusLabel = mode.ToLowerInvariant() switch
        {
            "closed" => localization.GetText("Closed"),
            "draft" => localization.GetText("Draft"),
            _ => localization.GetText("Ongoing")
        };
        AddRow(localization.GetText("ActivityState"), statusLabel);

        AddRow(localization.GetText("CreatedBy"),
            string.IsNullOrEmpty(createdByName) ? localization.GetText("All") : createdByName);
        AddRow(localization.GetText("RecruitingResponsable"),
            string.IsNullOrEmpty(responsibleName) ? localization.GetText("All") : responsibleName);
        AddRow(localization.GetText("ClientSection"),
            string.IsNullOrEmpty(clientSectionName) ? localization.GetText("All") : clientSectionName);

        if (showTemplateGroups)
            AddRow(localization.GetText("TemplateGroup"),
                string.IsNullOrEmpty(templateGroupName) ? localization.GetText("All") : templateGroupName);

        if (showClientSectionGroups)
            AddRow(localization.GetText("ClientSectionGroup"),
                string.IsNullOrEmpty(clientSectionGroupName) ? localization.GetText("All") : clientSectionGroupName);

        if (dateFrom.HasValue)
            AddRow(localization.GetText("From"), dateFrom.Value.ToString("dd-MM-yyyy"));
        if (dateTo.HasValue)
            AddRow(localization.GetText("To"), dateTo.Value.ToString("dd-MM-yyyy"));

        sheet.Column(1).AdjustToContents();
        sheet.Column(2).AdjustToContents();
    }

    private static void BuildMembersSheet(
        XLWorkbook workbook,
        ILocalizationService localization,
        List<ActivityMemberExportRow> members)
    {
        var sheet = workbook.Worksheets.Add(localization.GetText("ActivityMembers"));

        // Column order matches legacy: Sags-id, Name, Email, Roles, Responsible, AltResponsible, CommitteeMember, Internal, Active
        sheet.Cell(1, 1).Value = "Sags-id";
        sheet.Cell(1, 2).Value = localization.GetText("Name");
        sheet.Cell(1, 3).Value = localization.GetText("Email");
        sheet.Cell(1, 4).Value = localization.GetText("Roles");
        sheet.Cell(1, 5).Value = localization.GetText("ERRecruitmentResponsible");
        sheet.Cell(1, 6).Value = localization.GetText("AlternativeRecruitmentResponsible");
        sheet.Cell(1, 7).Value = localization.GetText("ERecruitmentRecruitmentCommitteMember");
        sheet.Cell(1, 8).Value = localization.GetText("Internal");
        sheet.Cell(1, 9).Value = localization.GetText("Active");
        ApplyHeaderStyle(sheet.Row(1));

        if (members.Count == 0)
        {
            sheet.Columns().AdjustToContents();
            return;
        }

        var yes = localization.GetText("Yes");
        var no = localization.GetText("No");

        int row = 2;
        foreach (var member in members)
        {
            sheet.Cell(row, 1).Value = member.ActivityId;
            sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(member.FullName) ? "-" : member.FullName;
            sheet.Cell(row, 3).Value = string.IsNullOrWhiteSpace(member.Email) ? "-" : member.Email;
            sheet.Cell(row, 4).Value = string.IsNullOrWhiteSpace(member.InRoles) ? "-" : member.InRoles;
            sheet.Cell(row, 5).Value = member.IsResponsible ? yes : no;
            sheet.Cell(row, 6).Value = member.IsResponsibleAlternative ? yes : no;
            sheet.Cell(row, 7).Value = member.IsMember ? yes : no;
            sheet.Cell(row, 8).Value = member.IsInternal ? yes : no;
            sheet.Cell(row, 9).Value = member.IsActive ? yes : no;
            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void ApplyHeaderStyle(IXLRow row)
    {
        row.Style.Font.Bold = true;
        row.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        row.Style.Border.BottomBorderColor = XLColor.Black;
    }
}
