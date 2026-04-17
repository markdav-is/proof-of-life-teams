FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["src/ProofOfLife.ServiceDefaults/ProofOfLife.ServiceDefaults.csproj", "ProofOfLife.ServiceDefaults/"]
COPY ["src/ProofOfLife.Api/ProofOfLife.Api.csproj", "ProofOfLife.Api/"]
RUN dotnet restore "ProofOfLife.Api/ProofOfLife.Api.csproj"
COPY src/ProofOfLife.ServiceDefaults/ ProofOfLife.ServiceDefaults/
COPY src/ProofOfLife.Api/ ProofOfLife.Api/
WORKDIR "/src/ProofOfLife.Api"
RUN dotnet publish "ProofOfLife.Api.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProofOfLife.Api.dll"]
