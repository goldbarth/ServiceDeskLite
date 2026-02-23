namespace ServiceDeskLite.Web.Features.Tickets;

internal static class StatusTransitionHelper
{
    private static readonly Dictionary<string, string[]> _allowed = new()
    {
        ["New"]        = ["Triaged"],
        ["Triaged"]    = ["InProgress", "Waiting", "Resolved"],
        ["InProgress"] = ["Waiting", "Resolved"],
        ["Waiting"]    = ["InProgress", "Resolved"],
        ["Resolved"]   = ["Closed", "InProgress"],
        ["Closed"]     = [],
    };

    public static IReadOnlyList<string> GetAllowedTransitions(string currentStatus)
        => _allowed.TryGetValue(currentStatus, out var targets) ? targets : [];
}
