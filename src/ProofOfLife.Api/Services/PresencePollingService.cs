using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

/// <summary>
/// Background service that polls Teams presence every 5 minutes and resets the store at midnight.
/// </summary>
public class PresencePollingService(
    IGraphService graphService,
    IPresenceService presenceService,
    ILogger<PresencePollingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private DateOnly _lastResetDate = DateOnly.FromDateTime(DateTime.UtcNow);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Presence polling service started");

        // Fetch all users once on startup, then poll presence
        await PollAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            ResetAtMidnightIfNeeded();
            await PollAsync(stoppingToken);
        }
    }

    private void ResetAtMidnightIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today > _lastResetDate)
        {
            logger.LogInformation("New day detected — resetting presence store");
            presenceService.Reset();
            _lastResetDate = today;
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            logger.LogDebug("Fetching all users from Graph...");
            var users = await graphService.GetAllUsersAsync(ct);
            if (users.Count == 0) return;

            logger.LogDebug("Fetching presence for {Count} users", users.Count);
            var userMap = users.ToDictionary(u => u.Id);
            var activePresences = await graphService.GetPresenceBatchAsync(users.Select(u => u.Id), ct);

            foreach (var presence in activePresences)
            {
                if (!userMap.TryGetValue(presence.UserId, out var user)) continue;

                presenceService.RecordPresence(new RecordPresenceRequest(
                    user.Id,
                    user.DisplayName,
                    user.Email ?? string.Empty,
                    user.Department ?? "Unknown",
                    user.JobTitle ?? string.Empty,
                    user.ManagerId,
                    PresenceSource.TeamsGraph));
            }

            logger.LogInformation("Presence poll complete: {Active}/{Total} users active",
                activePresences.Count, users.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Error during presence poll");
        }
    }
}
