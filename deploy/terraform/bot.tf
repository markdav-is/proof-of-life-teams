# ── Bot app registration ──────────────────────────────────────────────────────
resource "azuread_application" "bot" {
  display_name     = "Proof of Life Bot (${var.environment})"
  sign_in_audience = "AzureADMyOrg"
}

resource "azuread_service_principal" "bot" {
  client_id    = azuread_application.bot.client_id
  use_existing = false
}

resource "azuread_application_password" "bot" {
  application_id = azuread_application.bot.id
  display_name   = "terraform-managed"
  end_date       = timeadd(timestamp(), "8760h") # 1 year

  lifecycle {
    ignore_changes = [end_date]
  }
}

resource "azurerm_key_vault_secret" "bot_app_password" {
  name         = "BotAppPassword"
  value        = azuread_application_password.bot.value
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_key_vault_access_policy.terraform_policy]
}

# ── Azure Bot Service ─────────────────────────────────────────────────────────
resource "azurerm_bot_service_azure_bot" "bot" {
  name                = "bot-${local.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  # Bot Service is a global resource (location must be "global")
  location             = "global"
  microsoft_app_id     = azuread_application.bot.client_id
  microsoft_app_type   = "SingleTenant"
  microsoft_app_tenant_id = var.tenant_id
  sku                  = "S1"
  endpoint             = "https://${var.bot_hostname}/api/messages"
  tags                 = local.tags

  # Display name shown to users in Teams
  display_name = "Proof of Life"
  description  = "Silent presence tracker — records Teams activity on the agency dashboard."
}

# ── Teams channel ─────────────────────────────────────────────────────────────
resource "azurerm_bot_channel_ms_teams" "teams" {
  bot_name            = azurerm_bot_service_azure_bot.bot.name
  location            = azurerm_bot_service_azure_bot.bot.location
  resource_group_name = azurerm_resource_group.main.name
  calling_web_hook    = "https://${var.bot_hostname}/api/calls"
}
