using ProofOfLife.Teams.Web.Models;
using ProofOfLife.Teams.Web.Services;

namespace ProofOfLife.Teams.Web.Tests;

public class OrgChartServiceTests
{
    [Fact]
    public void SeedData_ContainsExpectedEmployee()
    {
        var service = new OrgChartService();

        var employee = service.TryGetEmployee("dee.analyst@agency.example");

        Assert.NotNull(employee);
        Assert.Equal("Operations", employee.Department);
    }

    [Fact]
    public void SeedData_ProvidesDownstreamReportsForAmy()
    {
        var service = new OrgChartService();

        var downstream = service.GetDownstreamEmployees("amy.manager@agency.example");

        Assert.True(downstream.Count >= 3);
    }

    [Fact]
    public void GetDownstreamEmployees_IncludesNestedReports()
    {
        var service = new OrgChartService();

        var downstream = service.GetDownstreamEmployees("amy.manager@agency.example");

        Assert.Contains(downstream, x => x.Upn == "brad.supervisor@agency.example");
        Assert.Contains(downstream, x => x.Upn == "dee.analyst@agency.example");
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
        var orgChart = new OrgChartService();
        tracker.RecordPresence("dee.analyst@agency.example", "teams", null);

        var active = tracker.GetActiveToday(orgChart.GetDownstreamEmployees("amy.manager@agency.example"));

        Assert.Contains(active, x => x.Upn == "dee.analyst@agency.example");
    }
}
