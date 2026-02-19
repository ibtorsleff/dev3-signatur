namespace SignaturPortal.Web.Components.Services;

/// <summary>
/// Scoped service holding the current dark mode state for a Blazor circuit.
/// NavMenu calls Toggle(); MainLayout subscribes to OnChange to update its binding.
/// </summary>
public class ThemeStateService
{
    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    /// <summary>
    /// Initialises the dark mode value from localStorage on first render.
    /// Does not fire OnChange — caller handles the re-render directly.
    /// </summary>
    public void Initialize(bool isDark)
    {
        IsDarkMode = isDark;
    }

    /// <summary>Toggles dark mode and notifies all subscribers.</summary>
    public void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        OnChange?.Invoke();
    }
}
