using ProofOfLife.Api.Models;
using ProofOfLife.Api.Services;

namespace ProofOfLife.Api.Endpoints;

public static class WebhookEndpoints
{
    // Graph presence statuses that count as "active today"
    private static readonly HashSet<string> ActiveAvailabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Busy", "DoNotDisturb", "BeRightBack",
        "InACall", "InAConferenceCall", "InAMeeting", "Presenting",
        "UrgentInterruptionsOnly", "AvailableIdle", "BusyIdle"
    };

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // No auth on this route — Graph calls it from outside.
        // Security is enforced by verifying clientState on every notification.
        app.MapPost("/api/webhooks/graph", HandleGraphNotification)
            .WithName("GraphWebhook")
            .WithSummary("Receives Microsoft Graph Change Notifications for Teams presence")
            .ExcludeFromDescription(); // hide from Scalar/OpenAPI — not a public API

        return app;
    }

    private static async Task<IResult> HandleGraphNotification(
        HttpContext context,
        IPresenceService presenceService,
        GraphSubscriptionService subscriptionService,
        IConfiguration config,
        ILogger<Program> logger)
    {
        // ── 1. Validation handshake ───────────────────────────────────────────
        // Graph POSTs with ?validationToken=<token> when a subscription is first created.
        // We must echo it back as text/plain within 10 seconds.
        if (context.Request.Query.TryGetValue("validationToken", out var validationToken))
        {
            logger.LogInformation("Graph webhook validation handshake received");
            return Results.Content(validationToken.ToString(), "text/plain", statusCode: 200);
        }

        // ── 2. Change / lifecycle notifications ──────────────────────────────
        GraphNotificationPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<GraphNotificationPayload>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse Graph notification payload");
            return Results.BadRequest();
        }

        if (payload?.Value is null or { Count: 0 })
            return Results.Accepted();

        var expectedClientState = config["Webhook:ClientState"];

        foreach (var notification in payload.Value)
        {
            // Security: reject notifications whose clientState doesn't match
            if (notification.ClientState != expectedClientState)
            {
                logger.LogWarning("Rejected Graph notification with invalid clientState " +
                                  "(subscriptionId: {SubId})", notification.SubscriptionId);
                continue;
            }

            // ── 2a. Lifecycle events ─────────────────────────────────────────
            if (notification.LifecycleEvent is not null)
            {
                HandleLifecycleEvent(notification, subscriptionService, logger);
                continue;
            }

            // ── 2b. Presence change notifications ────────────────────────────
            if (notification.ChangeType != "updated") continue;

            var subId = notification.SubscriptionId;
            if (subId is null || !subscriptionService.IsKnownSubscription(subId))
            {
                logger.LogDebug("Received notification for unknown subscription {SubId} — ignoring", subId);
                continue;
            }

            var userId = notification.ResourceData?.Id
                         ?? subscriptionService.GetUserIdForSubscription(subId);

            if (userId is null) continue;

            var availability = notification.ResourceData?.Availability;

            // If the notification doesn't carry availability inline (some Graph versions
            // omit it), we only know the user has activity — treat as active anyway.
            var isActive = availability is null || ActiveAvailabilities.Contains(availability);
            if (!isActive) continue;

            var user = subscriptionService.GetUserInfo(userId);
            if (user is null)
            {
                // User not yet in cache (e.g. added after last sync) — record with minimal info
                logger.LogDebug("Presence notification for uncached user {UserId}", userId);
                presenceService.RecordPresence(new RecordPresenceRequest(
                    userId, userId, userId, "Unknown", string.Empty, null,
                    PresenceSource.TeamsGraph));
                continue;
            }

            presenceService.RecordPresence(new RecordPresenceRequest(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Department ?? "Unknown",
                user.JobTitle ?? string.Empty,
                user.ManagerId,
                PresenceSource.TeamsGraph));

            logger.LogDebug("Webhook presence recorded: {User} ({Availability})",
                user.DisplayName, availability ?? "unknown");
        }

        // Graph requires 202 Accepted — anything else triggers retries
        return Results.Accepted();
    }

    private static void HandleLifecycleEvent(
        GraphChangeNotification notification,
        GraphSubscriptionService subscriptionService,
        ILogger logger)
    {
        var subId = notification.SubscriptionId ?? "(unknown)";
        switch (notification.LifecycleEvent)
        {
            case "subscriptionRemoved":
                logger.LogWarning("Graph removed subscription {SubId} — will recreate on next sync", subId);
                if (notification.SubscriptionId is not null)
                    subscriptionService.HandleSubscriptionRemoved(notification.SubscriptionId);
                break;

            case "reauthorizationRequired":
                // Treat the same as removed; the renewal loop will recreate it
                logger.LogWarning("Graph requires reauthorisation for subscription {SubId}", subId);
                if (notification.SubscriptionId is not null)
                    subscriptionService.HandleSubscriptionRemoved(notification.SubscriptionId);
                break;

            case "missed":
                logger.LogInformation("Graph signals missed notifications for subscription {SubId} — " +
                                      "polling backstop will catch up", subId);
                break;

            default:
                logger.LogDebug("Unhandled lifecycle event '{Event}' for subscription {SubId}",
                    notification.LifecycleEvent, subId);
                break;
        }
    }
}
