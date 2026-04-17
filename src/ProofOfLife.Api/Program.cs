using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Graph;
using ProofOfLife.Api.Auth;
using ProofOfLife.Api.Endpoints;
using ProofOfLife.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Graph SDK — app-level client credentials for background polling + subscription management
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var credential = new ClientSecretCredential(
        config["Graph:TenantId"]!,
        config["Graph:ClientId"]!,
        config["Graph:ClientSecret"]!);
    return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
});

builder.Services.AddSingleton<IPresenceService, PresenceService>();
builder.Services.AddScoped<IGraphService, GraphService>();

// GraphSubscriptionService manages real-time webhook subscriptions.
// Registered as singleton so the webhook endpoint can inject it for user-cache lookups.
builder.Services.AddSingleton<GraphSubscriptionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GraphSubscriptionService>());

// Polling backstop — catches anything missed by webhooks, now at 30-min interval
builder.Services.AddHostedService<PresencePollingService>();

// Authentication: both API key and Azure AD JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var config = builder.Configuration;
    options.Authority = $"{config["AzureAd:Instance"]}{config["AzureAd:TenantId"]}/v2.0";
    options.Audience = config["AzureAd:Audience"];
    options.TokenValidationParameters.ValidateIssuer = true;
})
.AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.SchemeName, _ => { });

// Policy that accepts either JWT bearer OR API key
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ApiKeyOrJwt", policy =>
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthOptions.SchemeName)
              .RequireAuthenticatedUser());

builder.Services.AddOpenApi();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.WithOrigins(
        builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5001", "https://localhost:7001"])
        .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Webhook endpoint is public (no auth) — security is via clientState verification
app.MapWebhookEndpoints();

app.MapPresenceEndpoints();
app.MapDepartmentEndpoints();

app.Run();

public partial class Program { }
