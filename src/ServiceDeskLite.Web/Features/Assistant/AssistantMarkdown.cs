using System.Net;
using System.Text;

using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Features.Assistant;

/// <summary>
/// Converts assistant Markdown into HTML that is safe to inject via <c>MarkupString</c>.
/// Assistant output embeds user-controlled content (ticket titles, descriptions, comments
/// echoed by the model), so everything here treats the input as hostile.
/// </summary>
public static class AssistantMarkdown
{
    // DisableHtml() makes the parser emit raw HTML as escaped literal text, which closes
    // the stored-XSS path through echoed ticket content. The extensions are listed
    // explicitly instead of UseAdvancedExtensions(): that bundle includes
    // GenericAttributes, which would let markdown attach arbitrary HTML attributes
    // (onmouseover=...) and reopen the hole DisableHtml closes.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];

    // Private-use characters as citation-placeholder delimiters. They are not HTML-special,
    // so Markdig passes them through untouched to the output, where they are swapped for the
    // trusted badge HTML. The model's own text is scrubbed of them first, so it cannot forge a
    // placeholder and reach the swap.
    private const char SentinelOpen = '';
    private const char SentinelClose = '';

    /// <summary>
    /// Renders the answer to HTML and anchors an inline citation badge after the first mention
    /// of each citation's title (the model cites by title). The badge carries a hover/focus
    /// tooltip. Citations whose title never appears are returned in <see cref="MarkdownRender.Unanchored"/>
    /// so the caller can still list them; every citation is also numbered by its position so the
    /// inline badge and the sources list agree.
    /// </summary>
    public static MarkdownRender Render(string markdown, IReadOnlyList<AssistantCitation> citations)
    {
        if (citations.Count == 0)
            return new MarkdownRender(ToHtml(markdown), []);

        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkdownRender(string.Empty, citations);

        // Defense in depth: strip any pre-existing sentinels so the model cannot smuggle one in.
        var text = markdown.Replace(SentinelOpen, ' ').Replace(SentinelClose, ' ');

        var anchored = new bool[citations.Count];
        var inserts = new List<(int Position, int Index)>();

        for (var i = 0; i < citations.Count; i++)
        {
            var title = citations[i].Title;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var start = text.IndexOf(title, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                continue;

            var end = start + title.Length;
            if (end < text.Length && IsClosingQuote(text[end]))
                end++;

            inserts.Add((end, i));
            anchored[i] = true;
        }

        if (inserts.Count == 0)
            return new MarkdownRender(ToHtml(text), citations);

        // Insert right-to-left so earlier positions stay valid. When two citations share one
        // anchor position (e.g. two passages from the same article), insert the higher index
        // first so the lower one ends up on its left - the co-located badges read [1][2], not [2][1].
        var sb = new StringBuilder(text);
        foreach (var (position, index) in inserts.OrderByDescending(x => x.Position).ThenByDescending(x => x.Index))
            sb.Insert(position, $"{SentinelOpen}{index}{SentinelClose}");

        var html = ToHtml(sb.ToString());

        foreach (var (_, index) in inserts)
            html = html.Replace($"{SentinelOpen}{index}{SentinelClose}", BadgeHtml(index + 1, citations[index]));

        var unanchored = citations.Where((_, i) => !anchored[i]).ToList();
        return new MarkdownRender(html, unanchored);
    }

    // Trusted markup, but every citation field is user-reachable content (KB corpus text), so
    // each is HTML-encoded before it enters the string.
    private static string BadgeHtml(int number, AssistantCitation citation)
    {
        var title = WebUtility.HtmlEncode(citation.Title);
        var source = WebUtility.HtmlEncode(citation.Source);
        var heading = WebUtility.HtmlEncode(citation.Heading);
        var snippet = WebUtility.HtmlEncode(citation.Snippet);

        return
            $"<span class=\"assistant-chat__cite\" tabindex=\"0\" role=\"button\" aria-label=\"Source {number}: {title}\">" +
            $"[{number}]" +
            "<span class=\"assistant-chat__cite-tip\" role=\"tooltip\">" +
            $"<span class=\"assistant-chat__cite-tip-title\">{title}</span>" +
            $"<span class=\"assistant-chat__cite-tip-meta\">{source} › {heading}</span>" +
            $"<span class=\"assistant-chat__cite-tip-snippet\">{snippet}</span>" +
            "</span></span>";
    }

    private static bool IsClosingQuote(char c) => c is '"' or '”' or '\'' or '’' or '»';

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var document = Markdown.Parse(markdown, Pipeline);

        // The renderer HTML-escapes attribute values, but escaping does not make a
        // javascript: or data: href harmless — the scheme itself is the payload.
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!HasSafeScheme(link.Url))
                link.Url = string.Empty;
        }

        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (!HasSafeScheme(autolink.Url))
                autolink.Url = string.Empty;
        }

        var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    // Scheme allow-list rather than a block-list: anything before the first ':' must be
    // exactly http/https/mailto. A colon that appears after '/', '?' or '#' belongs to the
    // path of a relative URL, not to a scheme. Obfuscations like "java\tscript:" fail the
    // exact match, so no separate control-character stripping is needed.
    private static bool HasSafeScheme(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return true;

        var colon = url.IndexOf(':');
        if (colon < 0)
            return true;

        var pathStart = url.IndexOfAny(['/', '?', '#']);
        if (pathStart >= 0 && pathStart < colon)
            return true;

        return SafeSchemes.Contains(url[..colon], StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Result of <see cref="AssistantMarkdown.Render"/>: the answer HTML with inline citation badges
/// already injected, plus the citations whose title never appeared in the text and so could not be
/// anchored - the caller lists those under the reply.
/// </summary>
public sealed record MarkdownRender(string Html, IReadOnlyList<AssistantCitation> Unanchored);
