using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using ProofOfLife.Bot.Services;

namespace ProofOfLife.Bot;

/// <summary>
/// Silent presence bot — records any Teams activity as proof of life.
/// Never responds to users; purely observational.
/// </summary>
public class PresenceBot(PresenceApiClient apiClient, ILogger<PresenceBot> logger) : ActivityHandler
{
    public override Task OnActivityAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var activity = turnContext.Activity;
        var from = activity.From;

        // Skip: no sender, or sender is a bot/system
        if (string.IsNullOrEmpty(from?.AadObjectId) || IsBot(activity))
            return Task.CompletedTask;

        var userId = from.AadObjectId;
        var displayName = from.Name ?? string.Empty;

        logger.LogDebug("Activity from {User} ({Type}) — recording presence", displayName, activity.Type);

        // Fire-and-forget: don't hold up the Bot Framework ack waiting for the API call
        _ = RecordAndSwallowAsync(userId, displayName, activity.Type, ct);

        return Task.CompletedTask;
    }

    private async Task RecordAndSwallowAsync(
        string userId, string displayName, string activityType, CancellationToken ct)
    {
        try
        {
            await apiClient.RecordPresenceAsync(userId, displayName, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record presence for {User} ({Type})", displayName, activityType);
        }
    }

    private static bool IsBot(Activity activity) =>
        string.Equals(activity.From?.Role, "bot", StringComparison.OrdinalIgnoreCase)
        || activity.From?.Id?.StartsWith("28:") == true; // Teams bot ID prefix
}
