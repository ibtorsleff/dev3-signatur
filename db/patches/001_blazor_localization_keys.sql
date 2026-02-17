-- ============================================================
-- 001_blazor_localization_keys.sql
-- Adds Blazor-portal-specific localization keys that do not
-- exist in the legacy application.
--
-- Safe to re-run: every INSERT is guarded by IF NOT EXISTS.
-- SiteId = -1  => global (not site-specific)
-- LanguageId 1 = EN, 3 = DK
-- ============================================================

DECLARE @now DATETIME = GETDATE()

-- ------------------------------------------------------------
-- ShowFilter
-- Used in ActivityList when the server data load fails.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ShowFilter' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'ShowFilter', N'Show filter', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ShowFilter' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'ShowFilter', N'Vis filter', -1, 1, 3, 1, @now, @now, 1)

-- ------------------------------------------------------------
-- HideFilter
-- Used in ActivityList when the server data load fails.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'HideFilter' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'HideFilter', N'Hide filter', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'HideFilter' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'HideFilter', N'Skjul filter', -1, 1, 3, 1, @now, @now, 1)


-- ------------------------------------------------------------
-- ErrorLoadingActivities 
-- Used in ActivityList when the server data load fails.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ErrorLoadingActivities' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'ErrorLoadingActivities ', N'Error loading activities', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ErrorLoadingActivities ' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'General', N'ErrorLoadingActivities ', N'Fejl under indlæsning af aktiviteter', -1, 1, 3, 1, @now, @now, 1)


-- ------------------------------------------------------------
-- CacheManagement
-- Used on the Blazor admin cache status page (/admin/cache).
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'CacheManagement' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'CacheManagement', N'Cache Management', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'CacheManagement' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'CacheManagement', N'Cache-administration', -1, 1, 3, 1, @now, @now, 1)

-- ------------------------------------------------------------
-- Of
-- Used in MudDataGridPager InfoFormat: "{first_item}-{last_item} of {all_items}"
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'Of' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'Of', N'of', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'Of' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'Of', N'af', -1, 1, 3, 1, @now, @now, 1)

-- ------------------------------------------------------------
-- RowsPerPage
-- Used in MudDataGridPager RowsPerPageString.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'RowsPerPage' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'RowsPerPage', N'Rows per page:', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'RowsPerPage' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'RowsPerPage', N'Rækker pr. side:', -1, 1, 3, 1, @now, @now, 1)
