#!/usr/bin/env bash
set -euo pipefail

# Local quality gate helper.
# Runs:
# 1) engineering rules check
# 2) build
# 3) focused backend tests (if present)
#
# Usage:
#   ./scripts/dev-check.sh
#   ./scripts/dev-check.sh --all-files
#   ./scripts/dev-check.sh --skip-tests

RULE_MODE="--changed-only"
SKIP_TESTS="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --all-files)
      RULE_MODE="--all"
      shift
      ;;
    --skip-tests)
      SKIP_TESTS="true"
      shift
      ;;
    *)
      echo "Usage: $0 [--all-files] [--skip-tests]"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

./scripts/check-engineering-rules.sh "$RULE_MODE"
./scripts/dev-build.sh --configuration Debug

if [[ "$SKIP_TESTS" != "true" ]]; then
  if [[ -f "tests/Callora.Host.Backend.Tests/Callora.Host.Backend.Tests.csproj" ]]; then
    ./scripts/dev-test.sh --project tests/Callora.Host.Backend.Tests/Callora.Host.Backend.Tests.csproj
  fi
fi

echo "dev-check passed."
