namespace ProofOfLife.Bot.Services;

public class PresenceApiClient(HttpClient http, IConfiguration config)
{
    public async Task RecordPresenceAsync(string userId, string displayName, CancellationToken ct = default)
    {
        // Set API key on every call (HttpClient is shared/reused)
        http.DefaultRequestHeaders.Remove("X-Api-Key");
        http.DefaultRequestHeaders.Add("X-Api-Key", config["ApiKey"] ?? string.Empty);

        // Department, email, jobTitle are unknown here — the API's PresenceService
        // will enrich them from cached Graph data if a Graph event arrives later.
        var body = new
        {
            userId,
            displayName,
            email       = string.Empty,
            department  = string.Empty,
            jobTitle    = string.Empty,
            managerId   = (string?)null,
            source      = "TeamsBot"
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // don't let a slow API stall the bot

        var response = await http.PostAsJsonAsync("/api/presence", body, cts.Token);
        response.EnsureSuccessStatusCode();
    }
}
