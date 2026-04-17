namespace ProofOfLife.Teams.Web.Models;

public sealed record EmployeeProfile(
    string Upn,
    string DisplayName,
    string Department,
    string? ManagerUpn);
