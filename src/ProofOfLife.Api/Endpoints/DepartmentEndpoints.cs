using ProofOfLife.Api.Services;

namespace ProofOfLife.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static IEndpointRouteBuilder MapDepartmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/departments").RequireAuthorization("ApiKeyOrJwt");

        group.MapGet("/", GetDepartmentSummary)
            .WithName("GetDepartmentSummary")
            .WithSummary("Get presence summary grouped by department");

        group.MapGet("/{department}", GetDepartmentUsers)
            .WithName("GetDepartmentUsers")
            .WithSummary("Get all present users in a specific department");

        return app;
    }

    private static IResult GetDepartmentSummary(IPresenceService presenceService) =>
        Results.Ok(presenceService.GetDepartmentSummary());

    private static IResult GetDepartmentUsers(string department, IPresenceService presenceService)
    {
        var users = presenceService.GetAllPresent()
            .Where(u => string.Equals(u.Department, department, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Results.Ok(users);
    }
}
