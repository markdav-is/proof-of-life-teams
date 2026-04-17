using Microsoft.Graph;
using Microsoft.Graph.Models;
using ProofOfLife.Api.Models;

namespace ProofOfLife.Api.Services;

public class GraphService(GraphServiceClient graphClient, ILogger<GraphService> logger) : IGraphService
{
    // Statuses that indicate the user has Teams open/active today
    private static readonly HashSet<string> ActiveAvailabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Busy", "DoNotDisturb", "BeRightBack",
        "InACall", "InAConferenceCall", "InAMeeting", "Presenting",
        "UrgentInterruptionsOnly", "AvailableIdle", "BusyIdle"
    };

    public async Task<IReadOnlyList<GraphUserInfo>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = new List<GraphUserInfo>();
        try
        {
            var page = await graphClient.Users.GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "department", "jobTitle"];
                cfg.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                cfg.QueryParameters.Top = 999;
            }, ct);

            while (page is not null)
            {
                if (page.Value is not null)
                    users.AddRange(page.Value.Select(ToGraphUserInfo));

                if (page.OdataNextLink is null) break;
                page = await graphClient.Users.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch users from Graph");
        }
        return users;
    }

    public async Task<IReadOnlyList<GraphPresenceInfo>> GetPresenceBatchAsync(
        IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var results = new List<GraphPresenceInfo>();
        var idList = userIds.ToList();

        // Graph presence batch endpoint supports up to 650 IDs
        foreach (var chunk in idList.Chunk(650))
        {
            try
            {
                var response = await graphClient.Communications.GetPresencesByUserId.PostAsync(
                    new Microsoft.Graph.Communications.GetPresencesByUserId.GetPresencesByUserIdPostRequestBody
                    {
                        Ids = [.. chunk]
                    }, cancellationToken: ct);

                if (response?.Value is not null)
                {
                    results.AddRange(response.Value
                        .Where(p => p.Id is not null && ActiveAvailabilities.Contains(p.Availability ?? ""))
                        .Select(p => new GraphPresenceInfo(p.Id!, p.Availability ?? "Unknown", p.Activity ?? "Unknown")));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch presence batch from Graph");
            }
        }
        return results;
    }

    public async Task<IReadOnlyList<GraphUserInfo>> GetDirectReportsAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var reports = await graphClient.Users[userId].DirectReports.GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "department", "jobTitle"];
            }, ct);

            return reports?.Value?
                .OfType<User>()
                .Select(ToGraphUserInfo)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch direct reports for {UserId}", userId);
            return [];
        }
    }

    public async Task<IReadOnlyList<GraphUserInfo>> GetTransitiveReportsAsync(string userId, CancellationToken ct = default)
    {
        var all = new List<GraphUserInfo>();
        await CollectReportsRecursiveAsync(userId, all, ct);
        return all;
    }

    private async Task CollectReportsRecursiveAsync(
        string managerId, List<GraphUserInfo> accumulator, CancellationToken ct)
    {
        var directs = await GetDirectReportsAsync(managerId, ct);
        foreach (var report in directs)
        {
            accumulator.Add(report);
            await CollectReportsRecursiveAsync(report.Id, accumulator, ct);
        }
    }

    public async Task<GraphUserInfo?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var user = await graphClient.Users[userId].GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "department", "jobTitle"];
            }, ct);
            return user is null ? null : ToGraphUserInfo(user);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch user {UserId}", userId);
            return null;
        }
    }

    public async Task<GraphUserInfo?> GetManagerAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var manager = await graphClient.Users[userId].Manager.GetAsync(ct);
            return manager is User user ? ToGraphUserInfo(user) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch manager for {UserId}", userId);
            return null;
        }
    }

    private static GraphUserInfo ToGraphUserInfo(User user) => new(
        user.Id ?? string.Empty,
        user.DisplayName ?? string.Empty,
        user.Mail ?? user.UserPrincipalName,
        user.Department,
        user.JobTitle,
        null);
}
