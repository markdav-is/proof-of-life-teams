#!/usr/bin/env bash
# Build Docker images and push to ACR.
# Usage: ./scripts/build-and-push.sh [IMAGE_TAG] [ACR_NAME]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE_TAG="${1:-$(git -C "$REPO_ROOT" rev-parse --short HEAD)}"
ACR_NAME="${2:-}"

if [[ -z "$ACR_NAME" ]]; then
  # Try to read from terraform output
  ACR_NAME=$(terraform -chdir="$REPO_ROOT/deploy/terraform" output -raw acr_name 2>/dev/null || true)
fi

if [[ -z "$ACR_NAME" ]]; then
  echo "ERROR: ACR_NAME not provided and could not be read from terraform output."
  echo "Usage: $0 <image-tag> <acr-name>"
  exit 1
fi

ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"

echo "==> Logging into ACR: $ACR_LOGIN_SERVER"
az acr login --name "$ACR_NAME"

build_and_push() {
  local service="$1"
  local dockerfile="$2"
  local image="${ACR_LOGIN_SERVER}/${service}:${IMAGE_TAG}"
  local latest="${ACR_LOGIN_SERVER}/${service}:latest"

  echo ""
  echo "==> Building $service (tag: $IMAGE_TAG)"
  docker build \
    --file "$dockerfile" \
    --tag "$image" \
    --tag "$latest" \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    --cache-from "$latest" \
    "$REPO_ROOT"

  echo "==> Pushing $image"
  docker push "$image"
  docker push "$latest"
}

build_and_push "proof-of-life-api" "$REPO_ROOT/src/ProofOfLife.Api/Dockerfile"
build_and_push "proof-of-life-bot" "$REPO_ROOT/src/ProofOfLife.Bot/Dockerfile"
build_and_push "proof-of-life-web" "$REPO_ROOT/src/ProofOfLife.Web/Dockerfile"

echo ""
echo "==> Done. Images pushed with tag: $IMAGE_TAG"
echo "    API: ${ACR_LOGIN_SERVER}/proof-of-life-api:${IMAGE_TAG}"
echo "    Bot: ${ACR_LOGIN_SERVER}/proof-of-life-bot:${IMAGE_TAG}"
echo "    Web: ${ACR_LOGIN_SERVER}/proof-of-life-web:${IMAGE_TAG}"
