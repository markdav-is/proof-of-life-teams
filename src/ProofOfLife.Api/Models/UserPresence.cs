namespace ProofOfLife.Api.Models;

public record UserPresence(
    string UserId,
    string DisplayName,
    string Email,
    string Department,
    string JobTitle,
    string ManagerId,
    DateTimeOffset FirstSeenToday,
    DateTimeOffset LastSeenAt,
    PresenceSource Source
);

public enum PresenceSource
{
    TeamsGraph,
    TeamsBot,
    ExternalApi,
    WindowsLogin,
    OfficeActivity
}
