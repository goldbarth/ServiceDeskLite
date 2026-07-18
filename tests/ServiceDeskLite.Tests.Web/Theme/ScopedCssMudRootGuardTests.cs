using System.Text.RegularExpressions;

using FluentAssertions;

namespace ServiceDeskLite.Tests.Web.Theme;

/// <summary>
/// Blazor CSS isolation stamps the scope attribute (<c>b-xxxxx</c>) onto the HTML elements a
/// component writes, but never onto a child component's root. So a plain scoped rule like
/// <c>.foo { ... }</c> where the sibling <c>.razor</c> puts <c>foo</c> on a
/// <c>&lt;MudPaper Class="foo"&gt;</c> compiles to <c>.foo[b-xxxxx]</c> and matches nothing -
/// the style silently dies (issue #215, #242). The fix is either <c>::deep .foo</c> (anchored on
/// a plain wrapper that carries the scope) or deleting the rule if it was inert.
///
/// This guard reads the shipped stylesheets and their sibling markup and fails when a plain,
/// non-<c>::deep</c> scoped rule targets a class the markup only ever puts on a
/// <c>&lt;Mud...&gt;</c> element. It is what stops the pattern creeping back - it crept in during
/// #208 and #210 precisely because nothing caught it.
/// </summary>
public sealed class ScopedCssMudRootGuardTests
{
    // An opening tag with its attribute text; the attribute group tolerates quoted values so a
    // '>' inside a string does not end the match early, and it spans newlines for multi-line tags.
    private static readonly Regex OpeningTag = new(
        @"<(?<tag>[A-Za-z][A-Za-z0-9]*)(?<attrs>(?:[^>""']|""[^""]*""|'[^']*')*?)/?>",
        RegexOptions.Compiled);

    private static readonly Regex MudClassAttr = new(@"\bClass\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex HtmlClassAttr = new(@"\b[Cc]lass\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex LiteralClassToken = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly Regex CssComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CssRule = new(@"(?<sel>[^{}]+)\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex ClassInSelector = new(@"\.([A-Za-z0-9_-]+)", RegexOptions.Compiled);

    public static TheoryData<string> ScopedStylesheetsWithSibling()
    {
        var data = new TheoryData<string>();
        foreach (var css in EnumerateScopedStylesheets())
        {
            if (File.Exists(RazorFor(css)))
                data.Add(css);
        }
        return data;
    }

    [Fact]
    public void EveryScopedStylesheetHasASiblingComponent()
    {
        var orphans = EnumerateScopedStylesheets()
            .Where(css => !File.Exists(RazorFor(css)))
            .Select(Path.GetFileName)
            .ToArray();

        orphans.Should().BeEmpty(
            "the guard only inspects a stylesheet it can pair with markup; an orphan .razor.css slips through unchecked");
    }

    [Theory]
    [MemberData(nameof(ScopedStylesheetsWithSibling))]
    public void NoPlainScopedRuleTargetsAMudComponentRoot(string cssPath)
    {
        var (mudClasses, htmlClasses) = ClassesByHost(File.ReadAllText(RazorFor(cssPath)));

        var offenders = new List<string>();
        foreach (var (selector, classes) in PlainScopedRules(File.ReadAllText(cssPath)))
        {
            foreach (var cls in classes)
            {
                // Live only on a Mud element (never on a plain element that would carry the scope)
                // means the plain rule reaches nothing. If the class also lands on plain HTML the
                // rule is at least partially live, so it is not this bug.
                if (mudClasses.TryGetValue(cls, out var tags) && !htmlClasses.Contains(cls))
                    offenders.Add($"'{selector.Trim()}' -> .{cls} is only on <{string.Join("/", tags.OrderBy(t => t))}>");
            }
        }

        offenders.Should().BeEmpty(
            "a plain scoped rule on a Mud component root never matches; use ::deep (anchored on a plain wrapper) or delete it - see {0}",
            Path.GetFileName(cssPath));
    }

    // classes put on <Mud...> tags vs. on plain HTML tags. A class can appear in both maps.
    private static (Dictionary<string, HashSet<string>> Mud, HashSet<string> Html) ClassesByHost(string razor)
    {
        var mud = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var html = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match tag in OpeningTag.Matches(razor))
        {
            var name = tag.Groups["tag"].Value;
            var attrs = tag.Groups["attrs"].Value;
            var isMud = name.StartsWith("Mud", StringComparison.Ordinal);

            var values = isMud
                ? MudClassAttr.Matches(attrs).Select(m => m.Groups["v"].Value)
                : HtmlClassAttr.Matches(attrs).Select(m => m.Groups["v"].Value);

            foreach (var value in values)
            foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!LiteralClassToken.IsMatch(token))
                    continue; // skip @-interpolated fragments we cannot resolve statically

                if (isMud)
                    (mud.TryGetValue(token, out var set) ? set : mud[token] = new HashSet<string>(StringComparer.Ordinal)).Add(name);
                else
                    html.Add(token);
            }
        }

        return (mud, html);
    }

    // Each plain (non-::deep) rule paired with the classes in its key (rightmost) compound
    // selector - the element Blazor stamps the scope onto.
    private static IEnumerable<(string Selector, IReadOnlyList<string> Classes)> PlainScopedRules(string css)
    {
        css = CssComment.Replace(css, string.Empty);

        foreach (Match rule in CssRule.Matches(css))
        {
            var selectorList = rule.Groups["sel"].Value;
            foreach (var selector in selectorList.Split(','))
            {
                var part = selector.Trim();
                if (part.Length == 0 || part.StartsWith('@') || part.Contains("::deep", StringComparison.Ordinal))
                    continue;

                var rightmost = Regex.Split(part, @"[ >+~]").Last(s => s.Length > 0);
                var classes = ClassInSelector.Matches(rightmost).Select(m => m.Groups[1].Value).ToArray();
                if (classes.Length > 0)
                    yield return (part, classes);
            }
        }
    }

    private static string RazorFor(string cssPath) => cssPath[..^".css".Length];

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
