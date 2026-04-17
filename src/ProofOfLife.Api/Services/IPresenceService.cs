using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

public interface IPresenceService
{
    void RecordPresence(RecordPresenceRequest request);
    bool IsPresent(string userId);
    UserPresence? GetPresence(string userId);
    IReadOnlyList<UserPresence> GetAllPresent();
    IReadOnlyList<DepartmentSummary> GetDepartmentSummary();
    void Reset();
}
