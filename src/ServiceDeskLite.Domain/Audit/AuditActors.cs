namespace ServiceDeskLite.Domain.Audit;

/// <summary>
/// Well-known values for <see cref="AuditEvent.Actor"/>.
/// <see cref="AuditEvent.Actor"/> stays a free-form string — a real user identity will not
/// be enumerable — but the AI actors are fixed, first-class actors, and reporting has to
/// recognise them. Naming them here keeps the write side (the agent's tools) and the read
/// side (automation-rate reporting) from drifting apart on a bare literal.
/// </summary>
public static class AuditActors
{
    /// <summary>The assistant acting inside a conversation, with a human in the loop.</summary>
    public const string AiAssistant = "ai-assistant";

    /// <summary>
    /// The autonomous worker acting on its own schedule, with nobody watching.
    /// Deliberately distinct from <see cref="AiAssistant"/>: reading the audit trail, "the
    /// assistant did this while I was talking to it" and "a background process decided this
    /// without me" are not the same event, and only the second one needs explaining to whoever
    /// owns the ticket.
    /// </summary>
    public const string AiWorker = "ai-worker";

    /// <summary>
    /// Every actor that is a model rather than a person. The automation rate counts these.
    /// </summary>
    /// <remarks>
    /// A collection rather than a predicate because the EF provider has to translate the test into
    /// SQL, and it can turn a <c>Contains</c> over this into an <c>IN</c> clause but not a method
    /// call. Both providers read this one list instead of each comparing against literals of its
    /// own, which is how the two would come to disagree about what "automated" means.
    /// </remarks>
    public static readonly string[] Automated = [AiAssistant, AiWorker];

    /// <summary>Whether an action was taken by a model rather than a person.</summary>
    public static bool IsAutomated(string? actor) => actor is AiAssistant or AiWorker;
}
