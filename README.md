# Proof of Life — Teams Presence Dashboard

A .NET 10 Aspire application that shows who has logged into Microsoft Teams or used M365 today, broken down by department. Managers can see their full org-chart subtree. External systems can report presence via a simple API.

---

## Architecture

```
┌─────────────────────────────┐     ┌──────────────────────────────────┐
│   ProofOfLife.Web           │     │   ProofOfLife.Api                │
│   Blazor Server             │────▶│   ASP.NET Core Minimal API        │
│   Azure AD / MSAL auth      │     │   Webhooks: real-time (seconds)   │
│   Manager org-chart view    │     │   Backstop poll every 30 min      │
│   Department dashboard      │     │   In-memory presence store        │
└─────────────────────────────┘     └────────────────┬──────────────────┘
                                                     │  subscriptions +
                                                     │  change notifications
                                          ┌──────────▼──────────┐
                                          │  Microsoft Graph     │
                                          │  Presence.Read.All   │
                                          │  User.Read.All       │
                                          └─────────────────────┘
                                              │ webhooks fire on
                                              │ presence change (seconds)
                                              ▼
                                    POST /api/webhooks/graph

External systems ──▶ POST /api/presence  (X-Api-Key header)
Windows machines ──▶ LoginEventDetector.ps1 ──▶ POST /api/presence
Office launches  ──▶ OfficeActivityHook.ps1  ──▶ POST /api/presence
```

## Projects

| Project | Description |
|---------|-------------|
| `ProofOfLife.AppHost` | Aspire host — orchestrates API + Web in development |
| `ProofOfLife.ServiceDefaults` | Shared OpenTelemetry, health checks, service discovery |
| `ProofOfLife.Api` | Backend: Graph polling, in-memory store, REST API |
| `ProofOfLife.Web` | Blazor Server frontend: dashboard + manager org view |

---

## Quick Start (local dev)

### Prerequisites
- .NET 9 SDK
- .NET Aspire workload: `dotnet workload install aspire`
- An Azure AD / Entra ID tenant
- Two App Registrations (see below)

### 1. Azure AD App Registrations

**API app registration** (client credentials — for background Graph polling):
- Platform: none (service/daemon)
- API permissions (Application):
  - `Presence.Read.All`
  - `User.Read.All`
  - `OrgContact.Read.All`
- Expose an API scope: `api://<client-id>/Presence.ReadWrite`

**Web app registration** (for manager login):
- Platform: Web, redirect URI `https://localhost:7001/signin-oidc`
- API permissions (Delegated):
  - `User.Read`
  - `User.ReadBasic.All`
  - `Directory.Read.All`

### 2. User Secrets

```bash
# API project
cd src/ProofOfLife.Api
dotnet user-secrets set "AzureAd:TenantId"       "<your-tenant-id>"
dotnet user-secrets set "AzureAd:ClientId"        "<api-client-id>"
dotnet user-secrets set "AzureAd:ClientSecret"    "<api-client-secret>"
dotnet user-secrets set "Graph:TenantId"          "<your-tenant-id>"
dotnet user-secrets set "Graph:ClientId"          "<api-client-id>"
dotnet user-secrets set "Graph:ClientSecret"      "<api-client-secret>"
dotnet user-secrets set "ApiKey"                  "dev-secret-key-change-me"

# Web project
cd src/ProofOfLife.Web
dotnet user-secrets set "AzureAd:TenantId"        "<your-tenant-id>"
dotnet user-secrets set "AzureAd:ClientId"        "<web-client-id>"
dotnet user-secrets set "AzureAd:ClientSecret"    "<web-client-secret>"
dotnet user-secrets set "ApiKey"                  "dev-secret-key-change-me"
```

### 3. Run

```bash
dotnet run --project src/ProofOfLife.AppHost
```

The Aspire dashboard opens at `https://localhost:15888`. The web app is at `https://localhost:7001`.

---

## External Presence API

Any system can report that a user is present today:

```http
POST https://pol-api.yourdomain.com/api/presence
X-Api-Key: your-api-key
Content-Type: application/json

{
  "userId":      "user@yourdomain.com",
  "displayName": "Jane Smith",
  "email":       "jsmith@yourdomain.com",
  "department":  "Engineering",
  "jobTitle":    "Senior Developer",
  "managerId":   null,
  "source":      "ExternalApi"
}
```

```http
GET https://pol-api.yourdomain.com/api/departments
X-Api-Key: your-api-key
```

---

## Windows Login Detector

Reports Windows interactive logons to the API. Runs as a scheduled task under SYSTEM.

```powershell
# Install (run once, as Administrator)
.\scripts\windows-login-detector\Install-LoginDetector.ps1 `
    -ApiBaseUrl https://pol-api.yourdomain.com `
    -ApiKey your-api-key

# Or run interactively for testing
.\scripts\windows-login-detector\LoginEventDetector.ps1 `
    -ApiBaseUrl https://pol-api.yourdomain.com `
    -ApiKey your-api-key
```

## Office Activity Monitor

Reports presence when Office apps (Word, Excel, Outlook, Teams, etc.) are launched. Runs per-user at logon via Startup folder or Group Policy.

```powershell
.\scripts\office-activity-monitor\OfficeActivityHook.ps1 `
    -ApiBaseUrl https://pol-api.yourdomain.com `
    -ApiKey your-api-key
```

---

## Deployment to AKS

### Option A: Terraform (recommended)

```bash
# 1. Bootstrap remote state + init
./scripts/terraform-init.sh uksouth

# 2. Plan
terraform -chdir=deploy/terraform plan \
  -var="tenant_id=<tenant-id>" \
  -var="api_key=<api-key>" \
  -var="api_client_secret=<secret>" \
  -var="web_client_secret=<secret>"

# 3. Apply
terraform -chdir=deploy/terraform apply ...

# 4. Build & push images
./scripts/build-and-push.sh latest $(terraform -chdir=deploy/terraform output -raw acr_name)

# 5. Deploy to AKS
./scripts/deploy.sh latest
```

### Option B: Manual kubectl

```bash
# Get AKS credentials
az aks get-credentials --resource-group rg-pol-prod --name aks-pol-prod

# Apply manifests (replace placeholders first)
kubectl apply -f deploy/kubernetes/namespace.yaml
kubectl apply -f deploy/kubernetes/configmap.yaml
# Apply secrets via CI/CD or External Secrets Operator (NOT secrets.yaml directly)
kubectl apply -f deploy/kubernetes/services.yaml
kubectl apply -f deploy/kubernetes/api-deployment.yaml
kubectl apply -f deploy/kubernetes/web-deployment.yaml
kubectl apply -f deploy/kubernetes/ingress.yaml
kubectl apply -f deploy/kubernetes/hpa.yaml
```

---

## Graph Change Notifications (Real-time Presence)

`GraphSubscriptionService` registers one `communications/presences/{userId}` change notification subscription per user on startup, then renews them every 55 minutes (Graph's max is 60 min). When any user's Teams presence transitions to an active state, Graph POSTs to `/api/webhooks/graph` within seconds.

**Flow:**
1. App starts → fetch all users → create subscriptions in batches of 50
2. Graph detects presence change → POST to `/api/webhooks/graph` (seconds later)
3. Webhook handler verifies `clientState` → calls `IPresenceService.RecordPresence()`
4. Backstop poll every 30 min catches any users missed during subscription lag
5. Midnight reset clears the store; next poll/webhook repopulates it

**Security:** every notification must carry `Webhook:ClientState` — a secret known only to your app and stored in Key Vault. Notifications with wrong/missing clientState are silently dropped.

**Lifecycle events:** Graph sends `subscriptionRemoved` or `reauthorizationRequired` to the same endpoint when a subscription is forcibly removed. The handler drops the entry from the in-memory dict and the renewal loop recreates it on the next cycle.

### Local development with webhooks

Graph requires a public HTTPS endpoint to deliver notifications. In dev, use [VS Dev Tunnels](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started):

```bash
devtunnel host --port 7002 --allow-anonymous
# Copy the tunnel URL, then set:
dotnet user-secrets set "Webhook:NotificationUrl" "https://<tunnel-id>.devtunnels.ms/api/webhooks/graph" --project src/ProofOfLife.Api
dotnet user-secrets set "Webhook:ClientState" "$(uuidgen)" --project src/ProofOfLife.Api
```

Or with ngrok:
```bash
ngrok http 7002
dotnet user-secrets set "Webhook:NotificationUrl" "https://<ngrok-id>.ngrok.io/api/webhooks/graph" --project src/ProofOfLife.Api
```

If `Webhook:NotificationUrl` is not set, the service logs a warning and falls back to polling-only mode — no error thrown.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| In-memory store only | No persistence needed; `ConcurrentDictionary` is safe and fast |
| Reset at midnight UTC | `PresencePollingService` compares `DateOnly` to detect day rollover |
| Dual auth (JWT + API key) | JWT for web UI, API key for external/Windows agents |
| Presence sources | `TeamsGraph`, `ExternalApi`, `WindowsLogin`, `OfficeActivity` |
| Webhook-first, poll as backstop | Subscriptions give seconds latency; 30-min poll catches stragglers |
| Subscription lifetime 58 min | Graph max is 60 min; renew at 55 min gives 5-min safety margin |
| clientState verification | Prevents spoofed notifications from arbitrary callers |
| Singleton `GraphSubscriptionService` | Both background host and injected into webhook endpoint for user cache |
| Blazor Server sticky sessions | Required for SignalR circuit; ingress affinity cookie configured |
| AKS Workload Identity | No secret credentials on nodes; federated OIDC token exchange |

---

## Graph Permissions Required

| Permission | Type | Purpose |
|-----------|------|---------|
| `Presence.Read.All` | Application | Poll Teams presence for all users |
| `User.Read.All` | Application | Enumerate users + departments |
| `User.Read` | Delegated | Manager's own profile |
| `User.ReadBasic.All` | Delegated | Look up direct reports |
| `Directory.Read.All` | Delegated | Full org chart traversal |
