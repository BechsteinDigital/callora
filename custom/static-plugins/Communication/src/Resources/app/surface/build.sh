#!/usr/bin/env bash
# Builds the Communication plugin's surface bundle (Vue SFCs → IIFE) into
# ../../public/surface. Vue is external (resolved from the runtime's window.CalloraVue), so no
# framework is bundled and the blocks run inside the surface runtime's single Vue instance.
#
# Beside the admin build.sh rather than replacing it: a plugin ships two bundles for two
# runtimes, and the packaging step addresses each at its own path.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_ROOT="$(cd "$DIR/../../../.." && pwd)"
SURFACE_SDK="$(cd "$PLUGIN_ROOT/../../../src/Surface.Rendering/Resources/app/surface" && pwd)"

# @callora/surface is a file: dependency, which links the source directory rather than a
# published tarball — and its library output is gitignored. Without building it first the
# install succeeds and the build then fails on ERR_MODULE_NOT_FOUND for the preset.
cd "$SURFACE_SDK"
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

npx vite build --config vite.surface.config.ts
