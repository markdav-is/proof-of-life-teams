using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using ProofOfLife.Bot;
using ProofOfLife.Bot.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddControllers();

// Bot Framework authentication reads MicrosoftAppId / MicrosoftAppPassword /
// MicrosoftAppType / MicrosoftAppTenantId from configuration.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp =>
{
    var auth   = sp.GetRequiredService<BotFrameworkAuthentication>();
    var logger = sp.GetRequiredService<ILogger<CloudAdapter>>();
    var adapter = new CloudAdapter(auth, logger);

    adapter.OnTurnError = async (turnContext, ex) =>
    {
        logger.LogError(ex, "Unhandled error in bot turn");
        // Deliberately no user-facing reply — this is a silent presence bot
        await Task.CompletedTask;
    };

    return adapter;
});

builder.Services.AddTransient<IBot, PresenceBot>();

builder.Services.AddHttpClient<PresenceApiClient>(client =>
    client.BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("ApiBaseUrl not configured")));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
