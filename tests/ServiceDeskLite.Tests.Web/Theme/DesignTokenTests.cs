using System.Text.RegularExpressions;

using FluentAssertions;

using ServiceDeskLite.Web.Theme;

namespace ServiceDeskLite.Tests.Web.Theme;

/// <summary>
/// The token scale only holds if nothing bypasses it. These tests read the shipped
/// stylesheets rather than the theme class, because a literal in a stylesheet is exactly
/// the thing a review of the theme class cannot see.
/// </summary>
public sealed class DesignTokenTests
{
    private static readonly Regex ColorLiteral = new(
        @"#[0-9a-fA-F]{3,8}\b|\brgba?\(",
        RegexOptions.Compiled);

    // Anchored on the start of a declaration, not of a line: a guard that a reformat can
    // silence is not a guard.
    private static readonly Regex SpacingOrRadiusLiteral = new(
        @"(?:^|[;{])\s*(padding|margin|gap|row-gap|column-gap|border[a-z-]*radius)[a-z-]*\s*:[^;}]*?\d+(\.\d+)?(px|rem)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static TheoryData<string> ScopedStylesheets()
    {
        var data = new TheoryData<string>();
        foreach (var path in EnumerateScopedStylesheets())
            data.Add(path);
        return data;
    }

    [Fact]
    public void ScopedStylesheetsExist()
        => EnumerateScopedStylesheets().Should().NotBeEmpty(
            "the other tests here silently pass when they find no files to read");

    [Theory]
    [MemberData(nameof(ScopedStylesheets))]
    public void NoColourLiteralOutsideTheTokens(string path)
    {
        var offenders = File.ReadAllLines(path)
            .Select((line, index) => (line, number: index + 1))
            .Where(x => ColorLiteral.IsMatch(x.line))
            .Select(x => $"{x.number}: {x.line.Trim()}")
            .ToArray();

        offenders.Should().BeEmpty(
            "colour belongs in DesignTokens, not in {0}", Path.GetFileName(path));
    }

    [Theory]
    [MemberData(nameof(ScopedStylesheets))]
    public void NoSpacingOrRadiusLiteralOutsideTheScale(string path)
    {
        var text = File.ReadAllText(path);
        var offenders = SpacingOrRadiusLiteral.Matches(text)
            .Where(m => !m.Value.Contains("calc("))
            .Select(m => m.Value.Trim())
            .ToArray();

        offenders.Should().BeEmpty(
            "spacing and radius come from the token scale, not from {0}", Path.GetFileName(path));
    }

    [Fact]
    public void RootCssDeclaresEveryScaleStep()
    {
        var css = DesignTokens.RootCss;

        css.Should().StartWith(":root{").And.EndWith("}");
        css.Should().Contain("--sdl-space-1:4px").And.Contain("--sdl-space-4:24px");
        css.Should().Contain("--sdl-radius-pill:999px");
        css.Should().Contain("--sdl-status-new-fg:").And.Contain("--sdl-priority-critical-border:");
    }

    [Fact]
    public void ResolvedAndClosedShareOneChip()
        => DesignTokens.Status.Closed.Should().BeSameAs(DesignTokens.Status.Resolved);

    private static IReadOnlyList<string> EnumerateScopedStylesheets()
    {
        var web = Path.Combine(RepositoryRoot(), "src", "ServiceDeskLite.Web");
        return
        [
            .. Directory.EnumerateFiles(Path.Combine(web, "Components"), "*.razor.css", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(Path.Combine(web, "Features"), "*.razor.css", SearchOption.AllDirectories)
        ];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ServiceDeskLite.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate ServiceDeskLite.slnx above the test output directory.");

        return directory.FullName;
    }
}
