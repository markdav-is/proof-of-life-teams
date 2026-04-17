var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.ProofOfLife_Api>("proof-of-life-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

// Bot posts presence events to the API using the internal API key
builder.AddProject<Projects.ProofOfLife_Bot>("proof-of-life-bot")
    .WithReference(api)
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.AddProject<Projects.ProofOfLife_Web>("proof-of-life-web")
    .WithReference(api)
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.Build().Run();
