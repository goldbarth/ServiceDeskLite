using System.Collections.Concurrent;

using Serilog.Core;
using Serilog.Events;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

public sealed class InMemorySink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = [];
    
    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();
    
    public void Emit(LogEvent logEvent)
        => _events.Enqueue(logEvent);
}
