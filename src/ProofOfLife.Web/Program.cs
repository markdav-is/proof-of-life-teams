using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using ProofOfLife.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Azure AD auth with Microsoft Graph delegated permissions (for org chart)
builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi(
        builder.Configuration.GetSection("MicrosoftGraph:Scopes").Get<string[]>()!)
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddInMemoryTokenCaches();

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
builder.Services.AddRazorPages();
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = options.DefaultPolicy);

builder.Services.AddHttpContextAccessor();

// API client pointing at the backend API
builder.Services.AddHttpClient<PresenceApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("ApiBaseUrl not configured"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapControllers();
app.MapRazorPages();
app.MapRazorComponents<ProofOfLife.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
