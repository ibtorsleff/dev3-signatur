namespace SignaturPortal.Application.Interfaces;

/// <summary>
/// Provides localized text lookup from the Localization database table.
/// Mirrors the legacy BasePage.GetText / Globalization.Get methods.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets localized text for the current user's language (from IUserSessionContext.UserLanguageId).
    /// </summary>
    string GetText(string key);

    /// <summary>
    /// Gets localized text for a specific language.
    /// </summary>
    string GetText(string key, int languageId);

    /// <summary>
    /// Gets localized text with string.Format arguments, using the current user's language.
    /// </summary>
    string GetText(string key, params object[] args);

    /// <summary>
    /// Gets localized text with string.Format arguments, for a specific language.
    /// </summary>
    string GetText(string key, int languageId, params object[] args);

    /// <summary>
    /// Checks if a translation exists for the given key (using current user's language).
    /// </summary>
    bool TextExists(string key);

    /// <summary>
    /// Checks if a translation exists for the given key and language.
    /// </summary>
    bool TextExists(string key, int languageId);
}
