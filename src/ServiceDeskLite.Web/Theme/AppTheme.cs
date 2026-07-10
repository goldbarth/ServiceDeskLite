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
            Primary = new MudColor(DesignTokens.Brand.Primary),
            Secondary = new MudColor(DesignTokens.Brand.Secondary),
            Info = new MudColor(DesignTokens.Brand.Info),
            Success = new MudColor(DesignTokens.Brand.Success),
            Warning = new MudColor(DesignTokens.Brand.Warning),
            Error = new MudColor(DesignTokens.Brand.Error),
            Background = new MudColor(DesignTokens.Surface.Background),
            Surface = new MudColor(DesignTokens.Surface.Default),
            AppbarBackground = new MudColor(DesignTokens.Surface.AppBar),
            AppbarText = new MudColor(DesignTokens.Text.Primary),
            DrawerBackground = new MudColor(DesignTokens.Surface.Drawer),
            DrawerText = new MudColor("#344054"),
            TextPrimary = new MudColor(DesignTokens.Text.Primary),
            TextSecondary = new MudColor(DesignTokens.Text.Secondary),
            ActionDefault = new MudColor("#6F7C8F"),
            LinesDefault = new MudColor(DesignTokens.Line.Default),
            LinesInputs = new MudColor(DesignTokens.Line.Strong)
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
