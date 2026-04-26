#!/usr/bin/env bash
set -euo pipefail

PLUGINS_ROOT="custom/plugins"
OUT_ROOT="build/plugin-assets"
OUT_PATH="build/manifests/plugin-ui-assets.manifest.json"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plugins-root)
      PLUGINS_ROOT="${2:-}"
      shift 2
      ;;
    --out-root)
      OUT_ROOT="${2:-}"
      OUT_PATH="build/manifests/plugin-ui-assets.manifest.json"
      shift 2
      ;;
    --out)
      OUT_PATH="${2:-}"
      shift 2
      ;;
    *)
      echo "Usage: $0 [--plugins-root <path>] [--out-root <path>] [--out <path>]"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [[ ! -d "$PLUGINS_ROOT" ]]; then
  echo "Plugins root not found: $PLUGINS_ROOT"
  exit 1
fi

mkdir -p "$(dirname "$OUT_PATH")"

if [[ -d "$OUT_ROOT" ]]; then
  rm -rf "$OUT_ROOT"
fi
mkdir -p "$OUT_ROOT"

NUNJUCKS_NODE_MODULES="$ROOT_DIR/apps/admin-shell/node_modules"
NUNJUCKS_AVAILABLE="false"
if [[ -d "$NUNJUCKS_NODE_MODULES/nunjucks" ]]; then
  NUNJUCKS_AVAILABLE="true"
fi

entries=()
templates=()

entry_candidates=(main.js main.mjs index.js index.mjs app.js app.mjs)

escape_json() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '%s' "$value"
}

find_plugin_entry() {
  local dir="$1"
  local candidate
  for candidate in "${entry_candidates[@]}"; do
    if [[ -f "$dir/$candidate" ]]; then
      printf '%s' "$dir/$candidate"
      return 0
    fi
  done

  return 1
}

for plugin_dir in "$PLUGINS_ROOT"/*; do
  [[ -d "$plugin_dir" ]] || continue

  plugin_name="$(basename "$plugin_dir")"
  if [[ "$plugin_name" == ".build" ]]; then
    continue
  fi

  admin_src_dir="$plugin_dir/src/Resources/app/admin/src"
  admin_public_dir="$plugin_dir/src/Resources/public/admin"
  if [[ -d "$admin_src_dir" ]]; then
    rm -rf "$admin_public_dir"
    mkdir -p "$admin_public_dir"
    cp -a "$admin_src_dir"/. "$admin_public_dir"/
  fi

  if [[ -d "$admin_public_dir" ]]; then
    admin_target="$OUT_ROOT/$plugin_name/admin"
    mkdir -p "$admin_target"
    cp -a "$admin_public_dir"/. "$admin_target"/

    if ! admin_entry="$(find_plugin_entry "$admin_target")"; then
      echo "Missing admin entry file for plugin '$plugin_name' in '$admin_public_dir'."
      exit 1
    fi

    admin_entry_name="$(basename "$admin_entry")"
    admin_entry_rel="$plugin_name/admin/$admin_entry_name"
    entries+=("$plugin_name|admin|$admin_entry_rel")
  fi

  workspace_src_dir="$plugin_dir/src/Resources/app/workspace/src"
  workspace_public_dir="$plugin_dir/src/Resources/public/workspace"
  if [[ -d "$workspace_src_dir" ]]; then
    rm -rf "$workspace_public_dir"
    mkdir -p "$workspace_public_dir"
    cp -a "$workspace_src_dir"/. "$workspace_public_dir"/
  fi

  if [[ -d "$workspace_public_dir" ]]; then
    workspace_target="$OUT_ROOT/$plugin_name/workspace"
    mkdir -p "$workspace_target"
    cp -a "$workspace_public_dir"/. "$workspace_target"/

    if ! workspace_entry="$(find_plugin_entry "$workspace_target")"; then
      echo "Missing workspace entry file for plugin '$plugin_name' in '$workspace_public_dir'."
      exit 1
    fi

    workspace_entry_name="$(basename "$workspace_entry")"
    workspace_entry_rel="$plugin_name/workspace/$workspace_entry_name"
    entries+=("$plugin_name|workspace|$workspace_entry_rel")
  fi

  template_dir="$plugin_dir/src/Resources/views/workspace"
  if [[ -d "$template_dir" ]]; then
    while IFS= read -r template_file; do
      template_relative_in_plugin="${template_file#${template_dir}/}"
      target_relative="$template_relative_in_plugin"
      extension="${template_relative_in_plugin##*.}"
      extension_lower="$(printf '%s' "$extension" | tr '[:upper:]' '[:lower:]')"

      if [[ "$extension_lower" == "njk" ]]; then
        if [[ "$NUNJUCKS_AVAILABLE" != "true" ]]; then
          echo "nunjucks not found in '$NUNJUCKS_NODE_MODULES'. Run npm install in apps/admin-shell first."
          exit 1
        fi

        target_relative="${template_relative_in_plugin%.*}.html"
        target_file="$OUT_ROOT/$plugin_name/views/workspace/$target_relative"
        NODE_PATH="$NUNJUCKS_NODE_MODULES" node "$ROOT_DIR/scripts/render-workspace-template.mjs" \
          --input "$template_file" \
          --output "$target_file" \
          --templates-root "$template_dir"
      else
        target_file="$OUT_ROOT/$plugin_name/views/workspace/$target_relative"
        mkdir -p "$(dirname "$target_file")"
        cp -f "$template_file" "$target_file"
      fi

      template_rel="$plugin_name/views/workspace/$target_relative"
      templates+=("$plugin_name|$template_rel")
    done < <(find "$template_dir" -type f | sort)
  fi
done

mapfile -t sorted_entries < <(printf '%s\n' "${entries[@]}" | sed '/^$/d' | sort)
mapfile -t sorted_templates < <(printf '%s\n' "${templates[@]}" | sed '/^$/d' | sort)

generated_at="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"

{
  echo "{"
  echo "  \"generatedAtUtc\": \"$generated_at\"," 
  echo "  \"entries\": ["

  for ((i = 0; i < ${#sorted_entries[@]}; i++)); do
    IFS='|' read -r plugin_id surface entry_path <<<"${sorted_entries[$i]}"
    comma=","
    if [[ $i -eq $((${#sorted_entries[@]} - 1)) ]]; then
      comma=""
    fi

    echo "    {\"pluginId\": \"$(escape_json "$plugin_id")\", \"surface\": \"$(escape_json "$surface")\", \"entryPath\": \"$(escape_json "$entry_path")\"}$comma"
  done

  echo "  ],"
  echo "  \"workspaceTemplates\": ["

  for ((i = 0; i < ${#sorted_templates[@]}; i++)); do
    IFS='|' read -r plugin_id template_path <<<"${sorted_templates[$i]}"
    comma=","
    if [[ $i -eq $((${#sorted_templates[@]} - 1)) ]]; then
      comma=""
    fi

    echo "    {\"pluginId\": \"$(escape_json "$plugin_id")\", \"templatePath\": \"$(escape_json "$template_path")\"}$comma"
  done

  echo "  ]"
  echo "}"
} > "$OUT_PATH"

echo "Wrote plugin UI asset manifest: $OUT_PATH"
