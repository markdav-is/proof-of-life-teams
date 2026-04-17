data "azuread_client_config" "current" {}

# ── API App Registration ────────────────────────────────────────────────────
resource "azuread_application" "api" {
  display_name     = "Proof of Life API (${var.environment})"
  sign_in_audience = "AzureADMyOrg"

  api {
    mapped_claims_enabled          = true
    requested_access_token_version = 2

    oauth2_permission_scope {
      admin_consent_description  = "Read and write Teams presence data"
      admin_consent_display_name = "Presence.ReadWrite"
      enabled                    = true
      id                         = "00000000-0000-0000-0000-000000000001"
      type                       = "Admin"
      value                      = "Presence.ReadWrite"
    }
  }

  required_resource_access {
    # Microsoft Graph
    resource_app_id = "00000003-0000-0000-c000-000000000000"

    # Application permissions (no user sign-in required for background polling)
    resource_access {
      id   = "9c3af74c-fd0f-4db4-b17a-71939e2a9d77" # Presence.Read.All
      type = "Role"
    }
    resource_access {
      id   = "df021288-bdef-4463-88db-98f22de89214" # User.Read.All
      type = "Role"
    }
    resource_access {
      id   = "498476ce-e0fe-48b0-b801-37ba7e2685c6" # OrgContact.Read.All
      type = "Role"
    }
  }
}

resource "azuread_service_principal" "api" {
  client_id = azuread_application.api.client_id
  use_existing = false
}

resource "azuread_application_password" "api" {
  application_id = azuread_application.api.id
  display_name   = "terraform-managed"
  end_date       = timeadd(timestamp(), "8760h") # 1 year

  lifecycle {
    ignore_changes = [end_date]
  }
}

# ── Web App Registration ────────────────────────────────────────────────────
resource "azuread_application" "web" {
  display_name     = "Proof of Life Web (${var.environment})"
  sign_in_audience = "AzureADMyOrg"

  web {
    redirect_uris = [
      "https://${var.web_hostname}/signin-oidc",
      "https://localhost:7001/signin-oidc",
    ]
    logout_url = "https://${var.web_hostname}/signout-oidc"

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000"

    # Delegated permissions (used on behalf of the signed-in manager)
    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
    resource_access {
      id   = "b340eb25-3456-403f-be2f-af7a0d370277" # User.ReadBasic.All
      type = "Scope"
    }
    resource_access {
      id   = "06da0dbc-49e2-44d2-8312-e9d56496d205" # Directory.Read.All (for org chart)
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "web" {
  client_id    = azuread_application.web.client_id
  use_existing = false
}

resource "azuread_application_password" "web" {
  application_id = azuread_application.web.id
  display_name   = "terraform-managed"
  end_date       = timeadd(timestamp(), "8760h")

  lifecycle {
    ignore_changes = [end_date]
  }
}

# Output app IDs for configmap substitution
output "api_client_id" {
  value = azuread_application.api.client_id
}

output "web_client_id" {
  value = azuread_application.web.client_id
}
