using System.Collections.Frozen;
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

    private static readonly FrozenDictionary<string, string> SeedManagerPasswords =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["amy.manager@agency.example"] = "Passw0rd!",
            ["brad.supervisor@agency.example"] = "Passw0rd!",
            ["carol.supervisor@agency.example"] = "Passw0rd!"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly FrozenDictionary<string, EmployeeProfile> _employeesByUpn;
    private readonly FrozenDictionary<string, string[]> _reportsByManager;

    public OrgChartService()
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
    }

    public bool ValidateManagerCredentials(string upn, string password) =>
        SeedManagerPasswords.TryGetValue(upn, out var stored) &&
        string.Equals(stored, password, StringComparison.Ordinal);

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
