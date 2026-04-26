#!/usr/bin/env bash
set -euo pipefail

MODULE_PATH="apps/admin-shell"
OUT_PATH="artifacts/admin-shell"
SKIP_INSTALL="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --module-path)
      MODULE_PATH="${2:-}"
      shift 2
      ;;
    --out)
      OUT_PATH="${2:-}"
      shift 2
      ;;
    --skip-install)
      SKIP_INSTALL="true"
      shift
      ;;
    *)
      echo "Usage: $0 [--module-path <path>] [--out <path>] [--skip-install]"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -d "$MODULE_PATH" ]]; then
  echo "Admin UI module path not found: $MODULE_PATH"
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required to build Admin UI."
  exit 1
fi

if [[ "$SKIP_INSTALL" != "true" ]]; then
  if [[ -f "$MODULE_PATH/package-lock.json" ]]; then
    npm ci --prefix "$MODULE_PATH"
  else
    npm install --prefix "$MODULE_PATH"
  fi
fi

./scripts/build-plugin-ui-assets.sh

npm run --prefix "$MODULE_PATH" build

SOURCE_PATH=""
if [[ -d "$MODULE_PATH/.output/public" ]]; then
  SOURCE_PATH="$MODULE_PATH/.output/public"
elif [[ -d "$MODULE_PATH/dist" ]]; then
  SOURCE_PATH="$MODULE_PATH/dist"
else
  echo "Nuxt output not found in '$MODULE_PATH/.output/public' or '$MODULE_PATH/dist'."
  exit 1
fi

rm -rf "$OUT_PATH"
mkdir -p "$OUT_PATH"
cp -a "$SOURCE_PATH"/. "$OUT_PATH"/

echo "Admin UI build artifacts written to: $OUT_PATH"
