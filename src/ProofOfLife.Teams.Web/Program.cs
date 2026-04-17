using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using ProofOfLife.Teams.Web.Models;
using ProofOfLife.Teams.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<OrgChartService>();
builder.Services.AddSingleton<PresenceTracker>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", [Authorize] (HttpContext context, OrgChartService orgChart, PresenceTracker tracker) =>
{
    var managerUpn = context.User.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
    if (string.IsNullOrWhiteSpace(managerUpn))
    {
        return Results.Redirect("/login");
    }

    var activeDownstream = tracker.GetActiveToday(orgChart.GetDownstreamEmployees(managerUpn));

    var grouped = activeDownstream
        .GroupBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
        .Select(x => (Department: x.Key, ActiveUsers: x.Count()))
        .ToArray();

    var html = new StringBuilder();
    html.Append("<html><head><title>Agency Login Dashboard</title><style>body{font-family:Segoe UI,Arial;margin:2rem;}table{border-collapse:collapse;width:100%;margin-bottom:1.5rem;}th,td{border:1px solid #ccc;padding:.5rem;text-align:left;}th{background:#f0f4f8;} .meta{display:flex;justify-content:space-between;align-items:center;}</style></head><body>");
    html.Append($"<div class='meta'><h1>Agency Teams Activity Dashboard</h1><form method='post' action='/logout'><button type='submit'>Logout</button></form></div><p>Manager: {WebUtility.HtmlEncode(managerUpn)}</p>");

    html.Append("<h2>Active M365 logins by department (today)</h2><table><thead><tr><th>Department</th><th>Active users</th></tr></thead><tbody>");
    foreach (var item in grouped)
    {
        html.Append($"<tr><td>{WebUtility.HtmlEncode(item.Department)}</td><td>{item.ActiveUsers}</td></tr>");
    }

    if (grouped.Length == 0)
    {
        html.Append("<tr><td colspan='2'>No downstream activity has been reported today.</td></tr>");
    }

    html.Append("</tbody></table>");

    html.Append("<h2>Active downstream employees</h2><table><thead><tr><th>Name</th><th>UPN</th><th>Department</th><th>Source</th></tr></thead><tbody>");
    foreach (var row in activeDownstream.OrderBy(x => x.Department).ThenBy(x => x.DisplayName))
    {
        html.Append($"<tr><td>{WebUtility.HtmlEncode(row.DisplayName)}</td><td>{WebUtility.HtmlEncode(row.Upn)}</td><td>{WebUtility.HtmlEncode(row.Department)}</td><td>{WebUtility.HtmlEncode(row.Source)}</td></tr>");
    }

    if (activeDownstream.Count == 0)
    {
        html.Append("<tr><td colspan='4'>No downstream activity has been reported today.</td></tr>");
    }

    html.Append("</tbody></table></body></html>");

    return Results.Content(html.ToString(), "text/html");
});

app.MapGet("/login", () => Results.Content(LoginPage(null), "text/html"));

app.MapPost("/login", async (HttpContext context, OrgChartService orgChart) =>
{
    var form = await context.Request.ReadFormAsync();
    var upn = form["upn"].ToString().Trim();
    var password = form["password"].ToString();

    if (!orgChart.ValidateManagerCredentials(upn, password))
    {
        return Results.Content(
            LoginPage("Invalid manager login."),
            "text/html",
            Encoding.UTF8,
            StatusCodes.Status401Unauthorized);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, upn),
        new(ClaimTypes.Name, upn)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.Redirect("/");
});

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapPost("/api/presence/events", (HttpContext context, PresenceEventRequest request, OrgChartService orgChart, PresenceTracker tracker, IConfiguration configuration) =>
{
    return RecordPresence(context, request, orgChart, tracker, configuration);
});

app.MapPost("/api/presence/teams", (HttpContext context, PresenceEventRequest request, OrgChartService orgChart, PresenceTracker tracker, IConfiguration configuration) =>
{
    request = request with { Source = "teams" };
    return RecordPresence(context, request, orgChart, tracker, configuration);
});

app.MapPost("/api/presence/windows-login", (HttpContext context, PresenceEventRequest request, OrgChartService orgChart, PresenceTracker tracker, IConfiguration configuration) =>
{
    request = request with { Source = "windows-login" };
    return RecordPresence(context, request, orgChart, tracker, configuration);
});

app.MapPost("/api/presence/office-activity", (HttpContext context, PresenceEventRequest request, OrgChartService orgChart, PresenceTracker tracker, IConfiguration configuration) =>
{
    request = request with { Source = "office-activity" };
    return RecordPresence(context, request, orgChart, tracker, configuration);
});

app.MapGet("/api/presence/today", [Authorize] (HttpContext context, OrgChartService orgChart, PresenceTracker tracker) =>
{
    var managerUpn = context.User.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
    if (string.IsNullOrWhiteSpace(managerUpn))
    {
        return Results.Unauthorized();
    }

    var data = tracker.GetActiveToday(orgChart.GetDownstreamEmployees(managerUpn));
    return Results.Ok(data);
});

app.MapGet("/api/presence/raw", (HttpContext context, PresenceTracker tracker, IConfiguration configuration) =>
{
    if (!IsApiKeyValid(context, configuration))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(tracker.GetActiveTodayWithoutOrgData());
});

app.Run();

static bool IsApiKeyValid(HttpContext context, IConfiguration configuration)
{
    var expected = configuration["PresenceApi:ApiKey"] ?? "local-dev-api-key";
    var provided = context.Request.Headers["X-Api-Key"].ToString();
    return !string.IsNullOrWhiteSpace(provided) && string.Equals(expected, provided, StringComparison.Ordinal);
}

static IResult RecordPresence(HttpContext context, PresenceEventRequest request, OrgChartService orgChart, PresenceTracker tracker, IConfiguration configuration)
{
    if (!IsApiKeyValid(context, configuration))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Upn))
    {
        return Results.BadRequest(new { error = "upn is required" });
    }

    var normalizedUpn = request.Upn.Trim();
    var source = string.IsNullOrWhiteSpace(request.Source) ? "external" : request.Source.Trim();
    var department = orgChart.TryGetEmployee(normalizedUpn)?.Department ?? request.Department;
    tracker.RecordPresence(normalizedUpn, source, department);

    return Results.Ok(new { status = "recorded", upn = normalizedUpn, source, department = department ?? "Unknown" });
}

static string LoginPage(string? message)
{
    var notice = string.IsNullOrWhiteSpace(message)
        ? string.Empty
        : $"<p style='color:#b00'>{WebUtility.HtmlEncode(message)}</p>";

    return $"""
    <html>
    <head><title>Manager Login</title></head>
    <body style='font-family:Segoe UI,Arial;margin:2rem;'>
      <h1>Proof of Life Teams</h1>
      <p>Login with manager credentials to see your downstream employees.</p>
      {notice}
      <form method='post' action='/login'>
        <label for='upn'>Manager UPN</label><br/>
        <input type='email' id='upn' name='upn' autocomplete='username' required/><br/><br/>
        <label for='password'>Password</label><br/>
        <input type='password' id='password' name='password' autocomplete='current-password' required/><br/><br/>
        <button type='submit'>Login</button>
      </form>
      <p>External systems can post to <code>/api/presence/events</code> with <code>X-Api-Key</code>.</p>
    </body>
    </html>
    """;
}

public partial class Program;
