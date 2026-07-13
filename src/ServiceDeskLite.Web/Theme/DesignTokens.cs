using System.Text;

namespace ServiceDeskLite.Web.Theme;

/// <summary>
/// The single source for every visual constant in the web layer: the palettes MudBlazor
/// renders, and the scales the component stylesheets consume as CSS custom properties.
/// A colour, spacing, radius, or elevation value that is not defined here does not belong
/// in a stylesheet.
/// Colour-bearing tokens live in a <see cref="Scheme"/> with a light and a dark instance;
/// one emitter walks both, so a token cannot exist in one theme and be forgotten in the
/// other.
/// </summary>
public static class DesignTokens
{
    /// <summary>Foreground, background, and border of a semantic chip, kept together so a status cannot own three colours that drift apart.</summary>
    public sealed record ChipColors(string Foreground, string Background, string Border);

    /// <summary>A dashboard tile's accent: saturated enough to carry an icon, unlike the muted chip set.</summary>
    public sealed record AccentColors(string Foreground, string Surface);

    public sealed record SurfaceColors(
        string Background,
        string Default,
        string Muted,
        string Sunken,
        string Translucent,
        string AppBar,
        string Drawer,
        string Scrim);

    /// <summary>The tints and washes that make the application shell read as depth rather than as flat grey.</summary>
    public sealed record BackdropColors(
        string TintTopRight,
        string TintTopLeft,
        string RaisedFrom,
        string RaisedTo,
        string SheenFrom,
        string SheenTo,
        string PanelFrom,
        string PanelTo);

    public sealed record LineColors(
        string Default,
        string Strong,
        string Subtle,
        string Focus,
        string Emphasis);

    public sealed record TextColors(
        string Primary,
        string Secondary,
        string Subtle,
        string OnAccent,
        string Body);

    /// <summary>Icons sit a shade lighter than the text they accompany, and darken as a row is hovered or selected.</summary>
    public sealed record IconColors(
        string Default,
        string Hover,
        string Strong);

    public sealed record BrandColors(
        string Primary,
        string PrimaryHover,
        string Secondary,
        string Info,
        string Success,
        string Warning,
        string Error);

    public sealed record ElevationShadows(
        string Raised,
        string Card,
        string Overlay,
        string Modal,
        string InsetHighlight);

    /// <summary>
    /// Workflow status and priority, as the queue, the board, and the ticket detail all
    /// render them. <c>StatusResolved</c> and <c>StatusClosed</c> deliberately share one
    /// set: both mean "no longer open".
    /// </summary>
    public sealed record ChipSet(
        ChipColors StatusNew,
        ChipColors StatusTriaged,
        ChipColors StatusInProgress,
        ChipColors StatusWaiting,
        ChipColors StatusResolved,
        ChipColors StatusClosed,
        ChipColors PriorityLow,
        ChipColors PriorityMedium,
        ChipColors PriorityHigh,
        ChipColors PriorityCritical,
        ChipColors Category,
        ChipColors Neutral,
        ChipColors Detail);

    /// <summary>Named after the tone a tile carries, so a tile cannot be "the blue one".</summary>
    public sealed record AccentSet(
        AccentColors Neutral,
        AccentColors Info,
        AccentColors Highlight,
        AccentColors Caution,
        AccentColors Critical,
        AccentColors Positive);

    /// <summary>Every colour-bearing token of one theme. What is not in here does not vary between light and dark.</summary>
    public sealed record Scheme(
        SurfaceColors Surface,
        BackdropColors Backdrop,
        LineColors Line,
        TextColors Text,
        IconColors Icon,
        BrandColors Brand,
        ElevationShadows Elevation,
        string FocusRing,
        string ActionDefault,
        string DrawerText,
        ChipSet Chips,
        AccentSet Accents);

    public static class Space
    {
        public const string X1 = "4px";
        public const string X2 = "8px";
        public const string X3 = "16px";
        public const string X4 = "24px";
    }

    public static class Radius
    {
        public const string Small = "8px";
        public const string Medium = "16px";
        public const string Large = "24px";
        public const string Pill = "999px";
    }

    public const string FontMono = "ui-monospace, \"Cascadia Mono\", Consolas, \"SF Mono\", Menlo, monospace";

    public const string MaxWidth = "1660px";
    public const string ContentMaxWidth = "1520px";

    public static Scheme Light { get; } = BuildLight();

    public static Scheme Dark { get; } = BuildDark();

    private static Scheme BuildLight()
    {
        var brand = new BrandColors(
            Primary: "#2F5D8A",
            PrimaryHover: "#264C71",
            Secondary: "#7D8795",
            Info: "#2F6ECA",
            Success: "#2D7D5B",
            Warning: "#B87822",
            Error: "#B85A69");

        var text = new TextColors(
            Primary: "#182230",
            Secondary: "#637083",
            Subtle: "#7A8798",
            OnAccent: "#FFFFFF",
            Body: "#243142");

        return new Scheme(
            Surface: new SurfaceColors(
                Background: "#F4F6F9",
                Default: "#FFFFFF",
                Muted: "#F7F9FB",
                Sunken: "#F2F4F7",
                Translucent: "rgba(255, 255, 255, 0.94)",
                AppBar: "rgba(248, 249, 251, 0.88)",
                Drawer: "#FBFCFD",
                Scrim: "rgba(0, 0, 0, 0.4)"),
            Backdrop: new BackdropColors(
                TintTopRight: "rgba(153, 166, 184, 0.16)",
                TintTopLeft: "rgba(79, 107, 139, 0.08)",
                RaisedFrom: "#EEF3F8",
                RaisedTo: "#E6ECF4",
                SheenFrom: "rgba(246, 248, 251, 0.72)",
                SheenTo: "rgba(255, 255, 255, 0)",
                PanelFrom: "rgba(255, 255, 255, 0.98)",
                PanelTo: "rgba(248, 250, 252, 0.98)"),
            Line: new LineColors(
                Default: "#DDE4EC",
                Strong: "#D5DDE7",
                Subtle: "#E8EDF3",
                Focus: "rgba(102, 125, 156, 0.75)",
                Emphasis: "#BDC9D8"),
            Text: text,
            Icon: new IconColors(
                Default: brand.Secondary,
                Hover: text.Secondary,
                Strong: "#425365"),
            Brand: brand,
            Elevation: new ElevationShadows(
                Raised: "0 2px 6px rgba(15, 23, 42, 0.04)",
                Card: "0 10px 22px rgba(15, 23, 42, 0.05)",
                Overlay: "0 18px 40px rgba(15, 23, 42, 0.06)",
                Modal: "0 3px 6px 2px rgba(0, 0, 0, 0.3)",
                InsetHighlight: "inset 0 1px 0 rgba(255, 255, 255, 0.7)"),
            FocusRing: "rgba(47, 110, 202, 0.28)",
            ActionDefault: "#6F7C8F",
            DrawerText: "#344054",
            Chips: BuildLightChips(),
            Accents: new AccentSet(
                Neutral: new AccentColors("#5F6D80", "#EEF2F6"),
                Info: new AccentColors(brand.Info, "#E9F1FF"),
                Highlight: new AccentColors("#7A63C6", "#F0EDFF"),
                Caution: new AccentColors(brand.Warning, "#FFF4E3"),
                Critical: new AccentColors(brand.Error, "#FDECEF"),
                Positive: new AccentColors(brand.Success, "#E8F6EF")));
    }

    private static ChipSet BuildLightChips()
    {
        var resolved = new ChipColors("#2B7554", "#EDF8F2", "#D5EADF");
        var blue = new ChipColors("#3764B2", "#EDF3FF", "#D8E5FF");
        var amber = new ChipColors("#996017", "#FFF4E2", "#F1DDB2");

        return new ChipSet(
            StatusNew: blue,
            StatusTriaged: new ChipColors("#7558B6", "#F2EDFF", "#E5DCFF"),
            StatusInProgress: amber,
            StatusWaiting: new ChipColors("#7E5D98", "#F4EDF8", "#E5D8EE"),
            StatusResolved: resolved,
            StatusClosed: resolved,
            PriorityLow: new ChipColors("#5F6F84", "#F2F5F8", "#E1E8EF"),
            PriorityMedium: blue,
            PriorityHigh: amber,
            PriorityCritical: new ChipColors("#A34D5E", "#FFF0F3", "#F2D5DD"),
            Category: new ChipColors("#4A5568", "#EEF1F5", "#DBE1E8"),
            Neutral: new ChipColors("#5F6D80", "#EEF2F6", "#DDE4EC"),
            Detail: new ChipColors("#2B6E75", "#EAF6F7", "#D2E9EB"));
    }

    private static Scheme BuildDark()
    {
        // Same hues as light, lifted for contrast against slate surfaces: a colour that
        // carries text on white needs more luminance to carry it on #10151D.
        var brand = new BrandColors(
            Primary: "#6E9CCB",
            PrimaryHover: "#85AEDA",
            Secondary: "#8D99A9",
            Info: "#6FA3E8",
            Success: "#57A87F",
            Warning: "#D69A4E",
            Error: "#D3808E");

        var text = new TextColors(
            Primary: "#E6EBF2",
            Secondary: "#9AA7B7",
            Subtle: "#7E8B9C",
            OnAccent: "#FFFFFF",
            Body: "#D5DDE8");

        return new Scheme(
            Surface: new SurfaceColors(
                Background: "#10151D",
                Default: "#171E28",
                Muted: "#1B232E",
                Sunken: "#121820",
                Translucent: "rgba(23, 30, 40, 0.94)",
                AppBar: "rgba(16, 21, 29, 0.88)",
                Drawer: "#141B24",
                Scrim: "rgba(0, 0, 0, 0.6)"),
            Backdrop: new BackdropColors(
                TintTopRight: "rgba(96, 116, 142, 0.18)",
                TintTopLeft: "rgba(79, 107, 139, 0.12)",
                RaisedFrom: "#1A2330",
                RaisedTo: "#16202B",
                SheenFrom: "rgba(36, 47, 62, 0.72)",
                SheenTo: "rgba(23, 30, 40, 0)",
                PanelFrom: "rgba(25, 33, 44, 0.98)",
                PanelTo: "rgba(20, 27, 36, 0.98)"),
            Line: new LineColors(
                Default: "#2A3542",
                Strong: "#33404F",
                Subtle: "#222C38",
                Focus: "rgba(120, 150, 190, 0.75)",
                Emphasis: "#45566B"),
            Text: text,
            Icon: new IconColors(
                Default: brand.Secondary,
                Hover: text.Secondary,
                Strong: "#B9C4D2"),
            Brand: brand,
            // Shadows barely read on dark surfaces, so they lean harder; the inset
            // highlight dims to a hint or every panel would wear a bright rim.
            Elevation: new ElevationShadows(
                Raised: "0 2px 6px rgba(0, 0, 0, 0.35)",
                Card: "0 10px 22px rgba(0, 0, 0, 0.4)",
                Overlay: "0 18px 40px rgba(0, 0, 0, 0.5)",
                Modal: "0 3px 6px 2px rgba(0, 0, 0, 0.6)",
                InsetHighlight: "inset 0 1px 0 rgba(255, 255, 255, 0.06)"),
            FocusRing: "rgba(111, 163, 232, 0.35)",
            ActionDefault: "#8D99A9",
            DrawerText: "#C3CDD9",
            Chips: BuildDarkChips(),
            Accents: new AccentSet(
                Neutral: new AccentColors("#A9B6C6", "rgba(95, 109, 128, 0.18)"),
                Info: new AccentColors(brand.Info, "rgba(47, 110, 202, 0.16)"),
                Highlight: new AccentColors("#A794E3", "rgba(122, 99, 198, 0.18)"),
                Caution: new AccentColors(brand.Warning, "rgba(184, 120, 34, 0.16)"),
                Critical: new AccentColors(brand.Error, "rgba(184, 90, 105, 0.16)"),
                Positive: new AccentColors(brand.Success, "rgba(45, 125, 91, 0.16)")));
    }

    private static ChipSet BuildDarkChips()
    {
        // Translucent washes instead of the light theme's pastel fills: an opaque pastel
        // would glow on a dark board, a wash keeps the hue while the surface shows through.
        var resolved = new ChipColors("#7FC9A4", "rgba(43, 117, 84, 0.22)", "rgba(84, 160, 124, 0.38)");
        var blue = new ChipColors("#8FB4EF", "rgba(63, 106, 180, 0.20)", "rgba(105, 146, 214, 0.38)");
        var amber = new ChipColors("#E0B36A", "rgba(153, 96, 23, 0.22)", "rgba(196, 148, 80, 0.38)");

        return new ChipSet(
            StatusNew: blue,
            StatusTriaged: new ChipColors("#B4A1E8", "rgba(117, 88, 182, 0.22)", "rgba(150, 124, 210, 0.38)"),
            StatusInProgress: amber,
            StatusWaiting: new ChipColors("#C7A9DD", "rgba(126, 93, 152, 0.22)", "rgba(160, 128, 186, 0.38)"),
            StatusResolved: resolved,
            StatusClosed: resolved,
            PriorityLow: new ChipColors("#A6B4C6", "rgba(95, 111, 132, 0.20)", "rgba(128, 145, 167, 0.36)"),
            PriorityMedium: blue,
            PriorityHigh: amber,
            PriorityCritical: new ChipColors("#E39AA8", "rgba(163, 77, 94, 0.22)", "rgba(200, 116, 132, 0.40)"),
            Category: new ChipColors("#B3BFCE", "rgba(74, 85, 104, 0.28)", "rgba(116, 130, 150, 0.40)"),
            Neutral: new ChipColors("#A9B6C6", "rgba(95, 109, 128, 0.22)", "rgba(128, 143, 162, 0.40)"),
            Detail: new ChipColors("#7CC4CC", "rgba(43, 110, 117, 0.22)", "rgba(80, 150, 158, 0.40)"));
    }

    /// <summary>
    /// Renders the tokens as CSS custom properties: the shared scales and the light
    /// scheme on <c>:root</c>, the dark scheme as overrides under <c>:root.sdl-dark</c>.
    /// The class is set on <c>&lt;html&gt;</c> by the theme script before first paint.
    /// </summary>
    public static string RootCss { get; } = BuildRootCss();

    private static string BuildRootCss()
    {
        var css = new StringBuilder(":root{color-scheme:light;");

        css.Append($"--sdl-space-1:{Space.X1};")
            .Append($"--sdl-space-2:{Space.X2};")
            .Append($"--sdl-space-3:{Space.X3};")
            .Append($"--sdl-space-4:{Space.X4};")
            .Append($"--sdl-radius-sm:{Radius.Small};")
            .Append($"--sdl-radius-md:{Radius.Medium};")
            .Append($"--sdl-radius-lg:{Radius.Large};")
            .Append($"--sdl-radius-pill:{Radius.Pill};")
            .Append($"--sdl-font-mono:{FontMono};")
            .Append($"--sdl-max-width:{MaxWidth};")
            .Append($"--sdl-content-max-width:{ContentMaxWidth};");

        AppendScheme(css, Light);
        css.Append('}');

        css.Append(":root.sdl-dark{color-scheme:dark;");
        AppendScheme(css, Dark);
        css.Append('}');

        return css.ToString();
    }

    // The one code path that turns a scheme into properties. Light and dark cannot
    // diverge in which tokens they define, only in the values.
    private static void AppendScheme(StringBuilder css, Scheme scheme)
    {
        css.Append($"--sdl-elevation-raised:{scheme.Elevation.Raised};")
            .Append($"--sdl-elevation-card:{scheme.Elevation.Card};")
            .Append($"--sdl-elevation-overlay:{scheme.Elevation.Overlay};")
            .Append($"--sdl-elevation-modal:{scheme.Elevation.Modal};")
            .Append($"--sdl-elevation-inset-highlight:{scheme.Elevation.InsetHighlight};")
            .Append($"--sdl-background:{scheme.Surface.Background};")
            .Append($"--sdl-surface:{scheme.Surface.Default};")
            .Append($"--sdl-surface-muted:{scheme.Surface.Muted};")
            .Append($"--sdl-surface-sunken:{scheme.Surface.Sunken};")
            .Append($"--sdl-surface-translucent:{scheme.Surface.Translucent};")
            .Append($"--sdl-surface-appbar:{scheme.Surface.AppBar};")
            .Append($"--sdl-surface-drawer:{scheme.Surface.Drawer};")
            .Append($"--sdl-scrim:{scheme.Surface.Scrim};")
            .Append($"--sdl-backdrop-tint-top-right:{scheme.Backdrop.TintTopRight};")
            .Append($"--sdl-backdrop-tint-top-left:{scheme.Backdrop.TintTopLeft};")
            .Append($"--sdl-backdrop-raised-from:{scheme.Backdrop.RaisedFrom};")
            .Append($"--sdl-backdrop-raised-to:{scheme.Backdrop.RaisedTo};")
            .Append($"--sdl-backdrop-sheen-from:{scheme.Backdrop.SheenFrom};")
            .Append($"--sdl-backdrop-sheen-to:{scheme.Backdrop.SheenTo};")
            .Append($"--sdl-backdrop-panel-from:{scheme.Backdrop.PanelFrom};")
            .Append($"--sdl-backdrop-panel-to:{scheme.Backdrop.PanelTo};")
            .Append($"--sdl-line:{scheme.Line.Default};")
            .Append($"--sdl-line-strong:{scheme.Line.Strong};")
            .Append($"--sdl-line-subtle:{scheme.Line.Subtle};")
            .Append($"--sdl-line-focus:{scheme.Line.Focus};")
            .Append($"--sdl-line-emphasis:{scheme.Line.Emphasis};")
            .Append($"--sdl-icon:{scheme.Icon.Default};")
            .Append($"--sdl-icon-hover:{scheme.Icon.Hover};")
            .Append($"--sdl-icon-strong:{scheme.Icon.Strong};")
            .Append($"--sdl-text:{scheme.Text.Primary};")
            .Append($"--sdl-text-muted:{scheme.Text.Secondary};")
            .Append($"--sdl-text-subtle:{scheme.Text.Subtle};")
            .Append($"--sdl-text-body:{scheme.Text.Body};")
            .Append($"--sdl-text-on-accent:{scheme.Text.OnAccent};")
            .Append($"--sdl-primary:{scheme.Brand.Primary};")
            .Append($"--sdl-primary-hover:{scheme.Brand.PrimaryHover};")
            .Append($"--sdl-info:{scheme.Brand.Info};")
            .Append($"--sdl-success:{scheme.Brand.Success};")
            .Append($"--sdl-warning:{scheme.Brand.Warning};")
            .Append($"--sdl-danger:{scheme.Brand.Error};")
            .Append($"--sdl-focus-ring:{scheme.FocusRing};");

        AppendChip(css, "status-new", scheme.Chips.StatusNew);
        AppendChip(css, "status-triaged", scheme.Chips.StatusTriaged);
        AppendChip(css, "status-inprogress", scheme.Chips.StatusInProgress);
        AppendChip(css, "status-waiting", scheme.Chips.StatusWaiting);
        AppendChip(css, "status-resolved", scheme.Chips.StatusResolved);
        AppendChip(css, "status-closed", scheme.Chips.StatusClosed);
        AppendChip(css, "priority-low", scheme.Chips.PriorityLow);
        AppendChip(css, "priority-medium", scheme.Chips.PriorityMedium);
        AppendChip(css, "priority-high", scheme.Chips.PriorityHigh);
        AppendChip(css, "priority-critical", scheme.Chips.PriorityCritical);
        AppendChip(css, "category", scheme.Chips.Category);
        AppendChip(css, "neutral", scheme.Chips.Neutral);
        AppendChip(css, "detail", scheme.Chips.Detail);

        AppendAccent(css, "neutral", scheme.Accents.Neutral);
        AppendAccent(css, "info", scheme.Accents.Info);
        AppendAccent(css, "highlight", scheme.Accents.Highlight);
        AppendAccent(css, "caution", scheme.Accents.Caution);
        AppendAccent(css, "critical", scheme.Accents.Critical);
        AppendAccent(css, "positive", scheme.Accents.Positive);
    }

    private static void AppendChip(StringBuilder css, string name, ChipColors colors) =>
        css.Append($"--sdl-{name}-fg:{colors.Foreground};")
            .Append($"--sdl-{name}-bg:{colors.Background};")
            .Append($"--sdl-{name}-border:{colors.Border};");

    private static void AppendAccent(StringBuilder css, string name, AccentColors colors) =>
        css.Append($"--sdl-accent-{name}-fg:{colors.Foreground};")
            .Append($"--sdl-accent-{name}-bg:{colors.Surface};");
}
