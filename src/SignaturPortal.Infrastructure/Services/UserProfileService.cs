using System.Text;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Application.DTOs;
using SignaturPortal.Application.Interfaces;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Infrastructure.Services;

/// <summary>
/// Handles user profile loading and saving for the Blazor profile dialog.
/// Migrated from UserProfile.ascx.cs — preserves all business rules for field
/// visibility, required status, email editing, and the full save sequence.
///
/// Client SSO configuration is read from Sig_Client.CustomData XML using the
/// (/ClientCustomData/SSO/@PropertyName) path convention, matching the serialization
/// pattern of legacy ClientCustomData class. Paths default safely to false when
/// the schema does not contain the expected node (not required = permissive).
///
/// NOTE: The following legacy features are deferred and marked as TODO:
///   - MustChangeEmailToWorkEmail (requires Onboarding process data)
///   - Calendar cascade when email changes (HelperERecruiting / HelperOnboarding)
///   - Notification mail/SMS after username change (SendMailHelper)
/// </summary>
public class UserProfileService : IUserProfileService
{
    private readonly IDbContextFactory<SignaturDbContext> _contextFactory;

    public UserProfileService(IDbContextFactory<SignaturDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // -------------------------------------------------------------------------
    // GetProfileAsync
    // -------------------------------------------------------------------------

    public async Task<UserProfileDto?> GetProfileAsync(
        Guid userId, int siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return null;

        bool isInternal = user.IsInternal;
        bool isClientLoggedOn = user.ClientId is > 0;

        // IsSsoUser: legacy AtlantaUser.IsSsoUser maps to a non-empty external identifier.
        // ExtUserId holds the SSO provider's user ID; KombitUuid is the Danish NemLog-in UUID.
        bool isSsoUser = !string.IsNullOrEmpty(user.ExtUserId) || user.KombitUuid.HasValue;

        ClientSsoConfig ssoConfig = new();
        UserEmailDomainConfig emailDomainConfig = new();

        if (isClientLoggedOn && user.ClientId.HasValue)
        {
            ssoConfig = await LoadClientSsoConfigAsync(db, user.ClientId.Value, ct);
            emailDomainConfig = await LoadEmailDomainConfigAsync(db, user.ClientId.Value, isInternal, ct);
        }

        bool userCanEditEmail = ComputeUserCanEditEmail(
            isClientLoggedOn, isInternal, isSsoUser,
            ssoConfig.IsSso, user.Email,
            emailDomainConfig.ClientMailDomains);

        // Required field rules (mirror SetupVarious() in UserProfile.ascx.cs)
        bool ssoRequired = isClientLoggedOn && ssoConfig.IsSso && isSsoUser;

        var config = new UserProfileConfigDto
        {
            ShowWorkArea = isInternal,
            ShowTitle = isInternal,
            WorkAreaRequired = ssoRequired && isInternal && ssoConfig.LoginWorkAreaRequired,
            TitleRequired = ssoRequired && isInternal && ssoConfig.LoginTitelRequired,
            OfficePhoneRequired = ssoRequired && ssoConfig.LoginOfficePhoneRequired,
            CellPhoneRequired = (isClientLoggedOn && ssoConfig.CellPhoneRequired && !isSsoUser)
                                || (ssoRequired && ssoConfig.LoginCellPhoneRequired),
            EmailRequired = userCanEditEmail,
            EmailDisabled = !userCanEditEmail,
            AllowedEmailDomains = emailDomainConfig.AllowedEmailDomains,
            BlockedEmailDomains = emailDomainConfig.BlockedEmailDomains,
            ClientMailDomains = emailDomainConfig.ClientMailDomains,
            // TODO: MustChangeEmailToWorkEmail requires Onboarding process queries
            MustChangeEmailToWorkEmail = false,
        };

        return new UserProfileDto
        {
            UserId = user.UserId,
            WorkArea = user.WorkArea,
            Title = user.Title,
            OfficePhone = user.OfficePhone,
            CellPhone = user.CellPhone,
            Email = user.Email,
            UserName = user.UserName,
            IsInternal = isInternal,
            Config = config,
        };
    }

    // -------------------------------------------------------------------------
    // UpdateProfileAsync
    // -------------------------------------------------------------------------

    public async Task<UserProfileUpdateResult> UpdateProfileAsync(
        UpdateUserProfileCommand command, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == command.UserId, ct);
            if (user is null)
                return UserProfileUpdateResult.Failed("User not found.");

            // -----------------------------------------------------------------
            // Build activity log (compare old vs new — mirrors legacy DoSave)
            // -----------------------------------------------------------------
            var logBuilder = new StringBuilder();

            if (user.IsInternal)
            {
                if (command.WorkArea is not null && user.WorkArea != command.WorkArea)
                    logBuilder.AppendFormat("Arbejdsområde: [{0}], ", command.WorkArea.Trim());

                if (command.Title is not null && user.Title != command.Title)
                    logBuilder.AppendFormat("Titel: [{0}], ", command.Title.Trim());
            }

            if (command.OfficePhone is not null && user.OfficePhone != command.OfficePhone)
                logBuilder.AppendFormat("Kontor tlf: [{0}], ", command.OfficePhone.Trim());

            if (command.CellPhone is not null && user.CellPhone != command.CellPhone)
                logBuilder.AppendFormat("Mobil tlf: [{0}], ", command.CellPhone.Trim());

            // -----------------------------------------------------------------
            // Determine email + username update
            // -----------------------------------------------------------------
            bool doUpdateEmail = command.Email is not null
                && !string.Equals(user.Email, command.Email, StringComparison.OrdinalIgnoreCase);

            bool doUpdateUserName = false;
            string newUserName = string.Empty;

            if (doUpdateEmail && user.UserName is not null && user.Email is not null)
            {
                // Email-based username: update when username contains the old email address
                if (user.UserName.ToLower().Contains(user.Email.ToLower().Trim()))
                {
                    newUserName = await FindAvailableUsernameAsync(
                        db, command.Email!, command.UserId, ct);
                    doUpdateUserName = true;
                }
            }

            if (doUpdateEmail)
            {
                logBuilder.AppendFormat("Mail: [{0}], ", command.Email!.ToLower());
                if (doUpdateUserName)
                    logBuilder.AppendFormat("Brugernavn: [{0}], ", newUserName);
            }

            // -----------------------------------------------------------------
            // Apply field updates
            // -----------------------------------------------------------------
            if (user.IsInternal)
            {
                if (command.WorkArea is not null) user.WorkArea = command.WorkArea.Trim();
                if (command.Title is not null)    user.Title    = command.Title.Trim();
            }

            user.OfficePhone = command.OfficePhone?.Trim();
            user.CellPhone   = command.CellPhone?.Trim();

            string? oldEmail = user.Email;
            if (doUpdateEmail)
            {
                user.Email = command.Email!.ToLower().Trim();

                if (doUpdateUserName)
                    user.UserName = newUserName;
            }

            user.ModifiedDate = DateTime.Now;

            // -----------------------------------------------------------------
            // Sync aspnet_Membership email
            // -----------------------------------------------------------------
            if (doUpdateEmail)
            {
                var membership = await db.AspnetMemberships
                    .FirstOrDefaultAsync(m => m.UserId == command.UserId, ct);

                if (membership is not null)
                {
                    membership.Email = user.Email;
                    membership.LoweredEmail = user.Email?.ToLower();
                }
            }

            // -----------------------------------------------------------------
            // Sync aspnet_Users username
            // -----------------------------------------------------------------
            if (doUpdateUserName)
            {
                var aspnetUser = await db.AspnetUsers
                    .FirstOrDefaultAsync(au => au.UserId == command.UserId, ct);

                if (aspnetUser is not null)
                {
                    aspnetUser.UserName = newUserName;
                    aspnetUser.LoweredUserName = newUserName.ToLower();
                }
            }

            // -----------------------------------------------------------------
            // Activity log entry
            // -----------------------------------------------------------------
            if (logBuilder.Length > 0)
            {
                // Trim trailing ", " and add period — mirrors legacy format
                var logText = "Bruger ændret via rediger profil. "
                    + logBuilder.ToString().TrimEnd(' ', ',') + ".";

                db.UserActivityLogs.Add(new Data.Entities.UserActivityLog
                {
                    ActionUserId = command.UserId,
                    TargetUserId = command.UserId,
                    TimeStamp = DateTime.Now,
                    Log = logText,
                });
            }

            await db.SaveChangesAsync(ct);

            // -----------------------------------------------------------------
            // TODO: Calendar cascade when email changes
            //   Legacy: HelperERecruiting.CandidateInterviewsClosedCalendarUserEmailChangedHandle()
            //           HelperERecruiting.CandidateInterviewsOpenCalendarUserEmailChangedHandle()
            //           HelperOnboarding.OBCalendarMeetingsUserEmailChangedHandle()
            //   Defer until HelperERecruiting / HelperOnboarding are ported to Blazor.
            // -----------------------------------------------------------------

            await tx.CommitAsync(ct);

            // -----------------------------------------------------------------
            // TODO: Send notification mail/SMS after username change
            //   Legacy: SendMailHelper.SendUserNotificationMailAndSms()
            //   Defer until mail/SMS infrastructure is ported.
            // -----------------------------------------------------------------

            return doUpdateUserName
                ? UserProfileUpdateResult.SavedWithUsernameChange(newUserName)
                : UserProfileUpdateResult.Saved();
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mirrors legacy UserCanEditEmail property in UserProfile.ascx.cs.
    /// Returns false when:
    ///   1. User is not client-logged-on, OR user is not internal.
    ///   2. User is an SSO user AND the client is configured as SSO.
    ///   3. User's email domain is in the client's registered mail domains,
    ///      OR the client has no mail domains configured (no-domain → no edit).
    /// Returns true only for internal client users whose email is on a domain
    /// not managed by the client.
    /// </summary>
    private static bool ComputeUserCanEditEmail(
        bool isClientLoggedOn, bool isInternal, bool isSsoUser,
        bool clientIsSso, string? userEmail, IReadOnlyList<string> clientMailDomains)
    {
        if (!isClientLoggedOn || !isInternal)
            return false;

        if (isSsoUser && clientIsSso)
            return false;

        // clientMailDomains.Count == 0 → no domain policy → cannot edit (legacy: NoClientDomains → false)
        if (clientMailDomains.Count == 0)
            return false;

        var userEmailDomain = GetEmailDomain(userEmail ?? "");
        if (clientMailDomains.Contains(userEmailDomain, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Finds the next available username derived from the new email address.
    /// Mirrors legacy MembershipHelper.UserNameAvailableNextGet().
    /// Tries email.ToLower() → email1 → email2 → … until a free slot is found.
    /// </summary>
    private static async Task<string> FindAvailableUsernameAsync(
        SignaturDbContext db, string email, Guid excludeUserId, CancellationToken ct)
    {
        var emailBase = email.ToLower().Trim();
        var candidate = emailBase;
        var counter = 1;

        while (await db.AspnetUsers.AnyAsync(
                   au => au.LoweredUserName == candidate && au.UserId != excludeUserId, ct))
        {
            candidate = emailBase + counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Loads client SSO configuration from Sig_Client.CustomData XML.
    /// Path convention: (/ClientCustomData/SSO/@PropertyName)[1]
    /// Matches legacy ClientCustomData.SSO.* properties.
    /// Returns default (all false) when the client record or XML nodes are not found.
    /// </summary>
    private static async Task<ClientSsoConfig> LoadClientSsoConfigAsync(
        SignaturDbContext db, int clientId, CancellationToken ct)
    {
        try
        {
            var rows = await db.Database.SqlQueryRaw<ClientSsoConfig>(
                @"SELECT
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/SSO/@IsSso)[1]',                              'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS IsSso,
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/SSO/@LoginWorkAreaRequired)[1]',              'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS LoginWorkAreaRequired,
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/SSO/@LoginTitelRequired)[1]',                 'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS LoginTitelRequired,
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/SSO/@LoginOfficePhoneRequired)[1]',           'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS LoginOfficePhoneRequired,
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/SSO/@LoginCellPhoneRequired)[1]',             'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS LoginCellPhoneRequired,
                    CAST(CASE WHEN SC.CustomData.value('(/ClientCustomData/UserSettings/@CellPhoneRequired)[1]',         'NVARCHAR(5)') = 'true' THEN 1 ELSE 0 END AS BIT) AS CellPhoneRequired
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}",
                clientId)
                .ToListAsync(ct);

            return rows.FirstOrDefault() ?? new ClientSsoConfig();
        }
        catch
        {
            // XML path mismatch or missing Sig_Client row — safe default: no requirements
            return new ClientSsoConfig();
        }
    }

    /// <summary>
    /// Loads email domain validation lists from Sig_Client.CustomData XML.
    /// Allowed/blocked domains are stored as comma-separated strings.
    /// Returns empty lists (no domain restrictions) when nodes are not found.
    /// </summary>
    private static async Task<UserEmailDomainConfig> LoadEmailDomainConfigAsync(
        SignaturDbContext db, int clientId, bool isInternal, CancellationToken ct)
    {
        try
        {
            var rows = await db.Database.SqlQueryRaw<EmailDomainRow>(
                @"SELECT
                    ISNULL(SC.CustomData.value('(/ClientCustomData/EmailDomains/AllowedDomainsInternal)[1]', 'NVARCHAR(2000)'), '') AS AllowedDomainsInternal,
                    ISNULL(SC.CustomData.value('(/ClientCustomData/EmailDomains/AllowedDomainsExternal)[1]', 'NVARCHAR(2000)'), '') AS AllowedDomainsExternal,
                    ISNULL(SC.CustomData.value('(/ClientCustomData/EmailDomains/BlockedDomainsInternal)[1]', 'NVARCHAR(2000)'), '') AS BlockedDomainsInternal,
                    ISNULL(SC.CustomData.value('(/ClientCustomData/EmailDomains/BlockedDomainsExternal)[1]', 'NVARCHAR(2000)'), '') AS BlockedDomainsExternal,
                    ISNULL(SC.CustomData.value('(/ClientCustomData/MailDomains)[1]',                         'NVARCHAR(2000)'), '') AS ClientMailDomains
                  FROM Sig_Client SC
                  WHERE SC.ClientId = {0}",
                clientId)
                .ToListAsync(ct);

            var row = rows.FirstOrDefault();
            if (row is null) return new UserEmailDomainConfig();

            return new UserEmailDomainConfig
            {
                AllowedEmailDomains = ParseDomainList(isInternal
                    ? row.AllowedDomainsInternal
                    : row.AllowedDomainsExternal),
                BlockedEmailDomains = ParseDomainList(isInternal
                    ? row.BlockedDomainsInternal
                    : row.BlockedDomainsExternal),
                ClientMailDomains = ParseDomainList(row.ClientMailDomains),
            };
        }
        catch
        {
            // XML path mismatch or missing row — no domain restrictions applied
            return new UserEmailDomainConfig();
        }
    }

    private static List<string> ParseDomainList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static string GetEmailDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..].ToLower() : string.Empty;
    }

    // -------------------------------------------------------------------------
    // Internal projection types for raw SQL queries
    // -------------------------------------------------------------------------

    // ReSharper disable once ClassNeverInstantiated.Local
    private class ClientSsoConfig
    {
        public bool IsSso { get; set; }
        public bool LoginWorkAreaRequired { get; set; }
        public bool LoginTitelRequired { get; set; }
        public bool LoginOfficePhoneRequired { get; set; }
        public bool LoginCellPhoneRequired { get; set; }
        public bool CellPhoneRequired { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class EmailDomainRow
    {
        public string? AllowedDomainsInternal { get; set; }
        public string? AllowedDomainsExternal { get; set; }
        public string? BlockedDomainsInternal { get; set; }
        public string? BlockedDomainsExternal { get; set; }
        public string? ClientMailDomains { get; set; }
    }

    private class UserEmailDomainConfig
    {
        public IReadOnlyList<string> AllowedEmailDomains { get; init; } = [];
        public IReadOnlyList<string> BlockedEmailDomains { get; init; } = [];
        public IReadOnlyList<string> ClientMailDomains { get; init; } = [];
    }
}
