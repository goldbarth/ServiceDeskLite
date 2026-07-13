using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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
