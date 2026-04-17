using Microsoft.AspNetCore.Mvc;
using ProofOfLife.Api.Auth;
using ProofOfLife.Api.Models;
using ProofOfLife.Api.Services;

namespace ProofOfLife.Api.Endpoints;

public static class PresenceEndpoints
{
    public static IEndpointRouteBuilder MapPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/presence").RequireAuthorization("ApiKeyOrJwt");

        group.MapGet("/", GetAllPresent)
            .WithName("GetAllPresent")
            .WithSummary("List all users present today");

        group.MapGet("/{userId}", GetUserPresence)
            .WithName("GetUserPresence")
            .WithSummary("Get presence status for a specific user");

        group.MapPost("/", RecordPresence)
            .WithName("RecordPresence")
            .WithSummary("Record a presence event for a user")
            .WithDescription("Allows external systems to report that a user is active today.");

        group.MapGet("/org/{userId}/reports", GetReportsPresence)
            .WithName("GetReportsPresence")
            .WithSummary("Get presence for a manager's direct reports");

        group.MapGet("/org/{userId}/transitive-reports", GetTransitiveReportsPresence)
            .WithName("GetTransitiveReportsPresence")
            .WithSummary("Get presence for all downstream reports (full org subtree)");

        return app;
    }

    private static IResult GetAllPresent(IPresenceService presenceService) =>
        Results.Ok(presenceService.GetAllPresent());

    private static IResult GetUserPresence(string userId, IPresenceService presenceService)
    {
        var presence = presenceService.GetPresence(userId);
        return presence is not null ? Results.Ok(presence) : Results.NotFound();
    }

    private static IResult RecordPresence(
        [FromBody] RecordPresenceRequest request,
        IPresenceService presenceService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Results.BadRequest("UserId is required");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Results.BadRequest("DisplayName is required");

        presenceService.RecordPresence(request);
        logger.LogInformation("External presence recorded for {User} ({Source})", request.DisplayName, request.Source);
        return Results.Created($"/api/presence/{request.UserId}", presenceService.GetPresence(request.UserId));
    }

    private static async Task<IResult> GetReportsPresence(
        string userId,
        IGraphService graphService,
        IPresenceService presenceService)
    {
        var reports = await graphService.GetDirectReportsAsync(userId);
        var result = reports.Select(r => BuildOrgNode(r, presenceService, [])).ToList();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTransitiveReportsPresence(
        string userId,
        IGraphService graphService,
        IPresenceService presenceService)
    {
        var manager = await graphService.GetUserAsync(userId);
        if (manager is null) return Results.NotFound();

        var node = await BuildOrgNodeWithReportsAsync(manager, graphService, presenceService);
        return Results.Ok(node);
    }

    private static OrgNodePresence BuildOrgNode(
        GraphUserInfo user,
        IPresenceService presenceService,
        IReadOnlyList<OrgNodePresence> reports)
    {
        var presence = presenceService.GetPresence(user.Id);
        return new OrgNodePresence(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.JobTitle ?? string.Empty,
            user.Department ?? string.Empty,
            presence is not null,
            presence?.LastSeenAt,
            reports);
    }

    private static async Task<OrgNodePresence> BuildOrgNodeWithReportsAsync(
        GraphUserInfo user,
        IGraphService graphService,
        IPresenceService presenceService)
    {
        var directs = await graphService.GetDirectReportsAsync(user.Id);
        var childNodes = new List<OrgNodePresence>();
        foreach (var direct in directs)
            childNodes.Add(await BuildOrgNodeWithReportsAsync(direct, graphService, presenceService));

        return BuildOrgNode(user, presenceService, childNodes);
    }
}
