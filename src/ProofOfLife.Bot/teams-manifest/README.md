# Teams App Manifest

This directory contains the Teams app manifest for the Proof of Life bot.

## Before packaging

1. Replace `__BOT_APP_ID__` in `manifest.json` with the bot's Azure AD application client ID
   (output as `bot_client_id` from `terraform output`)
2. Update `developer`, `validDomains`, and URL fields with your actual domain
3. Add two icon files:
   - `color.png`  — 192 × 192 px, full colour, PNG
   - `outline.png` — 32 × 32 px, white + transparent, PNG

## Package

```bash
cd src/ProofOfLife.Bot/teams-manifest
zip -j ../proof-of-life-bot.zip manifest.json color.png outline.png
```

Or use the helper script from the repo root:

```bash
./scripts/package-bot-manifest.sh
```

## Deploy to Teams

### Option A — Admin pre-install (recommended for full coverage)

Upload to your tenant's app catalogue so admins can push it to all users:

1. Teams Admin Centre → **Teams apps** → **Manage apps** → **Upload new app**
2. Upload `proof-of-life-bot.zip`
3. **Teams apps** → **Setup policies** → your policy → **Add apps** → search "Proof of Life" → **Add**
4. Assign the policy to all users (or "All staff" group)

Users will have the bot silently pre-installed — no action needed from them.

### Option B — Sideload for testing

1. Teams → **Apps** → **Manage your apps** → **Upload an app** → **Upload a custom app**
2. Select `proof-of-life-bot.zip`
3. The bot installs in your personal scope — any Teams activity you generate will be recorded

## Coverage note

The bot receives activity events for:
- **Personal scope**: direct messages to/from the bot, and lifecycle events
- **Team scope**: messages in channels where the bot is a member, @mentions
- **Group chat**: messages in group chats where the bot is added

For maximum coverage (catch any Teams activity, not just messages in specific channels),
pre-installing in **personal scope** via admin setup policy is the recommended approach.
The bot will then receive a `conversationUpdate` for every user on installation,
and any subsequent direct interaction with them.
