using Microsoft.Extensions.Configuration;
using ProofOfLife.Teams.Web.Models;
using ProofOfLife.Teams.Web.Services;

namespace ProofOfLife.Teams.Web.Tests;

public class OrgChartServiceTests
{
    private static OrgChartService CreateService() =>
        new(new ConfigurationBuilder().AddInMemoryCollection().Build());

    [Fact]
    public void SeedData_ContainsExpectedEmployee()
    {
        var service = CreateService();

        var employee = service.TryGetEmployee("dee.analyst@agency.example");

        Assert.NotNull(employee);
        Assert.Equal("Operations", employee.Department);
    }

    [Fact]
    public void SeedData_ProvidesDownstreamReportsForAmy()
    {
        var service = CreateService();

        var downstream = service.GetDownstreamEmployees("amy.manager@agency.example");

        Assert.True(downstream.Count >= 3);
    }

    [Fact]
    public void GetDownstreamEmployees_IncludesNestedReports()
    {
        var service = CreateService();

        var downstream = service.GetDownstreamEmployees("amy.manager@agency.example");

        Assert.Contains(downstream, x => x.Upn == "brad.supervisor@agency.example");
        Assert.Contains(downstream, x => x.Upn == "dee.analyst@agency.example");
    }

    [Fact]
    public void ValidateManagerCredentials_UsesConfiguredHash()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ManagerAuth:Accounts:0:Upn"] = "manager@agency.example",
            ["ManagerAuth:Accounts:0:PasswordHash"] = "XEYb7TKSiN4y+/TPyIF1NThWJLBbTFe3n1od27fJDus=",
            ["ManagerAuth:Accounts:0:Salt"] = "B+aAG0PI0uTOG5cqq0uJZg==",
            ["ManagerAuth:Accounts:0:Iterations"] = "100000"
        };

        var service = new OrgChartService(new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build());

        Assert.True(service.ValidateManagerCredentials("manager@agency.example", "Passw0rd!"));
        Assert.False(service.ValidateManagerCredentials("manager@agency.example", "wrong-password"));
    }
}

public class PresenceTrackerTests
{
    [Fact]
    public void GetActiveToday_ReturnsOnlyRecordedUsers()
    {
        var tracker = new PresenceTracker();
        var employees = new[]
        {
            new EmployeeProfile("active@agency.example", "Active User", "Ops", null),
            new EmployeeProfile("idle@agency.example", "Idle User", "Finance", null)
        };

        tracker.RecordPresence("active@agency.example", "teams", null);

        var active = tracker.GetActiveToday(employees);

        Assert.Single(active);
        Assert.Equal("active@agency.example", active.Single().Upn);
        Assert.Equal("teams", active.Single().Source);
    }

    [Fact]
    public void GetActiveToday_WithSeedOrgChartIncludesDownstreamPresence()
    {
        var tracker = new PresenceTracker();
        var orgChart = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new OrgChartService(orgChart);
        tracker.RecordPresence("dee.analyst@agency.example", "teams", null);

        var active = tracker.GetActiveToday(service.GetDownstreamEmployees("amy.manager@agency.example"));

        Assert.Contains(active, x => x.Upn == "dee.analyst@agency.example");
    }
}
