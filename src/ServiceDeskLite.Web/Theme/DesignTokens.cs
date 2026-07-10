using System.Text;

namespace ServiceDeskLite.Web.Theme;

/// <summary>
/// The single source for every visual constant in the web layer: the palette MudBlazor
/// renders, and the scales the component stylesheets consume as CSS custom properties.
/// A colour, spacing, radius, or elevation value that is not defined here does not belong
/// in a stylesheet.
/// </summary>
public static class DesignTokens
{
    /// <summary>Foreground, background, and border of a semantic chip, kept together so a status cannot own three colours that drift apart.</summary>
    public sealed record ChipColors(string Foreground, string Background, string Border);

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

    public static class Elevation
    {
        public const string Raised = "0 2px 6px rgba(15, 23, 42, 0.04)";
        public const string Card = "0 10px 22px rgba(15, 23, 42, 0.05)";
        public const string Overlay = "0 18px 40px rgba(15, 23, 42, 0.06)";

        /// <summary>A modal floats above the scrim, not above the page, so it casts a harder shadow than any card.</summary>
        public const string Modal = "0 3px 6px 2px rgba(0, 0, 0, 0.3)";

        /// <summary>A one-pixel light edge along the top of a panel, so it reads as lit from above.</summary>
        public const string InsetHighlight = "inset 0 1px 0 rgba(255, 255, 255, 0.7)";
    }

    public static class Surface
    {
        public const string Background = "#F4F6F9";
        public const string Default = "#FFFFFF";
        public const string Muted = "#F7F9FB";
        public const string Sunken = "#F2F4F7";
        public const string Translucent = "rgba(255, 255, 255, 0.94)";
        public const string AppBar = "rgba(248, 249, 251, 0.88)";
        public const string Drawer = "#FBFCFD";

        /// <summary>Dims the page behind a modal.</summary>
        public const string Scrim = "rgba(0, 0, 0, 0.4)";
    }

    /// <summary>The tints and washes that make the application shell read as depth rather than as flat grey.</summary>
    public static class Backdrop
    {
        public const string TintTopRight = "rgba(153, 166, 184, 0.16)";
        public const string TintTopLeft = "rgba(79, 107, 139, 0.08)";
        public const string RaisedFrom = "#EEF3F8";
        public const string RaisedTo = "#E6ECF4";

        /// <summary>The diagonal sheen across a hero panel, and the vertical wash beneath it.</summary>
        public const string SheenFrom = "rgba(246, 248, 251, 0.72)";
        public const string SheenTo = "rgba(255, 255, 255, 0)";
        public const string PanelFrom = "rgba(255, 255, 255, 0.98)";
        public const string PanelTo = "rgba(248, 250, 252, 0.98)";
    }

    public static class Line
    {
        public const string Default = "#DDE4EC";
        public const string Strong = "#D5DDE7";
        public const string Subtle = "#E8EDF3";
        public const string Focus = "rgba(102, 125, 156, 0.75)";

        /// <summary>Drag-and-drop affordances need a border the eye catches without a colour change.</summary>
        public const string Emphasis = "#BDC9D8";
    }

    public static class Text
    {
        public const string Primary = "#182230";
        public const string Secondary = "#637083";
        public const string Subtle = "#7A8798";
        public const string OnAccent = "#FFFFFF";

        /// <summary>Long-form copy sits a touch lighter than a heading, so a wall of text does not read as bold.</summary>
        public const string Body = "#243142";
    }

    /// <summary>Icons sit a shade lighter than the text they accompany, and darken as a row is hovered or selected.</summary>
    public static class Icon
    {
        public const string Default = Brand.Secondary;
        public const string Hover = Text.Secondary;
        public const string Strong = "#425365";
    }

    public static class Brand
    {
        public const string Primary = "#2F5D8A";
        public const string PrimaryHover = "#264C71";
        public const string Secondary = "#7D8795";
        public const string Info = "#2F6ECA";
        public const string Success = "#2D7D5B";
        public const string Warning = "#B87822";
        public const string Error = "#B85A69";
    }

    public static class Focus
    {
        public const string Ring = "rgba(47, 110, 202, 0.28)";
    }

    public const string FontMono = "ui-monospace, \"Cascadia Mono\", Consolas, \"SF Mono\", Menlo, monospace";

    public const string MaxWidth = "1660px";
    public const string ContentMaxWidth = "1520px";

    /// <summary>
    /// Workflow status, as the queue, the board, and the ticket detail all render it.
    /// <c>Resolved</c> and <c>Closed</c> deliberately share one set: both mean "no longer open".
    /// </summary>
    public static class Status
    {
        public static readonly ChipColors New = new("#3764B2", "#EDF3FF", "#D8E5FF");
        public static readonly ChipColors Triaged = new("#7558B6", "#F2EDFF", "#E5DCFF");
        public static readonly ChipColors InProgress = new("#996017", "#FFF4E2", "#F1DDB2");
        public static readonly ChipColors Waiting = new("#7E5D98", "#F4EDF8", "#E5D8EE");
        public static readonly ChipColors Resolved = new("#2B7554", "#EDF8F2", "#D5EADF");
        public static readonly ChipColors Closed = Resolved;
    }

    public static class Priority
    {
        public static readonly ChipColors Low = new("#5F6F84", "#F2F5F8", "#E1E8EF");
        public static readonly ChipColors Medium = new("#3764B2", "#EDF3FF", "#D8E5FF");
        public static readonly ChipColors High = new("#996017", "#FFF4E2", "#F1DDB2");
        public static readonly ChipColors Critical = new("#A34D5E", "#FFF0F3", "#F2D5DD");
    }

    public static readonly ChipColors Category = new("#4A5568", "#EEF1F5", "#DBE1E8");
    public static readonly ChipColors Neutral = new("#5F6D80", "#EEF2F6", "#DDE4EC");

    /// <summary>An edit to a ticket's fields, as the history timeline marks it. The one hue no status owns.</summary>
    public static readonly ChipColors Detail = new("#2B6E75", "#EAF6F7", "#D2E9EB");

    /// <summary>A dashboard tile's accent: saturated enough to carry an icon, unlike the muted chip set.</summary>
    public sealed record AccentColors(string Foreground, string Surface);

    /// <summary>Named after the tone a tile carries, so a tile cannot be "the blue one".</summary>
    public static class Accent
    {
        public static readonly AccentColors Neutral = new("#5F6D80", "#EEF2F6");
        public static readonly AccentColors Info = new(Brand.Info, "#E9F1FF");
        public static readonly AccentColors Highlight = new("#7A63C6", "#F0EDFF");
        public static readonly AccentColors Caution = new(Brand.Warning, "#FFF4E3");
        public static readonly AccentColors Critical = new(Brand.Error, "#FDECEF");
        public static readonly AccentColors Positive = new(Brand.Success, "#E8F6EF");
    }

    /// <summary>
    /// Renders the tokens as <c>:root</c> custom properties. MudBlazor emits its palette
    /// the same way; this keeps the stylesheets reading from one place rather than two.
    /// </summary>
    public static string RootCss { get; } = BuildRootCss();

    private static string BuildRootCss()
    {
        var css = new StringBuilder(":root{");

        css.Append($"--sdl-space-1:{Space.X1};")
            .Append($"--sdl-space-2:{Space.X2};")
            .Append($"--sdl-space-3:{Space.X3};")
            .Append($"--sdl-space-4:{Space.X4};")
            .Append($"--sdl-radius-sm:{Radius.Small};")
            .Append($"--sdl-radius-md:{Radius.Medium};")
            .Append($"--sdl-radius-lg:{Radius.Large};")
            .Append($"--sdl-radius-pill:{Radius.Pill};")
            .Append($"--sdl-elevation-raised:{Elevation.Raised};")
            .Append($"--sdl-elevation-card:{Elevation.Card};")
            .Append($"--sdl-elevation-overlay:{Elevation.Overlay};")
            .Append($"--sdl-elevation-modal:{Elevation.Modal};")
            .Append($"--sdl-elevation-inset-highlight:{Elevation.InsetHighlight};")
            .Append($"--sdl-background:{Surface.Background};")
            .Append($"--sdl-surface:{Surface.Default};")
            .Append($"--sdl-surface-muted:{Surface.Muted};")
            .Append($"--sdl-surface-sunken:{Surface.Sunken};")
            .Append($"--sdl-surface-translucent:{Surface.Translucent};")
            .Append($"--sdl-surface-appbar:{Surface.AppBar};")
            .Append($"--sdl-surface-drawer:{Surface.Drawer};")
            .Append($"--sdl-scrim:{Surface.Scrim};")
            .Append($"--sdl-backdrop-tint-top-right:{Backdrop.TintTopRight};")
            .Append($"--sdl-backdrop-tint-top-left:{Backdrop.TintTopLeft};")
            .Append($"--sdl-backdrop-raised-from:{Backdrop.RaisedFrom};")
            .Append($"--sdl-backdrop-raised-to:{Backdrop.RaisedTo};")
            .Append($"--sdl-backdrop-sheen-from:{Backdrop.SheenFrom};")
            .Append($"--sdl-backdrop-sheen-to:{Backdrop.SheenTo};")
            .Append($"--sdl-backdrop-panel-from:{Backdrop.PanelFrom};")
            .Append($"--sdl-backdrop-panel-to:{Backdrop.PanelTo};")
            .Append($"--sdl-line:{Line.Default};")
            .Append($"--sdl-line-strong:{Line.Strong};")
            .Append($"--sdl-line-subtle:{Line.Subtle};")
            .Append($"--sdl-line-focus:{Line.Focus};")
            .Append($"--sdl-line-emphasis:{Line.Emphasis};")
            .Append($"--sdl-icon:{Icon.Default};")
            .Append($"--sdl-icon-hover:{Icon.Hover};")
            .Append($"--sdl-icon-strong:{Icon.Strong};")
            .Append($"--sdl-text:{Text.Primary};")
            .Append($"--sdl-text-muted:{Text.Secondary};")
            .Append($"--sdl-text-subtle:{Text.Subtle};")
            .Append($"--sdl-text-body:{Text.Body};")
            .Append($"--sdl-text-on-accent:{Text.OnAccent};")
            .Append($"--sdl-primary:{Brand.Primary};")
            .Append($"--sdl-primary-hover:{Brand.PrimaryHover};")
            .Append($"--sdl-info:{Brand.Info};")
            .Append($"--sdl-success:{Brand.Success};")
            .Append($"--sdl-warning:{Brand.Warning};")
            .Append($"--sdl-danger:{Brand.Error};")
            .Append($"--sdl-focus-ring:{Focus.Ring};")
            .Append($"--sdl-font-mono:{FontMono};")
            .Append($"--sdl-max-width:{MaxWidth};")
            .Append($"--sdl-content-max-width:{ContentMaxWidth};");

        AppendChip(css, "status-new", Status.New);
        AppendChip(css, "status-triaged", Status.Triaged);
        AppendChip(css, "status-inprogress", Status.InProgress);
        AppendChip(css, "status-waiting", Status.Waiting);
        AppendChip(css, "status-resolved", Status.Resolved);
        AppendChip(css, "status-closed", Status.Closed);
        AppendChip(css, "priority-low", Priority.Low);
        AppendChip(css, "priority-medium", Priority.Medium);
        AppendChip(css, "priority-high", Priority.High);
        AppendChip(css, "priority-critical", Priority.Critical);
        AppendChip(css, "category", Category);
        AppendChip(css, "neutral", Neutral);
        AppendChip(css, "detail", Detail);

        AppendAccent(css, "neutral", Accent.Neutral);
        AppendAccent(css, "info", Accent.Info);
        AppendAccent(css, "highlight", Accent.Highlight);
        AppendAccent(css, "caution", Accent.Caution);
        AppendAccent(css, "critical", Accent.Critical);
        AppendAccent(css, "positive", Accent.Positive);

        return css.Append('}').ToString();
    }

    private static void AppendChip(StringBuilder css, string name, ChipColors colors) =>
        css.Append($"--sdl-{name}-fg:{colors.Foreground};")
            .Append($"--sdl-{name}-bg:{colors.Background};")
            .Append($"--sdl-{name}-border:{colors.Border};");

    private static void AppendAccent(StringBuilder css, string name, AccentColors colors) =>
        css.Append($"--sdl-accent-{name}-fg:{colors.Foreground};")
            .Append($"--sdl-accent-{name}-bg:{colors.Surface};");
}
