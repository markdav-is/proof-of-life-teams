using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using ProofOfLife.Teams.Web.Models;

namespace ProofOfLife.Teams.Web.Services;

public sealed class OrgChartService
{
    private static readonly EmployeeProfile[] SeedEmployees =
    [
        new("amy.manager@agency.example", "Amy Manager", "Operations", null),
        new("brad.supervisor@agency.example", "Brad Supervisor", "Operations", "amy.manager@agency.example"),
        new("carol.supervisor@agency.example", "Carol Supervisor", "Finance", "amy.manager@agency.example"),
        new("dee.analyst@agency.example", "Dee Analyst", "Operations", "brad.supervisor@agency.example"),
        new("eli.specialist@agency.example", "Eli Specialist", "Finance", "carol.supervisor@agency.example"),
        new("faye.contractor@agency.example", "Faye Contractor", "IT", "carol.supervisor@agency.example")
    ];

    private sealed class ManagerCredential
    {
        public string Upn { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public string Salt { get; init; } = string.Empty;
        public int Iterations { get; init; } = 100_000;
    }

    private readonly FrozenDictionary<string, EmployeeProfile> _employeesByUpn;
    private readonly FrozenDictionary<string, string[]> _reportsByManager;
    private readonly FrozenDictionary<string, ManagerCredential> _managerCredentialsByUpn;

    public OrgChartService(IConfiguration configuration)
    {
        var employeeList = SeedEmployees;
        _employeesByUpn = employeeList.ToFrozenDictionary(x => x.Upn, StringComparer.OrdinalIgnoreCase);

        _reportsByManager = employeeList
            .Where(x => !string.IsNullOrWhiteSpace(x.ManagerUpn))
            .GroupBy(x => x.ManagerUpn!, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                x => x.Key,
                x => x.Select(y => y.Upn).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var managerCredentials = configuration.GetSection("ManagerAuth:Accounts")
            .Get<ManagerCredential[]>() ?? [];
        _managerCredentialsByUpn = managerCredentials
            .Where(x => !string.IsNullOrWhiteSpace(x.Upn))
            .ToFrozenDictionary(x => x.Upn, StringComparer.OrdinalIgnoreCase);
    }

    public bool ValidateManagerCredentials(string upn, string password)
    {
        if (!_managerCredentialsByUpn.TryGetValue(upn, out var credential))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(credential.PasswordHash) || string.IsNullOrWhiteSpace(credential.Salt))
        {
            return false;
        }

        try
        {
            var saltBytes = Convert.FromBase64String(credential.Salt);
            var expectedHash = Convert.FromBase64String(credential.PasswordHash);
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                credential.Iterations <= 0 ? 100_000 : credential.Iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public EmployeeProfile? TryGetEmployee(string upn) =>
        _employeesByUpn.GetValueOrDefault(upn);

    public IReadOnlyCollection<EmployeeProfile> GetDownstreamEmployees(string managerUpn)
    {
        var downstream = new List<EmployeeProfile>();
        var queue = new Queue<string>();
        queue.Enqueue(managerUpn);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_reportsByManager.TryGetValue(current, out var reports))
            {
                continue;
            }

            foreach (var report in reports)
            {
                if (_employeesByUpn.TryGetValue(report, out var profile))
                {
                    downstream.Add(profile);
                    queue.Enqueue(report);
                }
            }
        }

        return downstream;
    }
}
