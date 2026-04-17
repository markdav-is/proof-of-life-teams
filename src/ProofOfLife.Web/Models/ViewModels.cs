namespace ProofOfLife.Web.Models;

public record UserPresence(
    string UserId,
    string DisplayName,
    string Email,
    string Department,
    string JobTitle,
    string ManagerId,
    DateTimeOffset FirstSeenToday,
    DateTimeOffset LastSeenAt,
    string Source
);

public record DepartmentSummary(
    string Department,
    int PresentCount,
    int TotalKnownCount,
    List<UserPresence> PresentUsers
);

public record OrgNodePresence(
    string UserId,
    string DisplayName,
    string Email,
    string JobTitle,
    string Department,
    bool IsPresent,
    DateTimeOffset? LastSeenAt,
    List<OrgNodePresence> DirectReports
);
