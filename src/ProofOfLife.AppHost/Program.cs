var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.ProofOfLife_Api>("proof-of-life-api")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.AddProject<Projects.ProofOfLife_Web>("proof-of-life-web")
    .WithReference(api)
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.Build().Run();
