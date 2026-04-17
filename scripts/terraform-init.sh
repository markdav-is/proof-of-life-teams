#!/usr/bin/env bash
# One-time terraform bootstrap: create the remote state storage account, then init.
# Run this ONCE before 'terraform apply'.
set -euo pipefail

LOCATION="${1:-uksouth}"
STATE_RG="rg-pol-tfstate"
STATE_SA="polterraformstate"       # must be globally unique — change if taken
STATE_CONTAINER="tfstate"

echo "==> Creating terraform state storage account"
az group create --name "$STATE_RG" --location "$LOCATION" --output none
az storage account create \
  --name "$STATE_SA" \
  --resource-group "$STATE_RG" \
  --location "$LOCATION" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --allow-blob-public-access false \
  --output none

az storage container create \
  --name "$STATE_CONTAINER" \
  --account-name "$STATE_SA" \
  --auth-mode login \
  --output none

echo "==> Running terraform init"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
terraform -chdir="$REPO_ROOT/deploy/terraform" init \
  -backend-config="resource_group_name=${STATE_RG}" \
  -backend-config="storage_account_name=${STATE_SA}" \
  -backend-config="container_name=${STATE_CONTAINER}" \
  -backend-config="key=proof-of-life.tfstate"

echo "==> Done. Now run: terraform -chdir=deploy/terraform plan"
