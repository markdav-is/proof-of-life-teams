resource "azurerm_kubernetes_cluster" "aks" {
  name                = "aks-${local.prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "${local.prefix}-aks"
  kubernetes_version  = var.aks_kubernetes_version
  tags                = local.tags

  # Enable OIDC issuer for Workload Identity
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  default_node_pool {
    name                = "system"
    node_count          = var.aks_node_count
    vm_size             = var.aks_node_vm_size
    os_disk_size_gb     = 50
    vnet_subnet_id      = azurerm_subnet.aks.id
    pod_subnet_id       = azurerm_subnet.pods.id
    type                = "VirtualMachineScaleSets"
    enable_auto_scaling = true
    min_count           = 2
    max_count           = 10

    upgrade_settings {
      max_surge = "10%"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin    = "azure"
    network_policy    = "azure"
    load_balancer_sku = "standard"
    outbound_type     = "loadBalancer"
  }

  azure_active_directory_role_based_access_control {
    managed                = true
    azure_rbac_enabled     = true
    tenant_id              = var.tenant_id
  }

  monitor_metrics {}

  auto_scaler_profile {
    balance_similar_node_groups = true
    skip_nodes_with_system_pods = true
  }

  maintenance_window_auto_upgrade {
    frequency   = "Weekly"
    interval    = 1
    duration    = 4
    day_of_week = "Sunday"
    start_time  = "02:00"
    utc_offset  = "+00:00"
  }

  lifecycle {
    ignore_changes = [
      default_node_pool[0].node_count,
    ]
  }
}

# User node pool for application workloads (separate from system pool)
resource "azurerm_kubernetes_cluster_node_pool" "app" {
  name                  = "app"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.aks.id
  vm_size               = var.aks_node_vm_size
  vnet_subnet_id        = azurerm_subnet.aks.id
  pod_subnet_id         = azurerm_subnet.pods.id
  enable_auto_scaling   = true
  min_count             = 2
  max_count             = 8
  node_labels = {
    "workload" = "application"
  }
  tags = local.tags
}
