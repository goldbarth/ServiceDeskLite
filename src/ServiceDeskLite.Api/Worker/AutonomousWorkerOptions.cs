using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Worker;

/// <summary>
/// The policy the autonomous worker runs under (ADR-0037). Everything it may do on its own, and
/// everything it must ask about first, is decided here rather than in a prompt — a prompt is a
/// request, and configuration is a rule.
/// </summary>
/// <remarks>
/// The three collections start <em>empty</em>, and their defaults are applied after binding (see
/// <c>AssistantComposition.AddAutonomousWorker</c>). This is not a style choice. The configuration
/// binder appends to a collection that already has items rather than replacing it, so a default of
/// <c>[New, Triaged, InProgress]</c> plus a configured <c>[Resolved]</c> yields all four — an
/// operator narrowing the worker's reach would silently widen it, and one clearing
/// <see cref="AutonomousWrites"/> to lock the worker down would still be granting <c>add_comment</c>.
/// Binding into an empty collection makes configuration mean what it says.
/// </remarks>
public sealed class AutonomousWorkerOptions
{
    public const string SectionName = "AutonomousWorker";

    /// <summary>
    /// Statuses scanned when none are configured. <see cref="TicketStatus.Waiting"/> is deliberately
    /// absent: the worker moves a ticket there when it needs a human, and scanning it again would
    /// mean asking the same question twice.
    /// </summary>
    public static readonly IReadOnlyList<TicketStatus> DefaultScanStatuses =
        [TicketStatus.New, TicketStatus.Triaged, TicketStatus.InProgress];

    /// <summary>
    /// State-changing tools the worker may run unattended when none are configured.
    /// Comments alone: a comment reaches a person and changes nothing, which is exactly the
    /// authority an unsupervised process should have.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultAutonomousWrites = [AddCommentTool.Name];

    /// <summary>
    /// Status transitions the worker may apply unattended when none are configured. Both sort a
    /// ticket; neither finishes it.
    /// </summary>
    /// <remarks>
    /// <see cref="TicketStatus.Waiting"/> is how the worker says "I asked, and I am waiting for an
    /// answer" — it parks a ticket rather than resolving anything.
    /// <see cref="TicketStatus.Triaged"/> is here because the workflow demands it: the only
    /// transition out of <see cref="TicketStatus.New"/> is to Triaged, so without it the worker
    /// could ask a question on a new ticket but never park it, and the ticket would sit in the queue
    /// looking actionable while it waited for a reply. Triaging classifies, consistent with routing
    /// being applied autonomously under ADR-0032.
    /// <para>
    /// <see cref="TicketStatus.Resolved"/> and <see cref="TicketStatus.Closed"/> are deliberately
    /// absent: finishing someone's ticket is a decision a person makes.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<TicketStatus> DefaultAutonomousStatusTransitions =
        [TicketStatus.Triaged, TicketStatus.Waiting];

    /// <summary>
    /// Off unless switched on. A service that starts writing to tickets the moment it boots is not
    /// something anybody should get by default, least of all in a demo someone cloned.
    /// </summary>
    public bool Enabled { get; set; }

    public int ScanIntervalSeconds { get; set; } = 300;

    /// <summary>Tickets examined per scan. Bounds the cost of one pass, not the worker's lifetime.</summary>
    public int MaxTicketsPerRun { get; set; } = 5;

    /// <summary>
    /// How settled a ticket must be before the worker touches it. A ticket created seconds ago may
    /// still be getting its details typed in, and a follow-up question about missing information is
    /// noise if the reporter was about to supply it.
    /// </summary>
    public int MinTicketAgeMinutes { get; set; } = 15;

    /// <summary>Statuses the worker scans. Empty means <see cref="DefaultScanStatuses"/>.</summary>
    public IReadOnlyList<TicketStatus> ScanStatuses { get; set; } = [];

    /// <summary>
    /// State-changing tools the worker may run unattended; everything else it can only propose.
    /// Empty means <see cref="DefaultAutonomousWrites"/>. Reads are never listed here — they are
    /// always allowed, because reading a ticket harms nobody.
    /// </summary>
    public IReadOnlyList<string> AutonomousWrites { get; set; } = [];

    /// <summary>
    /// Status transitions the worker may apply unattended.
    /// Empty means <see cref="DefaultAutonomousStatusTransitions"/>.
    /// </summary>
    public IReadOnlyList<TicketStatus> AutonomousStatusTransitions { get; set; } = [];

    /// <summary>Fills any collection the configuration left empty with its documented default.</summary>
    /// <remarks>
    /// Called once after binding. To grant the worker nothing, switch it off with
    /// <see cref="Enabled"/> — an empty list means "unset", not "forbid everything", because the
    /// binder cannot tell the two apart.
    /// </remarks>
    public void ApplyDefaults()
    {
        if (ScanStatuses.Count == 0)
            ScanStatuses = DefaultScanStatuses;

        if (AutonomousWrites.Count == 0)
            AutonomousWrites = DefaultAutonomousWrites;

        if (AutonomousStatusTransitions.Count == 0)
            AutonomousStatusTransitions = DefaultAutonomousStatusTransitions;
    }
}
