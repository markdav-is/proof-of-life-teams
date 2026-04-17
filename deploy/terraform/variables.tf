variable "subscription_id" {
  type        = string
  description = "Azure subscription ID"
}

variable "location" {
  type        = string
  description = "Azure region"
  default     = "eastus"
}

variable "resource_group_name" {
  type        = string
  description = "Resource group for AKS resources"
  default     = "proof-of-life-teams-rg"
}

variable "aks_name" {
  type        = string
  description = "AKS cluster name"
  default     = "proof-of-life-teams-aks"
}

variable "acr_name" {
  type        = string
  description = "ACR name (must be globally unique and alphanumeric)"
}
