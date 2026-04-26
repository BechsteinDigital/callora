#!/usr/bin/env bash
set -euo pipefail

# Builds a repository map with lightweight "purpose" hints per file.
# Default output: docs/REPO_MAP.md
#
# Usage:
#   ./scripts/build-repo-map.sh
#   ./scripts/build-repo-map.sh --out docs/REPO_MAP.md
#   ./scripts/build-repo-map.sh --format tsv --out /tmp/repo-map.tsv

OUT_PATH="docs/REPO_MAP.md"
FORMAT="markdown"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out)
      OUT_PATH="${2:-}"
      shift 2
      ;;
    --format)
      FORMAT="${2:-}"
      shift 2
      ;;
    *)
      echo "Usage: $0 [--out <path>] [--format markdown|tsv]"
      exit 1
      ;;
  esac
done

if [[ "$FORMAT" != "markdown" && "$FORMAT" != "tsv" ]]; then
  echo "Unsupported format: $FORMAT (expected markdown|tsv)"
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

mkdir -p "$(dirname "$OUT_PATH")"

collect_files() {
  local roots=()
  local candidate
  for candidate in src custom tests samples perf docs scripts .github/workflows; do
    if [[ -e "$candidate" ]]; then
      roots+=("$candidate")
    fi
  done

  find "${roots[@]}" \
    -type f \
    -not -path '*/.git/*' \
    -not -path '*/bin/*' \
    -not -path '*/obj/*' \
    -not -path '_site/*' \
    -not -path 'TestResults/*' \
    | sort
}

first_nonempty_line() {
  local file="$1"
  awk 'NF { print; exit }' "$file" 2>/dev/null || true
}

md_title() {
  local file="$1"
  local title
  title="$(rg -n '^# ' "$file" -m 1 2>/dev/null | sed -E 's/^[0-9]+:# //')"
  echo "${title:-Documentation}"
}

cs_purpose() {
  local file="$1"
  local summary
  local type_decl

  summary="$(rg -n '^\s*///\s*<summary>' "$file" -m 1 2>/dev/null | sed -E 's/^[0-9]+://')"
  type_decl="$(rg -n '^\s*(public|internal)\s+(sealed\s+|static\s+|partial\s+)*(class|interface|record|enum)\s+[A-Za-z_][A-Za-z0-9_]*' "$file" -m 1 2>/dev/null | sed -E 's/^[0-9]+://')"

  if [[ -n "$summary" ]]; then
    echo "C# type with XML summary"
  elif [[ -n "$type_decl" ]]; then
    echo "C# ${type_decl}"
  else
    echo "C# source"
  fi
}

csproj_purpose() {
  local file="$1"
  local sdk
  local tfm
  sdk="$(rg -n '<Project Sdk=' "$file" -m 1 2>/dev/null | sed -E 's/^[0-9]+://')"
  tfm="$(rg -n '<TargetFramework>' "$file" -m 1 2>/dev/null | sed -E 's/^[0-9]+://; s#</?TargetFramework>##g')"
  echo "Project file (${tfm:-unknown TFM})"
}

path_scope() {
  local file="$1"
  case "$file" in
    src/Core/*) echo "Core" ;;
    src/Client/*) echo "Client" ;;
    src/Hosting/*) echo "Hosting" ;;
    src/Host/*) echo "Host" ;;
    custom/plugins/*) echo "Plugins" ;;
    src/Modules/*) echo "Modules" ;;
    src/Audio/*) echo "Audio" ;;
    src/Licensing/*) echo "Licensing" ;;
    src/Abstractions/*) echo "Abstractions" ;;
    tests/*) echo "Tests" ;;
    samples/*) echo "Samples" ;;
    perf/*) echo "Performance" ;;
    docs/*) echo "Docs" ;;
    scripts/*) echo "Scripts" ;;
    .github/workflows/*) echo "CI" ;;
    *) echo "Other" ;;
  esac
}

purpose_for_file() {
  local file="$1"
  case "$file" in
    *.md) md_title "$file" ;;
    *.cs) cs_purpose "$file" ;;
    *.csproj) csproj_purpose "$file" ;;
    *.sln) echo "Solution file" ;;
    *.yml|*.yaml)
      if [[ "$file" == .github/workflows/* ]]; then
        echo "GitHub Actions workflow"
      else
        echo "YAML configuration"
      fi
      ;;
    *.json) echo "JSON configuration/data" ;;
    *.sh) echo "Shell automation script" ;;
    *) echo "$(first_nonempty_line "$file" | cut -c1-100)" ;;
  esac
}

if [[ "$FORMAT" == "markdown" ]]; then
  {
    echo "# Repository Map"
    echo
    echo "Generated: $(date -u +'%Y-%m-%d %H:%M:%SZ')"
    echo
    echo "| Scope | File | Purpose |"
    echo "|---|---|---|"
    while IFS= read -r file; do
      scope="$(path_scope "$file")"
      purpose="$(purpose_for_file "$file" | tr '|' '/' | tr -d '\r')"
      echo "| ${scope} | \`${file}\` | ${purpose} |"
    done < <(collect_files)
  } > "$OUT_PATH"
else
  {
    echo -e "scope\tfile\tpurpose"
    while IFS= read -r file; do
      scope="$(path_scope "$file")"
      purpose="$(purpose_for_file "$file" | tr '\t' ' ' | tr -d '\r')"
      echo -e "${scope}\t${file}\t${purpose}"
    done < <(collect_files)
  } > "$OUT_PATH"
fi

echo "Wrote repo map to ${OUT_PATH} (${FORMAT})."
