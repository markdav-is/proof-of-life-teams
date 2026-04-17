var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject("web", "../ProofOfLife.Teams.Web/ProofOfLife.Teams.Web.csproj");

builder.Build().Run();
