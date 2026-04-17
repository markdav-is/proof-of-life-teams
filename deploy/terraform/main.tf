locals {
  prefix = "${var.project}-${var.environment}"
  tags   = merge(var.tags, { Environment = var.environment })
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

# Key Vault for secrets (preferred over k8s secrets for production)
resource "azurerm_key_vault" "kv" {
  name                       = "kv-${local.prefix}-${random_string.suffix.result}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  tags                       = local.tags

  network_acls {
    default_action = "Allow"
    bypass         = "AzureServices"
  }
}

resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

# Store secrets in Key Vault
resource "azurerm_key_vault_secret" "webhook_client_state" {
  name         = "WebhookClientState"
  value        = var.webhook_client_state
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_key_vault_access_policy.terraform_policy]
}

resource "azurerm_key_vault_secret" "api_key" {
  name         = "ApiKey"
  value        = var.api_key
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_key_vault_access_policy.terraform_policy]
}

resource "azurerm_key_vault_secret" "api_client_secret" {
  name         = "ApiClientSecret"
  value        = var.api_client_secret
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_key_vault_access_policy.terraform_policy]
}

resource "azurerm_key_vault_secret" "web_client_secret" {
  name         = "WebClientSecret"
  value        = var.web_client_secret
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_key_vault_access_policy.terraform_policy]
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault_access_policy" "terraform_policy" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  secret_permissions = ["Get", "Set", "List", "Delete", "Purge"]
}

# Grant the AKS user-assigned identity access to Key Vault
resource "azurerm_key_vault_access_policy" "aks_policy" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = var.tenant_id
  object_id    = azurerm_user_assigned_identity.aks_workload.principal_id

  secret_permissions = ["Get", "List"]
}

# User-assigned managed identity for workload identity
resource "azurerm_user_assigned_identity" "aks_workload" {
  name                = "mi-${local.prefix}-workload"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

# Federated identity credential for workload identity
resource "azurerm_federated_identity_credential" "aks_federated" {
  name                = "federated-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.aks_workload.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.aks.oidc_issuer_url
  subject             = "system:serviceaccount:proof-of-life:proof-of-life-sa"
}

# Kubernetes namespace and secrets (applied after cluster is ready)
resource "kubernetes_namespace" "pol" {
  metadata {
    name = "proof-of-life"
    labels = {
      "app.kubernetes.io/managed-by" = "terraform"
    }
  }

  depends_on = [azurerm_kubernetes_cluster.aks]
}

# Install NGINX ingress controller via Helm
resource "helm_release" "nginx_ingress" {
  name             = "ingress-nginx"
  repository       = "https://kubernetes.github.io/ingress-nginx"
  chart            = "ingress-nginx"
  namespace        = "ingress-nginx"
  create_namespace = true
  version          = "4.10.1"

  set {
    name  = "controller.service.annotations.service\\.beta\\.kubernetes\\.io/azure-load-balancer-health-probe-request-path"
    value = "/healthz"
  }

  depends_on = [azurerm_kubernetes_cluster.aks]
}

# Install cert-manager via Helm
resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  namespace        = "cert-manager"
  create_namespace = true
  version          = "v1.15.0"

  set {
    name  = "installCRDs"
    value = "true"
  }

  depends_on = [azurerm_kubernetes_cluster.aks]
}
