# proof-of-life-teams

a system for knowing who's logged-into teams today

## What this solution includes

- **.NET Aspire AppHost** (`src/ProofOfLife.Teams.AppHost`) to orchestrate the web app.
- **Web dashboard** (`src/ProofOfLife.Teams.Web`) that shows active M365/Teams users by **department** for the logged-in manager's downstream org chart.
- **In-memory only presence store** (no persistence) that tracks whether a person has activity **today**.
- **Simple external API auth** using `X-Api-Key` for posting presence events.
- Bonus ingestion endpoints for non-Teams activity:
  - `POST /api/presence/windows-login`
  - `POST /api/presence/office-activity`
- **AKS deployment assets** in `deploy/aks`.
- **Terraform example** for provisioning AKS + ACR in `deploy/terraform`.

## Local run

```bash
dotnet run --project /home/runner/work/proof-of-life-teams/proof-of-life-teams/src/ProofOfLife.Teams.Web/ProofOfLife.Teams.Web.csproj
```

Login page defaults:

- Manager UPN: `amy.manager@agency.example`
- Password: `Passw0rd!`

## Presence ingestion API

Set `PresenceApi:ApiKey` (default: `local-dev-api-key`) and call:

```bash
curl -X POST http://localhost:5059/api/presence/teams \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-api-key" \
  -d '{"upn":"dee.analyst@agency.example"}'
```

Generic external endpoint:

```bash
curl -X POST http://localhost:5059/api/presence/events \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-api-key" \
  -d '{"upn":"eli.specialist@agency.example","source":"external-system"}'
```

## Aspire run

```bash
dotnet run --project /home/runner/work/proof-of-life-teams/proof-of-life-teams/src/ProofOfLife.Teams.AppHost/ProofOfLife.Teams.AppHost.csproj
```

## AKS deployment

1. Build and push your container image.
2. Run from `deploy/aks`:

```bash
IMAGE=<acr>.azurecr.io/proof-of-life-teams-web:latest PRESENCE_API_KEY=<secret> ./deploy.sh
```

## Terraform deployment example

From `deploy/terraform`:

```bash
terraform init
terraform apply -var "subscription_id=<subscription-id>" -var "acr_name=<globally-unique-acr-name>"
```
