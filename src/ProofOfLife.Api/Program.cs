using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Graph;
using ProofOfLife.Api.Auth;
using ProofOfLife.Api.Endpoints;
using ProofOfLife.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Graph SDK using app-level client credentials (for background polling)
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var tenantId = config["Graph:TenantId"]!;
    var clientId = config["Graph:ClientId"]!;
    var clientSecret = config["Graph:ClientSecret"]!;
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
});

builder.Services.AddSingleton<IPresenceService, PresenceService>();
builder.Services.AddScoped<IGraphService, GraphService>();
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
        builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5001", "https://localhost:7001"])
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

app.MapPresenceEndpoints();
app.MapDepartmentEndpoints();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
