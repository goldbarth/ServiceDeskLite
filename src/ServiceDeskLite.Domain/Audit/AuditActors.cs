namespace ServiceDeskLite.Domain.Audit;

/// <summary>
/// Well-known values for <see cref="AuditEvent.Actor"/>.
/// <see cref="AuditEvent.Actor"/> stays a free-form string — a real user identity will not
/// be enumerable — but the assistant is a fixed, first-class actor, and reporting has to
/// recognise it. Naming it here keeps the write side (the assistant's tools) and the read
/// side (automation-rate reporting) from drifting apart on a bare literal.
/// </summary>
public static class AuditActors
{
    public const string AiAssistant = "ai-assistant";
}
