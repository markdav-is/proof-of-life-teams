#!/usr/bin/env bash
set -euo pipefail

: "${IMAGE:?Set IMAGE to your built container image (example: myacr.azurecr.io/proof-of-life-teams-web:latest)}"
: "${PRESENCE_API_KEY:?Set PRESENCE_API_KEY before running deploy}"

kubectl apply -f namespace.yaml
kubectl -n proof-of-life-teams create secret generic proof-of-life-teams-secrets \
  --from-literal=presence-api-key="$PRESENCE_API_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -

sed "s|REPLACE_WITH_ACR/proof-of-life-teams-web:latest|$IMAGE|g" deployment.yaml | kubectl apply -f -
kubectl apply -f ingress.yaml
