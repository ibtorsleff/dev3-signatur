using MudBlazor;

namespace SignaturPortal.Web.Components.Themes;

public static class AdPortalTheme
{
    public const string CssClass = "theme-adportal";

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary          = "#421a56",
            PrimaryDarken    = "#2c0740",
            PrimaryLighten   = "#7a5e8a",
            Secondary        = "#f0edf2",
            AppbarBackground = "#421a56",
            Background       = "#f5f5f5",
            Surface          = "#ffffff",
            TextPrimary      = "#000000",
        },
        PaletteDark = new PaletteDark
        {
            Primary          = "#7a5e8a",
            PrimaryDarken    = "#421a56",
            PrimaryLighten   = "#a688b8",
            AppbarBackground = "#2c0740",
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
