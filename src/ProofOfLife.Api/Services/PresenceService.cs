using System.Collections.Concurrent;
using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

public class PresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<string, UserPresence> _present = new();

    public void RecordPresence(RecordPresenceRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        _present.AddOrUpdate(
            request.UserId,
            _ => new UserPresence(
                request.UserId,
                request.DisplayName,
                request.Email,
                request.Department,
                request.JobTitle,
                request.ManagerId ?? string.Empty,
                now,
                now,
                request.Source),
            (_, existing) => existing with { LastSeenAt = now, Source = request.Source });
    }

    public bool IsPresent(string userId) => _present.ContainsKey(userId);

    public UserPresence? GetPresence(string userId) =>
        _present.TryGetValue(userId, out var p) ? p : null;

    public IReadOnlyList<UserPresence> GetAllPresent() =>
        _present.Values.OrderBy(p => p.Department).ThenBy(p => p.DisplayName).ToList();

    public IReadOnlyList<DepartmentSummary> GetDepartmentSummary()
    {
        return _present.Values
            .GroupBy(p => p.Department)
            .Select(g => new DepartmentSummary(
                g.Key,
                g.Count(),
                g.Count(),
                g.OrderBy(p => p.DisplayName).ToList()))
            .OrderBy(d => d.Department)
            .ToList();
    }

    public void Reset() => _present.Clear();
}
