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
