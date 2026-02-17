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
-- ErrorsExist
-- Used in ActivityList when the server data load fails.
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ErrorsExist' AND LanguageId = 1 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'ErrorsExist', N'An error occurred', -1, 1, 1, 1, @now, @now, 1)

IF NOT EXISTS (SELECT 1 FROM dbo.Localization WHERE [Key] = N'ErrorsExist' AND LanguageId = 3 AND SiteId = -1)
    INSERT INTO dbo.Localization (Area, [Key], [Value], SiteId, [Enabled], LanguageId, LocalizationTypeId, CreateDate, ModifiedDate, Approved)
    VALUES (N'BlazorPortal', N'ErrorsExist', N'Der opstod en fejl', -1, 1, 3, 1, @now, @now, 1)

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
