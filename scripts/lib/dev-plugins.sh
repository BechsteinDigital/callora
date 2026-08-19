#!/usr/bin/env bash
# Baut die Plugins, die in den Discovery-Ordnern liegen. Wird von
# scripts/dev-build.sh gesourct — kein eigenständiger Einstiegspunkt.
#
# Es gibt hier KEINE Plugin-Liste. Gefunden wird, was der Host findet: jede
# registry.json unter den beiden Scan-Roots (LocalPluginDiscoveryService).
# Damit ist ein neu dazugeklontes Plugin automatisch dabei, und es kann nicht
# passieren, dass dieses Skript ein Plugin kennt, das der Host nicht lädt — oder
# umgekehrt. Wer hier eine Liste einführt, hat die zweite Wahrheit zurückgeholt.
#
# Pro Plugin in dieser Reihenfolge:
#   1. Plattform-Pakete   — das repo-eigene scripts/pack-callora.sh in dessen local-feed
#   2. Frontend-Bundles   — npm run build, wo eine package.json ein build-Skript deklariert
#   3. Assembly           — dotnet build des csproj neben der registry.json
#   4. Nachweis           — die assemblyFileName aus der registry.json muss unter bin/ liegen
#
# Erwartete Aufrufvariablen (von dev-build.sh gesetzt):
#   DEV_PLUGINS_ROOT_DIR    Wurzel des callora-Checkouts
#   DEV_PLUGINS_CONFIG      Debug | Release
#   DEV_PLUGINS_REPACK      1 = Plattform-Pakete neu packen, auch wenn local-feed existiert
#   DEV_PLUGINS_NO_FRONTEND 1 = npm-Schritte überspringen
#   DEV_PLUGINS_SELECTED     Array von pluginIds; leer = alle

# Ein gemeinsamer Feed für Pakete, die ein Plugin für ein anderes produziert.
# Der Fall existiert wirklich: VideoConference baut gegen
# Callora.Plugin.Communication.Abstractions, das im Communication-Repo liegt und
# NICHT in Callora.Host.sln steht — die pack-callora.sh der Plugins packen aber
# nur die Solution. Statt diese Kante als Namenspaar zu verdrahten, packt jedes
# Plugin sein *.Abstractions-Projekt hierher, und alle danach gebauten Plugins
# bekommen den Feed als zusätzliche Restore-Quelle. Foundation-Tier liefert
# Verträge an Application-Tier — die Reihenfolge stimmt, weil static-plugins
# zuerst gescannt wird, genau wie beim Host.
DEV_PLUGINS_SHARED_FEED=""

dev_plugins_log() { printf '\n\033[1m══ %s\033[0m\n' "$*"; }
dev_plugins_step() { printf '   → %s\n' "$*"; }
dev_plugins_warn() { printf '   \033[33m! %s\033[0m\n' "$*" >&2; }
dev_plugins_fail() { printf '\n\033[31mFEHLER: %s\033[0m\n' "$*" >&2; return 1; }

# Liest einen String-Wert aus einer flachen JSON-Ebene. Reicht für registry.json;
# eine echte JSON-Abhängigkeit (jq) wäre eine Voraussetzung mehr für einen Build,
# der sonst nur dotnet und npm braucht.
#
# \K statt (?<=...): ein PCRE-Lookbehind muss feste Länge haben, und \s* hat sie
# nicht — mit Lookbehind liefert grep hier stillschweigend nichts zurück.
dev_plugins_json_value() {
    [ -f "$1" ] || return 0
    grep -oP "\"$2\"\s*:\s*\"\K[^\"]*" "$1" | head -1
}

# Alle registry.json unter einem Scan-Root. Ausgeschlossen sind Verzeichnisse,
# in denen eine Kopie liegen kann (bin/obj) oder ein fremdes Paket eine eigene
# registry.json mitbringt (node_modules) — sonst würde dasselbe Plugin zweimal
# gebaut oder ein npm-Paket als Plugin gelesen.
dev_plugins_find_registries() {
    local root="$1"
    [ -d "$root" ] || return 0
    find "$root" \
        \( -name node_modules -o -name bin -o -name obj -o -name local-feed \
           -o -name artifacts -o -name .git \) -prune \
        -o -name registry.json -print 2>/dev/null | sort
}

dev_plugins_is_selected() {
    [ ${#DEV_PLUGINS_SELECTED[@]} -eq 0 ] && return 0
    local want
    for want in "${DEV_PLUGINS_SELECTED[@]}"; do
        [ "$want" = "$1" ] && return 0
    done
    return 1
}

# Das Plugin-Repo ist das erste Verzeichnis unterhalb des Scan-Roots. Die
# registry.json kann tiefer liegen (VideoConference: src/VideoConference/), aber
# package.json, NuGet.config und scripts/ gehören dem Repo.
dev_plugins_repo_root() {
    local scan_root="$1" plugin_root="$2"
    local rel="${plugin_root#"$scan_root"/}"
    printf '%s/%s' "$scan_root" "${rel%%/*}"
}

# Plattform-Pakete in den repo-eigenen local-feed. Jedes Plugin-Repo bringt das
# Skript selbst mit und liest die Version, gegen die es baut, aus seiner eigenen
# Directory.Packages.props — deshalb wird es aufgerufen und nicht nachgebaut.
dev_plugins_pack_platform() {
    local repo_root="$1" pack="$1/scripts/pack-callora.sh"

    if [ ! -f "$pack" ]; then
        return 0
    fi

    if [ -d "$repo_root/local-feed" ] && [ "${DEV_PLUGINS_REPACK:-0}" != "1" ]; then
        dev_plugins_step "Plattform-Pakete: local-feed vorhanden (--repack erneuert ihn)"
        return 0
    fi

    dev_plugins_step "Plattform-Pakete packen (pack-callora.sh)"
    # Kein harter Abbruch: der Feed ist das Mittel, die Assembly ist das Ziel. Ein
    # einzelnes unpackbares Projekt macht den Feed unvollständig, aber nicht
    # zwangsläufig unbrauchbar — fehlt ein Paket, das DIESES Plugin braucht,
    # scheitert der dotnet build weiter unten und genau das ist dann der Fehler.
    if ! bash "$pack" "$DEV_PLUGINS_ROOT_DIR"; then
        dev_plugins_warn "pack-callora.sh war nicht vollständig erfolgreich — der Feed in ${repo_root#"$DEV_PLUGINS_ROOT_DIR"/}/local-feed kann Pakete vermissen (Ausgabe oben)"
    fi
    return 0
}

# Frontend-Bundles. Welche es gibt, sagt das Repo in seinem build-Skript — eine
# Aufzählung hier würde beim nächsten hinzugefügten Bundle still veralten, und
# ein Plugin ohne Oberfläche fällt niemandem auf.
dev_plugins_build_frontend() {
    local repo_root="$1"

    if [ "${DEV_PLUGINS_NO_FRONTEND:-0}" = "1" ]; then
        return 0
    fi
    if [ ! -f "$repo_root/package.json" ] || ! grep -q '"build"' "$repo_root/package.json"; then
        return 0
    fi
    if ! command -v npm >/dev/null 2>&1; then
        dev_plugins_fail "npm fehlt, das Plugin deklariert aber ein build-Skript. Mit --no-frontend bewusst überspringen."
        return 1
    fi

    # `npm ci` bei JEDEM Lauf, nicht nur wenn node_modules fehlt.
    #
    # Vorher lief hier link-callora-npm.sh, weil @callora/admin und @callora/surface
    # nicht auf npm lagen: Es packte sie aus einem Checkout und spielte sie als Tarball
    # ein. Seit beide als 0.9.0 in der Registry liegen, ist das Skript nicht nur
    # überflüssig, sondern schädlich — es packte aus einem Geschwister-Checkout, der
    # tagealt sein durfte.
    #
    # Die alte Bedingung "nur wenn node_modules fehlt" war der zweite Teil desselben
    # Fehlers und muss mit weg: Der Composer baute vier Tage lang gegen einen
    # @callora/surface-Stand ohne bundle-readiness, weil sein node_modules existierte
    # und deshalb niemand mehr nachsah. `npm ci` ist Lock-treu und kostet zwei Sekunden
    # — billiger als ein Bundle, das gegen den falschen Vertrag gebaut ist und beim
    # Laden nichts sagt.
    if [ -f "$repo_root/package-lock.json" ]; then
        dev_plugins_step "npm ci"
        (cd "$repo_root" && npm ci --no-audit --no-fund) || return 1
    else
        # Ohne Lockfile auch bei jedem Lauf, und das ist keine Symmetrie um ihrer selbst
        # willen: Der Composer ist genau dieser Fall, und er ist das Repo, dem der Fehler
        # oben passiert ist. Hätte die Bedingung "nur wenn node_modules fehlt" hier
        # überlebt, wäre sie für das einzige Repo stehengeblieben, für das sie geschrieben
        # wurde.
        #
        # `npm install` ist nicht Lock-treu — es löst den Bereich aus package.json neu auf
        # und kann bei jedem Lauf etwas anderes einspielen. Das wird gesagt statt
        # weggelassen: Ein Bundle, dessen Abhängigkeiten niemand festgehalten hat, ist
        # nicht reproduzierbar, und der Weg dahin ist ein eingecheckter Lockfile.
        dev_plugins_warn "kein package-lock.json in ${repo_root##*/} — npm install löst neu auf statt lock-treu zu installieren"
        dev_plugins_step "npm install"
        (cd "$repo_root" && npm install --no-audit --no-fund) || return 1
    fi

    dev_plugins_step "npm run build"
    (cd "$repo_root" && npm run build --silent) || return 1
}

# Verträge, die dieses Plugin für andere bereitstellt. Erkannt am Namen
# (*.Abstractions.csproj): ein Abstractions-Projekt ist genau das — die
# Paketgrenze, gegen die ein anderes Plugin baut.
dev_plugins_pack_contracts() {
    local repo_root="$1"
    local props="$repo_root/Directory.Packages.props"
    local version=""
    local proj

    [ -f "$props" ] && version="$(grep -oP '(?<=<CalloraVersion>)[^<]+' "$props" | head -1)"

    while IFS= read -r proj; do
        [ -n "$proj" ] || continue
        dev_plugins_step "Vertrag packen: $(basename "$proj") ${version:+($version)}"
        local args=(-c Release --output "$DEV_PLUGINS_SHARED_FEED" --nologo -m:1 --tl:off)
        [ -n "$version" ] && args+=(-p:MinVerVersionOverride="$version")
        dotnet pack "$proj" "${args[@]}" >/dev/null || {
            dev_plugins_warn "pack fehlgeschlagen: $proj"
            continue
        }
        # Bei gleicher Versionsnummer gibt der globale Cache den ALTEN Inhalt
        # zurück; ein neu gepackter Vertrag käme sonst nie beim Konsumenten an.
        local pkg_id
        pkg_id="$(basename "$proj" .csproj)"
        [ -n "$version" ] && rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/${pkg_id,,}/$version"
    done < <(find "$repo_root" \
        \( -name node_modules -o -name bin -o -name obj -o -name local-feed -o -name .git \) -prune \
        -o -name '*.Abstractions.csproj' -print 2>/dev/null | sort)
}

dev_plugins_build_one() {
    local tier="$1" scan_root="$2" registry="$3"
    local plugin_root repo_root plugin_id assembly csproj

    plugin_root="$(dirname "$registry")"
    plugin_id="$(dev_plugins_json_value "$registry" pluginId)"
    assembly="$(dev_plugins_json_value "$registry" assemblyFileName)"

    if [ -z "$plugin_id" ] || [ -z "$assembly" ]; then
        dev_plugins_warn "übersprungen: ${registry#"$DEV_PLUGINS_ROOT_DIR"/} ohne pluginId/assemblyFileName (der Host überspringt sie ebenfalls)"
        return 0
    fi

    dev_plugins_is_selected "$plugin_id" || return 0

    repo_root="$(dev_plugins_repo_root "$scan_root" "$plugin_root")"

    # Der Host nimmt die erste csproj neben der registry.json (TopDirectoryOnly).
    csproj="$(find "$plugin_root" -maxdepth 1 -name '*.csproj' | sort | head -1)"
    if [ -z "$csproj" ]; then
        dev_plugins_fail "$plugin_id: keine csproj neben ${registry#"$DEV_PLUGINS_ROOT_DIR"/}"
        return 1
    fi

    dev_plugins_log "$plugin_id ($tier)"

    # Jeder Schritt mit explizitem `|| return`: diese Funktion wird aus
    # dev_plugins_run mit `|| return 1` aufgerufen, und in einem solchen Kontext
    # setzt bash `set -e` für den ganzen Funktionsrumpf aus. Ohne die Prüfungen
    # laufen fehlgeschlagene npm- und dotnet-Schritte stillschweigend durch — der
    # erste Testlauf meldete genau so ein Plugin als gebaut, dessen vite-Build
    # gescheitert war.
    dev_plugins_pack_platform "$repo_root" || return 1
    dev_plugins_build_frontend "$repo_root" || return 1

    dev_plugins_step "dotnet build ($DEV_PLUGINS_CONFIG)"
    # CopyLocalLockFileAssemblies: ohne das kopiert `dotnet build` bei einem
    # Bibliotheksprojekt KEINE NuGet-Abhängigkeit ins Ausgabeverzeichnis — die
    # deps.json fordert CalloraVoipSdk, Concentus, NAudio & Co., und keine davon
    # liegt da. Der Host findet das Plugin dann, lädt es, und der Konstruktor
    # stirbt an einer fehlenden Assembly: "Exception has been thrown by the target
    # of an invocation", ohne innere Ursache im Log.
    #
    # Die Distribution umgeht das mit `dotnet publish` in ein eigenes Verzeichnis.
    # Hier soll die Assembly aber in bin/ bleiben, weil genau dort die Discovery
    # im Dev-Fall sucht (LocalPluginDiscoveryService.ResolveAssemblyPath).
    #
    # Callora.* bleibt draußen — dafür sorgt die SDK-Regel des Plugin.Sdk-Pakets.
    # Alles andere kommt mit, auch was der Host ohnehin stellt (EF Core, Npgsql)
    # und das EF-Design-Tooling samt Roslyn, das ein Plugin für `dotnet ef` als
    # PackageReference führt. Zur Laufzeit harmlos: der Ladekontext löst
    # Framework-Assemblies auf die bereits geladene Host-Kopie auf. Wer den
    # Ballast loswerden will, setzt PrivateAssets="all" auf die Tooling-Referenz
    # im Plugin — das ist dessen Entscheidung, nicht die dieses Skripts.
    dotnet build "$csproj" \
        --configuration "$DEV_PLUGINS_CONFIG" \
        -p:RestoreAdditionalProjectSources="$DEV_PLUGINS_SHARED_FEED" \
        -p:CopyLocalLockFileAssemblies=true \
        --nologo --verbosity minimal \
        || { dev_plugins_fail "$plugin_id: dotnet build fehlgeschlagen"; return 1; }

    # Der Nachweis, der zählt: die Discovery sucht genau diese Datei unter bin/.
    # Ein grüner Build ohne sie hieße, der Host startet und das Plugin fehlt still.
    local built
    built="$(find "$plugin_root/bin" -name "$assembly" -type f 2>/dev/null | head -1)"
    if [ -z "$built" ]; then
        dev_plugins_fail "$plugin_id: Build grün, aber $assembly liegt nicht unter ${plugin_root#"$DEV_PLUGINS_ROOT_DIR"/}/bin"
        return 1
    fi

    if [ "$tier" = "system" ]; then
        dev_plugins_pack_contracts "$repo_root"
    fi

    DEV_PLUGINS_REPORT+=("$plugin_id|$tier|${built#"$DEV_PLUGINS_ROOT_DIR"/}")
    printf '   \033[32m✓\033[0m %s\n' "${built#"$DEV_PLUGINS_ROOT_DIR"/}"
}

dev_plugins_run() {
    DEV_PLUGINS_SHARED_FEED="$DEV_PLUGINS_ROOT_DIR/artifacts/dev-feed"
    mkdir -p "$DEV_PLUGINS_SHARED_FEED"
    DEV_PLUGINS_REPORT=()

    local found=0 scan_root tier registry
    # static-plugins zuerst: dieselbe Reihenfolge wie die Discovery, damit eine
    # Foundation ihre Verträge bereitstellt, bevor ein Konsument gebaut wird.
    for spec in "static-plugins:system" "plugins:application"; do
        scan_root="$DEV_PLUGINS_ROOT_DIR/custom/${spec%%:*}"
        tier="${spec##*:}"
        while IFS= read -r registry; do
            [ -n "$registry" ] || continue
            found=$((found + 1))
            dev_plugins_build_one "$tier" "$scan_root" "$registry" || return 1
        done < <(dev_plugins_find_registries "$scan_root")
    done

    if [ "$found" -eq 0 ]; then
        printf '\nKeine registry.json unter custom/static-plugins oder custom/plugins — nichts zu bauen.\n'
        return 0
    fi

    if [ ${#DEV_PLUGINS_REPORT[@]} -eq 0 ]; then
        printf '\n%s registry.json gefunden, aber keine passte zur Auswahl (--plugins).\n' "$found"
        return 0
    fi

    printf '\n\033[1mGebaute Plugins (%s)\033[0m\n' "${#DEV_PLUGINS_REPORT[@]}"
    local row
    for row in "${DEV_PLUGINS_REPORT[@]}"; do
        IFS='|' read -r id tier path <<<"$row"
        printf '  %-18s %-12s %s\n' "$id" "$tier" "$path"
    done
}
