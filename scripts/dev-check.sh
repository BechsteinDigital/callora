#!/usr/bin/env bash
set -euo pipefail

# Local quality gate.
#
# Runs:
#   1) build   — this is where the engineering rules are enforced
#   2) tests   — including the architecture rules (unless --skip-tests)
#
# There used to be a call to scripts/check-engineering-rules.sh here. That script has
# never existed in this repository, so dev-check exited on its first line and every
# local run of the gate was a no-op. The rules it was meant to police are enforced,
# just not by a shell script:
#
#   - Formatting, braces and file-scoped namespaces:
#       EnforceCodeStyleInBuild + TreatWarningsAsErrors (Directory.Build.props)
#   - Contract and visibility rules (CAL0001-CAL0003): Callora.Analyzers
#   - Public API surface: the PublicAPI analyzers + PublicAPI.Unshipped.txt
#   - One type per file, no nested types, no partial types, line caps, layering:
#       tests/Callora.Core.Tests/Architecture/ArchitectureRulesTests.cs
#
# A build that succeeds has already passed the first three. That is why the fix here
# is to delete the dead call rather than to reimplement weaker copies of those checks
# in bash, where they would drift from the ones that actually block a merge.
#
# Usage:
#   ./scripts/dev-check.sh
#   ./scripts/dev-check.sh --all-files
#   ./scripts/dev-check.sh --skip-tests

SKIP_TESTS="false"
ALL_FILES="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --all-files)
      # Kept because it appears in existing docs and habits. It selected the scope of
      # the removed script; the compiler has no narrower mode to select.
      ALL_FILES="true"
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

if [[ "$ALL_FILES" == "true" ]]; then
  echo "note: --all-files has no effect; the build checks every file it compiles."
fi

./scripts/dev-build.sh --configuration Debug

if [[ "$SKIP_TESTS" != "true" ]]; then
  if [[ -f "tests/Callora.Core.Tests/Callora.Core.Tests.csproj" ]]; then
    ./scripts/dev-test.sh --project tests/Callora.Core.Tests/Callora.Core.Tests.csproj
  fi
else
  echo "note: --skip-tests also skips the architecture rules, which are xunit tests."
fi

echo "dev-check passed."
