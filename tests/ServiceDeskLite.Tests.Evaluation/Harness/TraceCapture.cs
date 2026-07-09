using System.Collections.Concurrent;
using System.Diagnostics;

using ServiceDeskLite.Api.Observability;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>
/// Collects the assistant's trace spans for one conversation.
/// </summary>
/// <remarks>
/// An <see cref="ActivityListener"/> is process-wide, and xUnit runs test classes in parallel, so
/// a listener sees every other test's spans too. Filtering by the conversation's trace root is
/// what makes these assertions deterministic; disabling parallelism would only hide the coupling.
/// </remarks>
public sealed class TraceCapture : IDisposable
{
    private readonly ConcurrentBag<Activity> _spans = [];
    private readonly ActivityListener _listener;

    public TraceCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AssistantInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _spans.Add,
        };

        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Spans belonging to the trace that carried <paramref name="conversationId"/>, in start order.</summary>
    public IReadOnlyList<Activity> SpansFor(Guid conversationId)
    {
        var snapshot = _spans.ToArray();

        var chat = snapshot.SingleOrDefault(s =>
            s.OperationName == "assistant.chat"
            && Equals(s.GetTagItem("conversation.id"), conversationId));

        if (chat is null)
            return [];

        return snapshot
            .Where(s => s.RootId == chat.RootId)
            .OrderBy(s => s.StartTimeUtc)
            .ToList();
    }

    public void Dispose() => _listener.Dispose();
}
