using SignaturPortal.Infrastructure.Services;

namespace SignaturPortal.Tests.Recruiting;

/// <summary>
/// Unit tests for ErActivityService.BuildWebAdChangeSummary.
/// This method builds the tooltip text for Icon 3 (web ad changes) by
/// collecting distinct non-mail field names in SortOrder/TimeStamp order.
/// </summary>
public class BuildWebAdChangeSummaryTests
{
    private static ErActivityService.ActivityWebAdChangeRow Row(int actId, string fieldName, bool isMail = false)
        => new() { ERActivityId = actId, FieldName = fieldName, IsMail = isMail };

    [Test]
    public async Task EmptyList_ReturnsEmptyString()
    {
        var result = ErActivityService.BuildWebAdChangeSummary([]);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SingleNonMailChange_ReturnsFieldName()
    {
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline"),
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        await Assert.That(result).IsEqualTo("Headline");
    }

    [Test]
    public async Task MultipleDistinctFields_ReturnsCommaJoined()
    {
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline"),
            Row(1, "JobTitle"),
            Row(1, "Description"),
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        await Assert.That(result).IsEqualTo("Headline, JobTitle, Description");
    }

    [Test]
    public async Task DuplicateFieldName_DeduplicatedToFirstOccurrence()
    {
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline"),
            Row(1, "Headline"), // duplicate — should be ignored
            Row(1, "JobTitle"),
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        // "Headline" appears only once; "JobTitle" follows
        await Assert.That(result).IsEqualTo("Headline, JobTitle");
    }

    [Test]
    public async Task MailOnlyChanges_Excluded()
    {
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline", isMail: true),  // IsMail=true — must be excluded
            Row(1, "JobTitle", isMail: true),
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task MixOfMailAndNonMail_OnlyNonMailIncluded()
    {
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline", isMail: false),
            Row(1, "MailBody", isMail: true),  // excluded
            Row(1, "Description", isMail: false),
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        await Assert.That(result).IsEqualTo("Headline, Description");
    }

    [Test]
    public async Task FieldNameComparisonIsCaseInsensitive()
    {
        // "headline" and "Headline" are the same field — only first is kept
        var changes = new List<ErActivityService.ActivityWebAdChangeRow>
        {
            Row(1, "Headline"),
            Row(1, "headline"), // case-insensitive duplicate
        };

        var result = ErActivityService.BuildWebAdChangeSummary(changes);

        await Assert.That(result).IsEqualTo("Headline");
    }
}
