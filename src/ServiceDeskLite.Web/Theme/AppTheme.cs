using MudBlazor;
using MudBlazor.Utilities;

namespace ServiceDeskLite.Web.Theme;

public static class AppTheme
{
    /// <summary>The design tokens as <c>:root</c> custom properties, rendered into the document head.</summary>
    public static string RootCss => DesignTokens.RootCss;

    private static readonly string[] FontStack =
    [
        "Aptos",
        "Segoe UI Variable Text",
        "Segoe UI",
        "sans-serif"
    ];

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = new MudColor(DesignTokens.Light.Brand.Primary),
            Secondary = new MudColor(DesignTokens.Light.Brand.Secondary),
            Info = new MudColor(DesignTokens.Light.Brand.Info),
            Success = new MudColor(DesignTokens.Light.Brand.Success),
            Warning = new MudColor(DesignTokens.Light.Brand.Warning),
            Error = new MudColor(DesignTokens.Light.Brand.Error),
            Background = new MudColor(DesignTokens.Light.Surface.Background),
            Surface = new MudColor(DesignTokens.Light.Surface.Default),
            AppbarBackground = new MudColor(DesignTokens.Light.Surface.AppBar),
            AppbarText = new MudColor(DesignTokens.Light.Text.Primary),
            DrawerBackground = new MudColor(DesignTokens.Light.Surface.Drawer),
            DrawerText = new MudColor(DesignTokens.Light.DrawerText),
            TextPrimary = new MudColor(DesignTokens.Light.Text.Primary),
            TextSecondary = new MudColor(DesignTokens.Light.Text.Secondary),
            ActionDefault = new MudColor(DesignTokens.Light.ActionDefault),
            LinesDefault = new MudColor(DesignTokens.Light.Line.Default),
            LinesInputs = new MudColor(DesignTokens.Light.Line.Strong)
        },
        PaletteDark = new PaletteDark
        {
            Primary = new MudColor(DesignTokens.Dark.Brand.Primary),
            Secondary = new MudColor(DesignTokens.Dark.Brand.Secondary),
            Info = new MudColor(DesignTokens.Dark.Brand.Info),
            Success = new MudColor(DesignTokens.Dark.Brand.Success),
            Warning = new MudColor(DesignTokens.Dark.Brand.Warning),
            Error = new MudColor(DesignTokens.Dark.Brand.Error),
            Background = new MudColor(DesignTokens.Dark.Surface.Background),
            Surface = new MudColor(DesignTokens.Dark.Surface.Default),
            AppbarBackground = new MudColor(DesignTokens.Dark.Surface.AppBar),
            AppbarText = new MudColor(DesignTokens.Dark.Text.Primary),
            DrawerBackground = new MudColor(DesignTokens.Dark.Surface.Drawer),
            DrawerText = new MudColor(DesignTokens.Dark.DrawerText),
            TextPrimary = new MudColor(DesignTokens.Dark.Text.Primary),
            TextSecondary = new MudColor(DesignTokens.Dark.Text.Secondary),
            ActionDefault = new MudColor(DesignTokens.Dark.ActionDefault),
            LinesDefault = new MudColor(DesignTokens.Dark.Line.Default),
            LinesInputs = new MudColor(DesignTokens.Dark.Line.Strong)
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = DesignTokens.Radius.Medium,
            AppbarHeight = "72px",
            DrawerWidthLeft = "292px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = FontStack,
                FontSize = "0.96rem",
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "0"
            },
            H3 = new H3Typography
            {
                FontFamily = FontStack,
                FontWeight = "700",
                FontSize = "2.2rem",
                LineHeight = "1.08",
                LetterSpacing = "-0.02em"
            },
            H4 = new H4Typography
            {
                FontFamily = FontStack,
                FontWeight = "700",
                FontSize = "1.8rem",
                LineHeight = "1.14",
                LetterSpacing = "-0.02em"
            },
            H5 = new H5Typography
            {
                FontFamily = FontStack,
                FontWeight = "700",
                FontSize = "1.35rem",
                LineHeight = "1.2",
                LetterSpacing = "-0.01em"
            },
            H6 = new H6Typography
            {
                FontFamily = FontStack,
                FontWeight = "700",
                FontSize = "1.02rem",
                LineHeight = "1.25"
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = FontStack,
                FontWeight = "600",
                FontSize = "1rem",
                LineHeight = "1.5"
            },
            Body1 = new Body1Typography
            {
                FontFamily = FontStack,
                FontSize = "0.96rem",
                LineHeight = "1.6"
            },
            Body2 = new Body2Typography
            {
                FontFamily = FontStack,
                FontSize = "0.9rem",
                LineHeight = "1.55"
            },
            Button = new ButtonTypography
            {
                FontFamily = FontStack,
                FontWeight = "600",
                FontSize = "0.92rem",
                LineHeight = "1.2",
                LetterSpacing = "0",
                TextTransform = "none"
            },
            Caption = new CaptionTypography
            {
                FontFamily = FontStack,
                FontSize = "0.78rem",
                LineHeight = "1.45",
                LetterSpacing = "0"
            },
            Overline = new OverlineTypography
            {
                FontFamily = FontStack,
                FontWeight = "700",
                FontSize = "0.7rem",
                LineHeight = "1.2",
                LetterSpacing = "0.08em",
                TextTransform = "uppercase"
            }
        }
    };
}
