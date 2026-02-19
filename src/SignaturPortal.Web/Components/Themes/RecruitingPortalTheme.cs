using MudBlazor;

namespace SignaturPortal.Web.Components.Themes;

public static class RecruitingPortalTheme
{
    public const string CssClass = "theme-recruitingportal";

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary          = "#1b9b89",
            PrimaryDarken    = "#073f3c",
            PrimaryLighten   = "#72b9ab",
            Secondary        = "#e6f6f6",
            AppbarBackground = "#1a9b89",
            Background       = "#ffffff",
            Surface          = "#ffffff",
            TextPrimary      = "#000000",
        },
        PaletteDark = new PaletteDark
        {
            Primary          = "#1b9b89",
            PrimaryDarken    = "#72b9ab",
            PrimaryLighten   = "#073f3c",
            AppbarBackground = "#054541",
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
