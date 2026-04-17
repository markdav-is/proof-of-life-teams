output "resource_group_name" {
  description = "Name of the main resource group"
  value       = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  description = "AKS cluster name"
  value       = azurerm_kubernetes_cluster.aks.name
}

output "acr_login_server" {
  description = "ACR login server URL"
  value       = azurerm_container_registry.acr.login_server
}

output "acr_name" {
  description = "ACR resource name"
  value       = azurerm_container_registry.acr.name
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.kv.name
}

output "workload_identity_client_id" {
  description = "Client ID for the AKS workload managed identity"
  value       = azurerm_user_assigned_identity.aks_workload.client_id
}

output "tenant_id" {
  description = "Azure AD tenant ID"
  value       = var.tenant_id
  sensitive   = true
}

output "aks_oidc_issuer_url" {
  description = "AKS OIDC issuer URL (for workload identity)"
  value       = azurerm_kubernetes_cluster.aks.oidc_issuer_url
}
