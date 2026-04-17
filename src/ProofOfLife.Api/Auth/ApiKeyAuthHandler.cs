using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProofOfLife.Api.Auth;

public class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public class ApiKeyAuthHandler(
    IOptionsMonitor<ApiKeyAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<ApiKeyAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthOptions.HeaderName, out var apiKeyHeader))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Api-Key header"));

        var providedKey = apiKeyHeader.ToString();
        var validKey = configuration["ApiKey"];

        if (string.IsNullOrEmpty(validKey) || providedKey != validKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var claims = new[] { new Claim(ClaimTypes.Name, "ExternalSystem"), new Claim("scope", "presence.write") };
        var identity = new ClaimsIdentity(claims, ApiKeyAuthOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
