using System.Net.Http.Headers;
using System.Text.Json;
using ProofOfLife.Web.Models;

namespace ProofOfLife.Web.Services;

public class PresenceApiClient(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private void AddApiKey() =>
        http.DefaultRequestHeaders.Remove("X-Api-Key");

    public async Task<List<DepartmentSummary>> GetDepartmentSummaryAsync(CancellationToken ct = default)
    {
        SetApiKeyHeader();
        var response = await http.GetAsync("/api/departments", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DepartmentSummary>>(JsonOpts, ct) ?? [];
    }

    public async Task<List<UserPresence>> GetAllPresentAsync(CancellationToken ct = default)
    {
        SetApiKeyHeader();
        var response = await http.GetAsync("/api/presence", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserPresence>>(JsonOpts, ct) ?? [];
    }

    public async Task<OrgNodePresence?> GetTransitiveReportsAsync(string userId, CancellationToken ct = default)
    {
        SetApiKeyHeader();
        var response = await http.GetAsync($"/api/presence/org/{userId}/transitive-reports", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrgNodePresence>(JsonOpts, ct);
    }

    public async Task<List<OrgNodePresence>> GetDirectReportsAsync(string userId, CancellationToken ct = default)
    {
        SetApiKeyHeader();
        var response = await http.GetAsync($"/api/presence/org/{userId}/reports", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<OrgNodePresence>>(JsonOpts, ct) ?? [];
    }

    private void SetApiKeyHeader()
    {
        var key = config["ApiKey"];
        if (!string.IsNullOrEmpty(key))
        {
            http.DefaultRequestHeaders.Remove("X-Api-Key");
            http.DefaultRequestHeaders.Add("X-Api-Key", key);
        }
    }
}
