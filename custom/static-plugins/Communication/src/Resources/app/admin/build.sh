#!/usr/bin/env bash
# Builds the Communication plugin's admin UI bundle (Vue SFCs → IIFE) into
# ../public/admin/main.js. Vue is external (mapped to the host's window.CalloraAdmin.vue),
# so no framework is bundled. Invoked by the distribution's packaging step
# (Callora-Production scripts/bundle-plugins.sh) before the plugin is published and
# signed, and runnable standalone for local iteration.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$DIR"

if [ -f package-lock.json ]; then
  npm ci --no-audit --no-fund
else
  npm install --no-audit --no-fund
fi

npx vite build
