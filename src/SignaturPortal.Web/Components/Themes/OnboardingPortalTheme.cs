using MudBlazor;

namespace SignaturPortal.Web.Components.Themes;

public static class OnboardingPortalTheme
{
    public const string CssClass = "theme-onboarding";

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary          = "#1a6b9b",
            PrimaryDarken    = "#054166",
            PrimaryLighten   = "#5b9bbf",
            Secondary        = "#e6f0f6",
            AppbarBackground = "#1a6b9b",
            Background       = "#f5f5f5",
            Surface          = "#ffffff",
            TextPrimary      = "#000000",
        },
        PaletteDark = new PaletteDark
        {
            Primary          = "#1a6b9b",
            PrimaryDarken    = "#054166",
            PrimaryLighten   = "#5b9bbf",
            AppbarBackground = "#054166",
            Background       = "#121212",
            Surface          = "#1e1e1e",
            TextPrimary      = "#e0e0e0",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Tahoma", "Arial", "sans-serif"]
            }
        }
    };
}
