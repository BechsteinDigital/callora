#!/usr/bin/env bash
# Builds the Communication plugin's admin UI bundle (Vue SFCs → IIFE) into
# ../../public/admin. Vue is external (mapped to the host's window.CalloraAdmin.vue),
# so no framework is bundled. Invoked by the distribution's packaging step
# (Callora-Production scripts/bundle-plugins.sh) before the plugin is published and
# signed, and runnable standalone for local iteration.
#
# The script stays at this path although the build configuration moved to the plugin
# root: the packaging step addresses it here, and skips the admin build silently when it
# is absent — a moved script would ship a plugin without its UI and say nothing.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_ROOT="$(cd "$DIR/../../../.." && pwd)"
ADMIN_SDK="$(cd "$PLUGIN_ROOT/../../../src/Administration/Resources/app/administration" && pwd)"

# @callora/admin is a file: dependency, which links the source directory rather than a
# published tarball — and its library output is gitignored. Without building it first the
# install succeeds and the build then fails on ERR_MODULE_NOT_FOUND for the preset.
cd "$ADMIN_SDK"
if [ -f package-lock.json ]; then
  npm ci --no-audit --no-fund
else
  npm install --no-audit --no-fund
fi
npm run build:lib

cd "$PLUGIN_ROOT"
if [ -f package-lock.json ]; then
  npm ci --no-audit --no-fund
else
  npm install --no-audit --no-fund
fi

npx vite build
