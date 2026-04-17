using System.Collections.Concurrent;
using ProofOfLife.Teams.Web.Models;

namespace ProofOfLife.Teams.Web.Services;

public sealed class PresenceTracker
{
    private sealed class PresenceState
    {
        public required DateOnly LastSeenDate { get; init; }
        public required string LatestSource { get; init; }
        public string? LastReportedDepartment { get; init; }
    }

    private readonly ConcurrentDictionary<string, PresenceState> _presence =
        new(StringComparer.OrdinalIgnoreCase);

    public void RecordPresence(string upn, string source, string? department)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _presence.AddOrUpdate(
            upn,
            _ => new PresenceState
            {
                LastSeenDate = today,
                LatestSource = source,
                LastReportedDepartment = department
            },
            (_, _) => new PresenceState
            {
                LastSeenDate = today,
                LatestSource = source,
                LastReportedDepartment = department
            });
    }

    public IReadOnlyCollection<PresenceRecord> GetActiveToday(IEnumerable<EmployeeProfile> employees)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<PresenceRecord>();

        foreach (var employee in employees)
        {
            if (!_presence.TryGetValue(employee.Upn, out var state) || state.LastSeenDate != today)
            {
                continue;
            }

            results.Add(new PresenceRecord
            {
                Upn = employee.Upn,
                DisplayName = employee.DisplayName,
                Department = employee.Department,
                Source = state.LatestSource
            });
        }

        return results;
    }

    public IReadOnlyCollection<PresenceRecord> GetActiveTodayWithoutOrgData()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return _presence
            .Where(x => x.Value.LastSeenDate == today)
            .Select(x => new PresenceRecord
            {
                Upn = x.Key,
                DisplayName = x.Key,
                Department = x.Value.LastReportedDepartment ?? "Unknown",
                Source = x.Value.LatestSource
            })
            .OrderBy(x => x.Department)
            .ThenBy(x => x.Upn, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
