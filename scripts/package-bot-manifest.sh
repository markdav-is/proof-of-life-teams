#!/usr/bin/env bash
# Packages the Teams app manifest into a .zip ready for upload to Teams Admin Centre.
# Substitutes __BOT_APP_ID__ from terraform output.
#
# Usage: ./scripts/package-bot-manifest.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST_DIR="$REPO_ROOT/src/ProofOfLife.Bot/teams-manifest"
OUT_ZIP="$REPO_ROOT/proof-of-life-bot.zip"

BOT_CLIENT_ID=$(terraform -chdir="$REPO_ROOT/deploy/terraform" output -raw bot_client_id)

echo "==> Bot App ID: $BOT_CLIENT_ID"

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

# Substitute placeholder in manifest
sed "s/__BOT_APP_ID__/${BOT_CLIENT_ID}/g" "$MANIFEST_DIR/manifest.json" > "$TMP_DIR/manifest.json"

# Copy icons (must exist before packaging for production)
for icon in color.png outline.png; do
  if [[ -f "$MANIFEST_DIR/$icon" ]]; then
    cp "$MANIFEST_DIR/$icon" "$TMP_DIR/$icon"
  else
    echo "WARNING: $icon not found in $MANIFEST_DIR — add icon files before uploading to Teams"
  fi
done

(cd "$TMP_DIR" && zip -j "$OUT_ZIP" .)

echo "==> Package created: $OUT_ZIP"
echo ""
echo "Next steps:"
echo "  1. Upload $OUT_ZIP to Teams Admin Centre:"
echo "     https://admin.teams.microsoft.com → Teams apps → Manage apps → Upload new app"
echo "  2. Create a setup policy to pre-install the app for all users:"
echo "     Teams apps → Setup policies → Global → Add apps → 'Proof of Life'"
