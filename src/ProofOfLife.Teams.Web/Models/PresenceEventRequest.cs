namespace ProofOfLife.Teams.Web.Models;

public sealed record PresenceEventRequest
{
    public string Upn { get; init; } = string.Empty;
    public string? Department { get; init; }
    public string? Source { get; init; }
}
