FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["src/ProofOfLife.ServiceDefaults/ProofOfLife.ServiceDefaults.csproj", "ProofOfLife.ServiceDefaults/"]
COPY ["src/ProofOfLife.Bot/ProofOfLife.Bot.csproj", "ProofOfLife.Bot/"]
RUN dotnet restore "ProofOfLife.Bot/ProofOfLife.Bot.csproj"
COPY src/ProofOfLife.ServiceDefaults/ ProofOfLife.ServiceDefaults/
COPY src/ProofOfLife.Bot/ ProofOfLife.Bot/
WORKDIR "/src/ProofOfLife.Bot"
RUN dotnet publish "ProofOfLife.Bot.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProofOfLife.Bot.dll"]
