using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>One tagged piece of summary text, ready to be sent as an SSE delta.</summary>
public readonly record struct SummaryDelta(TicketSummarySection Section, string Text);

/// <summary>
/// Splits the model's marker-delimited output into per-section text deltas.
///
/// Markers never arrive whole: a token boundary can fall anywhere, so
/// "&lt;&lt;NEXT_STEPS&gt;&gt;" may reach us as "&lt;&lt;NEXT_" then "STEPS&gt;&gt;". Text that could still
/// grow into a marker is therefore held back rather than forwarded, and released
/// as literal text only once it can no longer become one. Call <see cref="Flush"/>
/// at end of stream to release whatever is still held.
/// </summary>
public sealed class SummarySectionParser
{
    private static readonly (string Marker, TicketSummarySection Section)[] Markers =
    [
        (TicketSummaryService.SummaryMarker, TicketSummarySection.Summary),
        (TicketSummaryService.NextStepsMarker, TicketSummarySection.NextSteps),
        (TicketSummaryService.RisksMarker, TicketSummarySection.Risks),
        (TicketSummaryService.MissingInfoMarker, TicketSummarySection.MissingInfo),
    ];

    private string _buffer = string.Empty;
    private TicketSummarySection? _section;
    private bool _skipLeadingWhitespace;

    public IReadOnlyList<SummaryDelta> Feed(string chunk)
    {
        _buffer += chunk;

        List<SummaryDelta> deltas = [];

        while (_buffer.Length > 0)
        {
            var open = _buffer.IndexOf('<');

            if (open < 0)
            {
                Emit(deltas, _buffer);
                _buffer = string.Empty;
                break;
            }

            if (open > 0)
            {
                Emit(deltas, _buffer[..open]);
                _buffer = _buffer[open..];
            }

            if (TryTakeMarker())
                continue;

            // Not a marker yet. If the buffer is still a viable marker prefix, wait for
            // more input; otherwise the '<' was ordinary text and must not block the stream.
            if (IsMarkerPrefix(_buffer))
                break;

            Emit(deltas, "<");
            _buffer = _buffer[1..];
        }

        return deltas;
    }

    /// <summary>Releases the held-back tail. A dangling marker prefix is emitted as literal text.</summary>
    public IReadOnlyList<SummaryDelta> Flush()
    {
        List<SummaryDelta> deltas = [];

        if (_buffer.Length > 0)
        {
            Emit(deltas, _buffer);
            _buffer = string.Empty;
        }

        return deltas;
    }

    private bool TryTakeMarker()
    {
        foreach (var (marker, section) in Markers)
        {
            if (!_buffer.StartsWith(marker, StringComparison.Ordinal))
                continue;

            _section = section;
            _buffer = _buffer[marker.Length..];

            // The marker sits on its own line; drop the newline that follows it so the
            // section does not open with a blank line.
            _skipLeadingWhitespace = true;
            return true;
        }

        return false;
    }

    private static bool IsMarkerPrefix(string candidate) =>
        Markers.Any(m => m.Marker.StartsWith(candidate, StringComparison.Ordinal));

    /// <summary>
    /// Text before the first marker is preamble the model was told not to produce;
    /// it belongs to no section and is dropped rather than shown in an arbitrary panel.
    /// </summary>
    private void Emit(List<SummaryDelta> deltas, string text)
    {
        if (_section is not { } section)
            return;

        if (_skipLeadingWhitespace)
        {
            text = text.TrimStart();
            if (text.Length == 0)
                return;

            _skipLeadingWhitespace = false;
        }

        deltas.Add(new SummaryDelta(section, text));
    }
}
