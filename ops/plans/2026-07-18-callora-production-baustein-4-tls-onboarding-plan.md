# Baustein 4 — Reverse-Proxy/TLS + Onboarding (Plan)

Datum: 2026-07-18 · Baustein 4/4 (letzter) der Callora-Production-Setup-Spec
Repo: `/home/dbechstein/Projekte/Callora-Production`

## Ziel

Ein Betreiber setzt Domain + Secrets in `.env`, richtet DNS auf den Server,
`docker compose up` — und hat Callora unter `https://seine.domain` mit
automatischem TLS. Plus ein Onboarding-README für Betreiber und Entwickler.

## Kernentscheidungen (DECISION-Log)

1. **Caddy als Reverse-Proxy** (auto-HTTPS/Let's Encrypt out-of-the-box, minimaler
   Caddyfile). Colocated → nur EIN Upstream: Caddy terminiert TLS und proxied
   ALLES an `app:8080` (kein Multi-Shell-Routing wie im alten `prod-like`).

2. **App-Port nicht mehr extern exponiert.** Nur Caddy veröffentlicht 80/443;
   der App-Container ist nur im internen Compose-Netz erreichbar. Reduziert die
   Angriffsfläche und macht Caddy zur einzigen Ingress-Stelle.

3. **AllowedHosts via Config, kein Core-Change (zu verifizieren):** ASP.NET
   HostFiltering ist über die `WebApplication`-Defaults aktiv und liest den
   Top-Level-`AllowedHosts`-Key. Wir setzen ihn aus `CALLORA_DOMAIN`. Greift das
   nicht (Callora deaktiviert HostFiltering), wird es ein kleiner Core-Change
   (`UseHostFiltering`) + Re-Pack — beim Bau entscheiden.

4. **Eine Domain-Variable `CALLORA_DOMAIN`** (+ `ACME_EMAIL` für Let's Encrypt)
   in `.env` → an Caddyfile (TLS + Routing) UND `AllowedHosts` (App) gereicht.

5. **Persistente Caddy-Daten** (Volume) für die ausgestellten Zertifikate —
   sonst neue ACME-Runde bei jedem Neustart (Rate-Limits).

6. **Ports parametrisierbar** (`CALLORA_HTTP_PORT`/`CALLORA_HTTPS_PORT`, Default
   80/443) — für den lokalen Verify (80/443 sind hier belegt) und flexible Hosts.

## Dateien

- `Caddyfile` (neu): `{$CALLORA_DOMAIN} { reverse_proxy app:8080 }` (+ ACME-Email).
- `docker-compose.yml` (mod): `caddy`-Service (Ports, Caddyfile-Mount, Volumes,
  `CALLORA_DOMAIN`/`ACME_EMAIL`); `app` verliert das externe Port-Mapping, bekommt
  `AllowedHosts` aus `CALLORA_DOMAIN`.
- `.env.example` (mod): `CALLORA_DOMAIN`, `ACME_EMAIL` ergänzen.
- `README.md` (mod): vollständiges Onboarding (Betreiber: Domain, DNS, `.env`, up;
  Dev: localhost).

## Arbeitsschritte
1. Feature-Branch `feat/tls-onboarding`.
2. `Caddyfile` + compose-`caddy`-Service; `app`-Port-Exposition entfernen.
3. `AllowedHosts` aus `CALLORA_DOMAIN` an `app` → beim Verify prüfen, ob
   HostFiltering ohne Core-Change greift.
4. `.env.example` erweitern.
5. Verify (lokal, `CALLORA_DOMAIN=localhost`, Ports 8880/8443):
   - Caddy startet, terminiert TLS (internes CA für localhost), `curl -k
     https://localhost:8443/admin/` → SPA HTTP 200.
   - App ist NICHT direkt erreichbar (kein externes Port-Mapping).
   - HostFiltering: guter Host → 200, fremder Host-Header → 400 (Core-Change nur
     falls nötig).
6. `README.md` Onboarding.
7. Reviewer; Merge; dann Push (beide Repos) — vom User so gewünscht.

## Durchstich-Kriterium (Spec-Erfolgskriterium 5 + TLS)
`docker compose up` bringt Caddy + App + Postgres hoch; `/admin` lädt über HTTPS;
der App-Container ist nur über den Proxy erreichbar; README führt beide Personas
in wenigen Schritten zum laufenden System.

## Risiken
- **Let's Encrypt lokal nicht testbar** (braucht echte Domain + öffentliches DNS).
  Lokal via `CALLORA_DOMAIN=localhost` + Caddys internem CA verifiziert; der echte
  ACME-Flow wird dokumentiert, nicht lokal durchgefahren (ehrlich ausweisen).
- **AllowedHosts greift evtl. nicht** ohne Core-Change → Schritt 3 verifiziert das
  empirisch; Ergebnis bestimmt, ob ein Core-Re-Pack nötig wird.
- **80/443 lokal belegt** (shopware_nginx) → parametrisierte Ports für den Verify.

## Offene Low-Follow-ups (aus Reviewer, nicht blockierend)
- **App-Healthcheck** für sauberes Caddy-Start-Ordering: der `app`-Service hat
  keinen Healthcheck, Caddy wartet nur auf `service_started` → transiente 502 beim
  allerersten Boot, bis die App lauscht (self-healing, Caddy retryt). Ein echter
  Healthcheck braucht ein Tool im schlanken `aspnet`-Image (curl/wget o. ä.) →
  bewusst vertagt, Scope-Erweiterung.
