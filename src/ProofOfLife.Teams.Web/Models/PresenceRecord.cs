namespace ProofOfLife.Teams.Web.Models;

public sealed class PresenceRecord
{
    public required string Upn { get; init; }
    public required string DisplayName { get; init; }
    public required string Department { get; init; }
    public required string Source { get; init; }
}
