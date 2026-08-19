# syntax=docker/dockerfile:1
#
# Zwei Stufen für zwei Fragen.
#
#   dev         "ich entwickle daran"  — Werkzeug ohne Quelle, das Repo kommt per Mount.
#                                        docker-compose.yml
#   standalone  "zeig mir das System"  — Quelle rein, alles gebaut, nichts vorausgesetzt.
#                                        docker-compose.standalone.yml
#
# Beide tragen .NET UND Node. Das ist der Punkt, an dem sich hier etwas ändert: Der
# Dev-Container lief bisher auf dem nackten SDK-Image, und weil dem npm fehlte,
# mussten die Frontends auf dem Host gebaut werden — wer .NET im Container hatte,
# brauchte Node trotzdem lokal. Beide Vue-Suiten hängen als MSBuild-Target am
# jeweiligen Projekt (BuildAdminFrontend / BuildSurfaceFrontend) und rufen npm auf;
# ohne npm im Image endet jeder vollständige Build im Container mit "npm: not found".
#
# Dies ist ausdrücklich KEIN Produktionsbild. Gestartet wird src/Host/Dev — die
# einzige lauffähige Zusammenstellung dieses Repositories und laut CLAUDE.md kein
# Produkt. Eine Distribution komponiert die Pakete selbst; das tut callora-production.

# ---------------------------------------------------------------------------
# dev — SDK + Node, sonst nichts. Bewusst ohne COPY: Die Quelle liegt im Mount,
# und ein eingebackener Stand wäre der zweite, der dann von dem im Mount abweicht.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev

# Node aus dem offiziellen Image statt über ein Setup-Skript von nodesource: Das
# Skript will apt-Quellen umschreiben und einen Schlüssel holen, also zwei
# Netzabhängigkeiten mehr in einem Build, der sonst nur Basis-Images zieht.
# Version 22, weil @callora/admin `engines.node >=22.6` deklariert und die
# Workflows auf 22 laufen.
COPY --from=node:22-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -sf ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
 && ln -sf ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
 && node --version && npm --version

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# ---------------------------------------------------------------------------
# standalone — Quelle rein, Host und jedes geklonte Plugin bauen.
# ---------------------------------------------------------------------------
FROM dev AS standalone
WORKDIR /src

# MinVer leitet die Version aus Git-Tags ab. Im Image gibt es kein .git (siehe
# .dockerignore), und ohne Tag greift der Rückfall auf 0.1.0-preview.0.<höhe> —
# woraufhin der Host jedes gefundene Plugin ablehnt: "Plugin dependency
# 'Callora.Core' requires '>=0.9.0-0', but the host provides 0.1.0-preview.0".
# 0.9.0 ist die Release-Version, die als Tag v0.9.0 auf GitHub liegt. Die Plugin-Repos
# pinnen in ihrer Directory.Packages.props noch auf 0.9.0-rc.25; das trägt, weil sie
# `>=0.9.0-0` fordern und 0.9.0 darüber liegt. Beim Nachziehen der Pins hier mitgehen.
#
# Das MUSS beim Bauen gesetzt sein, nicht beim Starten: MSBuild liest die Variable
# als Property, und die Version ist danach in der Assembly. Als Laufzeit-Umgebung
# in der compose käme sie zu spät und der Host stünde wieder ohne Plugins da —
# ohne Fehler beim Bauen, nur mit sechs Ablehnungen im Startprotokoll.
ARG CALLORA_VERSION=0.9.0
ENV MinVerVersionOverride=${CALLORA_VERSION}

COPY . .

# Ein Aufruf, kein nachgebauter Ablauf. dev-build.sh baut die Solution (und damit
# über die MSBuild-Targets beide Frontends) und danach jedes Plugin, das unter
# custom/ eine registry.json hat. Es gibt dort keine Plugin-Liste — gebaut wird,
# was der Host beim Start ebenfalls findet. Wer keine Plugins geklont hat, bekommt
# einen Host ohne Plugins, und das ist ein gültiger Zustand, kein Fehler.
RUN scripts/dev-build.sh --configuration Release

# Die Discovery löst ihre Scan-Roots gegen das ARBEITSVERZEICHNIS auf
# (CalloraHostingPathResolver), und die Plugin-Assemblies liegen nach dem Build
# unter custom/<plugin>/bin/. Deshalb bleibt /src stehen und wird nicht in ein
# schlankes Runtime-Image publiziert: Ein publish müsste diese Struktur von Hand
# nachbauen, und genau das ist der Job der Distribution, nicht der eines
# Entwicklerbildes, das ehrlich sein soll über das, was es ist.
#
# --no-build, weil oben schon gebaut wurde; ohne das Flag baut `dotnet run` beim
# Start erneut und der erste Aufruf dauert Minuten.
EXPOSE 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENTRYPOINT ["dotnet", "run", "--project", "src/Host/Dev/Callora.Host.Dev.csproj", \
            "--configuration", "Release", "--no-build", "--no-launch-profile"]
