variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "uksouth"
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)"
  type        = string
  default     = "prod"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod"
  }
}

variable "project" {
  description = "Project short name, used in resource naming"
  type        = string
  default     = "pol"
}

variable "tenant_id" {
  description = "Azure AD / Entra ID tenant ID"
  type        = string
  sensitive   = true
}

variable "aks_node_count" {
  description = "Number of AKS system nodes"
  type        = number
  default     = 3
}

variable "aks_node_vm_size" {
  description = "VM size for AKS nodes"
  type        = string
  default     = "Standard_D2s_v3"
}

variable "aks_kubernetes_version" {
  description = "Kubernetes version for AKS"
  type        = string
  default     = "1.30"
}

variable "acr_sku" {
  description = "ACR SKU (Basic, Standard, Premium)"
  type        = string
  default     = "Standard"
}

variable "web_hostname" {
  description = "Public hostname for the web frontend"
  type        = string
  default     = "pol.yourdomain.com"
}

variable "api_hostname" {
  description = "Public hostname for the API"
  type        = string
  default     = "pol-api.yourdomain.com"
}

variable "api_key" {
  description = "API key for external systems to call the presence API"
  type        = string
  sensitive   = true
}

variable "webhook_client_state" {
  description = "Secret token embedded in every Graph change notification subscription; verified on receipt"
  type        = string
  sensitive   = true
}

variable "api_client_secret" {
  description = "Azure AD client secret for the API app registration"
  type        = string
  sensitive   = true
}

variable "web_client_secret" {
  description = "Azure AD client secret for the Web app registration"
  type        = string
  sensitive   = true
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default = {
    Project     = "proof-of-life"
    ManagedBy   = "terraform"
  }
}
