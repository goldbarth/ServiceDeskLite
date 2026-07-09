using System.Globalization;

using ServiceDeskLite.Application.Abstractions.Routing;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Routing;

/// <summary>
/// Deterministic, rule-based ticket triage: classifies category and priority by keyword
/// match over the title + description, maps the category to a roster agent, and suggests
/// the triage status move. Confidence reflects how much actually matched — a ticket that
/// matches neither a category nor an urgency cue scores low, so the use-case only
/// suggests rather than applies. Pure and side-effect free: the routing rules are tested
/// against fixtures with no database or model (issue #159, ADR-0032).
/// </summary>
public sealed class KeywordTicketRouter : ITicketRouter
{
    // Category keywords, most-specific dimensions first. The best-matching category wins
    // (ties broken by this order); no match falls back to Other.
    private static readonly (TicketCategory Category, string[] Keywords)[] CategoryRules =
    [
        (TicketCategory.Access, ["password", "login", "log in", "sign in", "sso", "mfa", "2fa", "locked out", "lockout", "permission", "access denied", "reset"]),
        (TicketCategory.Account, ["account", "onboarding", "new hire", "offboarding", "mailbox", "provision", "license", "distribution list"]),
        (TicketCategory.Network, ["vpn", "network", "wifi", "wi-fi", "dns", "internet", "tunnel", "connection", "firewall", "proxy"]),
        (TicketCategory.Hardware, ["printer", "laptop", "monitor", "keyboard", "mouse", "hardware", "docking", "dock", "screen", "device", "battery"]),
        (TicketCategory.Software, ["install", "application", "app crash", "software", "update", "excel", "outlook", "teams", "browser", "error message"]),
    ];

    // Urgency cues, checked high-to-low; no match means the default Medium.
    private static readonly (TicketPriority Priority, string[] Keywords)[] PriorityRules =
    [
        (TicketPriority.Critical, ["outage", "down", "cannot work", "can't work", "production", "breach", "security incident", "urgent", "critical", "everyone", "company-wide"]),
        (TicketPriority.High, ["asap", "blocked", "deadline", "multiple users", "several colleagues", "important", "high priority", "escalate"]),
        (TicketPriority.Low, ["how do i", "how to", "question", "request", "whenever", "no rush", "minor", "cosmetic", "nice to have"]),
    ];

    // Which roster agent owns each category (names must match the seeded AgentRoster).
    private static readonly Dictionary<TicketCategory, string> CategoryOwners = new()
    {
        [TicketCategory.Network] = "Alex Kim",
        [TicketCategory.Hardware] = "Sam Rivera",
        [TicketCategory.Software] = "Priya Nair",
        [TicketCategory.Access] = "Jordan Lee",
        [TicketCategory.Account] = "Morgan Diaz",
    };

    private const double BaseConfidence = 0.3;
    private const double CategoryWeight = 0.35;
    private const double PriorityWeight = 0.25;

    public RoutingDecision Route(string title, string description)
    {
        var text = $"{title}\n{description}".ToLower(CultureInfo.InvariantCulture);

        var (category, categoryHit) = ClassifyCategory(text);
        var (priority, priorityHit) = ClassifyPriority(text);

        var confidence = BaseConfidence
            + (categoryHit is not null ? CategoryWeight : 0)
            + (priorityHit is not null ? PriorityWeight : 0);

        CategoryOwners.TryGetValue(category, out var assignee);

        // Routing is itself the triage step: a freshly-created ticket moves New -> Triaged.
        var status = TicketStatus.Triaged;

        var rationale = BuildRationale(category, categoryHit, priority, priorityHit, assignee);

        return new RoutingDecision(category, priority, assignee, status, Math.Round(confidence, 2), rationale);
    }

    private static (TicketCategory, string?) ClassifyCategory(string text)
    {
        TicketCategory best = TicketCategory.Other;
        string? bestHit = null;
        var bestCount = 0;

        foreach (var (category, keywords) in CategoryRules)
        {
            var hits = keywords.Where(text.Contains).ToList();
            if (hits.Count > bestCount)
            {
                best = category;
                bestHit = hits[0];
                bestCount = hits.Count;
            }
        }

        return (best, bestHit);
    }

    private static (TicketPriority, string?) ClassifyPriority(string text)
    {
        foreach (var (priority, keywords) in PriorityRules)
        {
            var hit = keywords.FirstOrDefault(text.Contains);
            if (hit is not null)
                return (priority, hit);
        }

        return (TicketPriority.Medium, null);
    }

    private static string BuildRationale(
        TicketCategory category, string? categoryHit,
        TicketPriority priority, string? priorityHit,
        string? assignee)
    {
        var categoryPart = categoryHit is not null
            ? $"category={category} (matched \"{categoryHit}\")"
            : $"category={category} (no keyword matched)";
        var priorityPart = priorityHit is not null
            ? $"priority={priority} (matched \"{priorityHit}\")"
            : $"priority={priority} (default)";
        var assigneePart = assignee is not null ? $"assignee={assignee}" : "assignee=none";

        return $"{categoryPart}; {priorityPart}; {assigneePart}; status=Triaged.";
    }
}
