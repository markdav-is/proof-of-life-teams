using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

public record GraphUserInfo(
    string Id,
    string DisplayName,
    string? Email,
    string? Department,
    string? JobTitle,
    string? ManagerId
);

public record GraphPresenceInfo(
    string UserId,
    string Availability,
    string Activity
);

public interface IGraphService
{
    Task<IReadOnlyList<GraphUserInfo>> GetAllUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GraphPresenceInfo>> GetPresenceBatchAsync(IEnumerable<string> userIds, CancellationToken ct = default);
    Task<IReadOnlyList<GraphUserInfo>> GetDirectReportsAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<GraphUserInfo>> GetTransitiveReportsAsync(string userId, CancellationToken ct = default);
    Task<GraphUserInfo?> GetUserAsync(string userId, CancellationToken ct = default);
    Task<GraphUserInfo?> GetManagerAsync(string userId, CancellationToken ct = default);
}
