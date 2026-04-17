using System.Collections.Concurrent;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

/// <summary>
/// Manages Graph Change Notification subscriptions for Teams presence.
///
/// Lifecycle:
///   1. On startup — fetch all users, create one subscription per user in batches.
///   2. Renewal loop (every 5 min) — renew any subscription expiring within 10 min.
///   3. Full sync (every hour) — re-fetch user list, create subscriptions for new users,
///      and clean up stale entries.
///   4. Lifecycle events — handles subscriptionRemoved / reauthorizationRequired sent
///      by Graph to the same webhook endpoint; removes and recreates affected subscriptions.
///
/// The webhook endpoint injects this service to:
///   - Verify a subscription ID is known (security)
///   - Enrich notifications with cached display name / department
/// </summary>
public class GraphSubscriptionService : BackgroundService
{
    // subscriptionId → (userId, expiresAt)
    private readonly ConcurrentDictionary<string, SubscriptionEntry> _subscriptions = new();

    // userId → GraphUserInfo (populated during sync, used to enrich webhook notifications)
    private readonly ConcurrentDictionary<string, GraphUserInfo> _userCache = new();

    private readonly GraphServiceClient _graph;
    private readonly IConfiguration _config;
    private readonly ILogger<GraphSubscriptionService> _logger;

    private static readonly TimeSpan RenewalCheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SubscriptionLifetime = TimeSpan.FromMinutes(58); // Graph max = 60 min
    private static readonly TimeSpan FullSyncInterval = TimeSpan.FromHours(1);
    private static readonly int BatchSize = 50; // subscriptions to create per burst

    private record SubscriptionEntry(string UserId, DateTimeOffset ExpiresAt);

    public GraphSubscriptionService(
        GraphServiceClient graph,
        IConfiguration config,
        ILogger<GraphSubscriptionService> logger)
    {
        _graph = graph;
        _config = config;
        _logger = logger;
    }

    // ── Public API (called by webhook endpoint) ──────────────────────────────

    public GraphUserInfo? GetUserInfo(string userId) =>
        _userCache.TryGetValue(userId, out var info) ? info : null;

    public bool IsKnownSubscription(string subscriptionId) =>
        _subscriptions.ContainsKey(subscriptionId);

    public string? GetUserIdForSubscription(string subscriptionId) =>
        _subscriptions.TryGetValue(subscriptionId, out var entry) ? entry.UserId : null;

    /// <summary>
    /// Called when Graph sends a lifecycle event (subscriptionRemoved / reauthorizationRequired).
    /// Removes the entry so the renewal loop recreates it.
    /// </summary>
    public void HandleSubscriptionRemoved(string subscriptionId)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var entry))
            _logger.LogWarning("Subscription {SubId} for user {UserId} removed by Graph — will recreate",
                subscriptionId, entry.UserId);
    }

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GraphSubscriptionService starting");

        // Initial full sync (best-effort — if webhook URL isn't reachable yet, this is a no-op)
        await FullSyncAsync(stoppingToken);

        var renewalTimer = new PeriodicTimer(RenewalCheckInterval);
        var syncTimer = new PeriodicTimer(FullSyncInterval);
        var lastSync = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await renewalTimer.WaitForNextTickAsync(stoppingToken);

            await RenewExpiringSoonAsync(stoppingToken);

            if (DateTimeOffset.UtcNow - lastSync >= FullSyncInterval)
            {
                await FullSyncAsync(stoppingToken);
                lastSync = DateTimeOffset.UtcNow;
            }
        }
    }

    // ── Sync & subscription management ───────────────────────────────────────

    private async Task FullSyncAsync(CancellationToken ct)
    {
        var notificationUrl = _config["Webhook:NotificationUrl"];
        if (string.IsNullOrWhiteSpace(notificationUrl))
        {
            _logger.LogWarning("Webhook:NotificationUrl not configured — skipping subscription sync. " +
                               "Set this to your public API URL (e.g. https://pol-api.yourdomain.com/api/webhooks/graph)");
            return;
        }

        _logger.LogInformation("Starting Graph subscription full sync");

        // Refresh user cache
        var users = await FetchAllUsersAsync(ct);
        foreach (var user in users)
            _userCache[user.Id] = user;

        // Find users who don't have an active subscription
        var subscribedUserIds = _subscriptions.Values.Select(e => e.UserId).ToHashSet();
        var missing = users.Where(u => !subscribedUserIds.Contains(u.Id)).ToList();

        _logger.LogInformation("Subscription sync: {Active} active, {Missing} to create, {Total} total users",
            subscribedUserIds.Count, missing.Count, users.Count);

        // Create subscriptions in batches to avoid Graph throttling
        foreach (var batch in missing.Chunk(BatchSize))
        {
            foreach (var user in batch)
                await TryCreateSubscriptionAsync(user, notificationUrl, ct);

            if (!ct.IsCancellationRequested)
                await Task.Delay(500, ct); // brief pause between batches
        }
    }

    private async Task RenewExpiringSoonAsync(CancellationToken ct)
    {
        var threshold = DateTimeOffset.UtcNow.Add(RenewBeforeExpiry);
        var expiring = _subscriptions
            .Where(kv => kv.Value.ExpiresAt <= threshold)
            .ToList();

        if (expiring.Count == 0) return;

        _logger.LogInformation("Renewing {Count} expiring subscriptions", expiring.Count);

        foreach (var (subId, entry) in expiring)
        {
            if (ct.IsCancellationRequested) break;
            await TryRenewSubscriptionAsync(subId, entry, ct);
        }
    }

    private async Task TryCreateSubscriptionAsync(GraphUserInfo user, string notificationUrl, CancellationToken ct)
    {
        try
        {
            var expiry = DateTimeOffset.UtcNow.Add(SubscriptionLifetime);
            var sub = await _graph.Subscriptions.PostAsync(new Subscription
            {
                ChangeType = "updated",
                NotificationUrl = notificationUrl,
                LifecycleNotificationUrl = notificationUrl,
                Resource = $"communications/presences/{user.Id}",
                ExpirationDateTime = expiry,
                ClientState = ClientState
            }, cancellationToken: ct);

            if (sub?.Id is not null)
            {
                _subscriptions[sub.Id] = new SubscriptionEntry(user.Id, expiry);
                _logger.LogDebug("Created subscription {SubId} for {User}", sub.Id, user.DisplayName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not create subscription for {User} — skipping", user.DisplayName);
        }
    }

    private async Task TryRenewSubscriptionAsync(string subId, SubscriptionEntry entry, CancellationToken ct)
    {
        try
        {
            var expiry = DateTimeOffset.UtcNow.Add(SubscriptionLifetime);
            await _graph.Subscriptions[subId].PatchAsync(
                new Subscription { ExpirationDateTime = expiry },
                cancellationToken: ct);

            _subscriptions[subId] = entry with { ExpiresAt = expiry };
            _logger.LogDebug("Renewed subscription {SubId}", subId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to renew subscription {SubId} — removing; will recreate on next sync", subId);
            _subscriptions.TryRemove(subId, out _);
        }
    }

    private async Task<List<GraphUserInfo>> FetchAllUsersAsync(CancellationToken ct)
    {
        var users = new List<GraphUserInfo>();
        try
        {
            var page = await _graph.Users.GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "department", "jobTitle"];
                cfg.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                cfg.QueryParameters.Top = 999;
            }, ct);

            while (page is not null)
            {
                if (page.Value is not null)
                    users.AddRange(page.Value.Select(u => new GraphUserInfo(
                        u.Id ?? string.Empty,
                        u.DisplayName ?? string.Empty,
                        u.Mail ?? u.UserPrincipalName,
                        u.Department,
                        u.JobTitle,
                        null)));

                if (page.OdataNextLink is null) break;
                page = await _graph.Users.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch users during subscription sync");
        }
        return users;
    }

    private string ClientState =>
        _config["Webhook:ClientState"] ?? throw new InvalidOperationException("Webhook:ClientState not configured");
}
