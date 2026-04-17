#!/usr/bin/env bash
# Deploy proof-of-life to AKS using kubectl manifests.
# Assumes images have already been built and pushed (run build-and-push.sh first).
#
# Usage: ./scripts/deploy.sh [IMAGE_TAG] [ENVIRONMENT]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE_TAG="${1:-$(git -C "$REPO_ROOT" rev-parse --short HEAD)}"
ENVIRONMENT="${2:-prod}"
K8S_DIR="$REPO_ROOT/deploy/kubernetes"

echo "==> Deploying proof-of-life (tag: $IMAGE_TAG, env: $ENVIRONMENT)"

# Read infra values from terraform output
TF_DIR="$REPO_ROOT/deploy/terraform"
ACR_NAME=$(terraform -chdir="$TF_DIR" output -raw acr_name)
AKS_CLUSTER=$(terraform -chdir="$TF_DIR" output -raw aks_cluster_name)
RESOURCE_GROUP=$(terraform -chdir="$TF_DIR" output -raw resource_group_name)
MANAGED_IDENTITY_CLIENT_ID=$(terraform -chdir="$TF_DIR" output -raw workload_identity_client_id)
TENANT_ID=$(terraform -chdir="$TF_DIR" output -raw tenant_id)
API_CLIENT_ID=$(terraform -chdir="$TF_DIR" output -raw api_client_id)
WEB_CLIENT_ID=$(terraform -chdir="$TF_DIR" output -raw web_client_id)

echo "==> Fetching AKS credentials for $AKS_CLUSTER"
az aks get-credentials --resource-group "$RESOURCE_GROUP" --name "$AKS_CLUSTER" --overwrite-existing

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"

# Helper: substitute placeholders and apply a manifest
apply_manifest() {
  local file="$1"
  sed \
    -e "s|__ACR_NAME__|${ACR_NAME}|g" \
    -e "s|__IMAGE_TAG__|${IMAGE_TAG}|g" \
    -e "s|__TENANT_ID__|${TENANT_ID}|g" \
    -e "s|__API_CLIENT_ID__|${API_CLIENT_ID}|g" \
    -e "s|__WEB_CLIENT_ID__|${WEB_CLIENT_ID}|g" \
    -e "s|__MANAGED_IDENTITY_CLIENT_ID__|${MANAGED_IDENTITY_CLIENT_ID}|g" \
    "$file" | kubectl apply -f -
}

echo "==> Applying namespace"
kubectl apply -f "$K8S_DIR/namespace.yaml"

echo "==> Applying configmaps"
apply_manifest "$K8S_DIR/configmap.yaml"

echo "==> Applying services and service account"
apply_manifest "$K8S_DIR/services.yaml"

# NOTE: secrets.yaml is a template — real secrets must be applied separately
# (from CI/CD with values from Azure Key Vault, or via External Secrets Operator)
if [[ "${SKIP_SECRETS:-false}" != "true" ]]; then
  echo ""
  echo "WARNING: secrets.yaml contains placeholder values."
  echo "         Apply real secrets via your CI/CD pipeline or External Secrets Operator."
  echo "         Set SKIP_SECRETS=true to suppress this message and skip applying secrets.yaml"
  echo ""
fi

echo "==> Applying deployments"
apply_manifest "$K8S_DIR/api-deployment.yaml"
apply_manifest "$K8S_DIR/web-deployment.yaml"

echo "==> Applying HPA"
kubectl apply -f "$K8S_DIR/hpa.yaml"

echo "==> Applying ingress"
kubectl apply -f "$K8S_DIR/ingress.yaml"

echo "==> Waiting for rollouts"
kubectl rollout status deployment/proof-of-life-api -n proof-of-life --timeout=120s
kubectl rollout status deployment/proof-of-life-web -n proof-of-life --timeout=120s

echo ""
echo "==> Deploy complete!"
kubectl get pods -n proof-of-life
