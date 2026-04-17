FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY ["src/ProofOfLife.ServiceDefaults/ProofOfLife.ServiceDefaults.csproj", "ProofOfLife.ServiceDefaults/"]
COPY ["src/ProofOfLife.Web/ProofOfLife.Web.csproj", "ProofOfLife.Web/"]
RUN dotnet restore "ProofOfLife.Web/ProofOfLife.Web.csproj"
COPY src/ProofOfLife.ServiceDefaults/ ProofOfLife.ServiceDefaults/
COPY src/ProofOfLife.Web/ ProofOfLife.Web/
WORKDIR "/src/ProofOfLife.Web"
RUN dotnet publish "ProofOfLife.Web.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProofOfLife.Web.dll"]
