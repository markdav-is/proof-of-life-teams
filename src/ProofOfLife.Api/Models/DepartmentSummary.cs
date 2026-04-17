namespace ProofOfLife.Api.Models;

public record DepartmentSummary(
    string Department,
    int PresentCount,
    int TotalKnownCount,
    IReadOnlyList<UserPresence> PresentUsers
);

public record OrgNodePresence(
    string UserId,
    string DisplayName,
    string Email,
    string JobTitle,
    string Department,
    bool IsPresent,
    DateTimeOffset? LastSeenAt,
    IReadOnlyList<OrgNodePresence> DirectReports
);

public record RecordPresenceRequest(
    string UserId,
    string DisplayName,
    string Email,
    string Department,
    string JobTitle,
    string? ManagerId,
    PresenceSource Source
);
