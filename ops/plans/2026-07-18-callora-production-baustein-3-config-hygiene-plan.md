# Baustein 3 — Config-Hygiene (Plan)

Datum: 2026-07-18 · Baustein 3/4 der Callora-Production-Setup-Spec
Repo: `/home/dbechstein/Projekte/Callora-Production` (Kern); ggf. Core für AllowedHosts

## Ziel

`docker compose up` bootet **ohne** `ASPNETCORE_ENVIRONMENT=Development` in eine
gehärtete Prod-Instanz: der fail-closed Startup-Guard (`BackendSecretHygiene`)
ist grün, keine Secrets mehr im eingecheckten `appsettings.json`, `.env` liefert
die echten Werte, `.env.example` dokumentiert sie.

## Guard-Mechanik (Ist)

`BackendSecretHygiene.Inspect` prüft auf die exakten Dev-Default-**Werte**
(JwtSigningKey, DemoAdmin-Passwort, DB-Passwort-Segment `password=callora`,
Bootstrap-API-Key). Außerhalb Development → Start verweigert; in Development nur
Warnung. Die `BackendHostOptions`-C#-Defaults SIND diese Dev-Defaults → fehlt ein
Secret in der Config, greift der Dev-Default → Prod verweigert (fail-closed).
Zweiter Guard: `EnableBootstrapApiKeys && RequireApiKeyAuthentication && ApiKeys
leer` → Start-Fehler (auch in Dev) → Dev braucht einen ApiKey.

## Kernentscheidungen (DECISION-Log)

1. **appsettings-Dreiteilung:**
   - `appsettings.json` (Basis): Nicht-Secret-Config. Die 4 Secrets raus
     (JwtSigningKey, DatabaseConnectionString, ApiKeys, DemoAdminUser).
   - `appsettings.Development.json`: nur was Dev braucht und C#-Defaults nicht
     abdecken → `ApiKeys: ["callora-local-dev-key-change-me"]` (sonst ApiKey-Guard-
     Crash) + `AllowPrivateWebhookTargets: true` (bestehend). JWT/DemoAdmin/DB
     fallen auf die C#-Dev-Defaults (Guard warnt nur in Dev).
   - `appsettings.Production.json` (NEU, KEINE Secrets, sichere Prod-Defaults):
     `DemoAdminUser.Enabled=false`, `AuthCookieRequireHttps=true`,
     `AllowPrivateWebhookTargets=false`.

2. **Secrets über `.env` → Compose-Env → Container:** rohe Secrets in `.env`
   (`POSTGRES_PASSWORD`, `CALLORA_JWT_SIGNING_KEY`, `CALLORA_API_KEY`). Das compose
   mappt sie auf `BackendHost__JwtSigningKey`, `BackendHost__ApiKeys__0` und baut
   die DB-Connection `Host=db;…;Password=${POSTGRES_PASSWORD}`. `${VAR:?msg}`
   erzwingt Präsenz → fehlt `.env`, bricht `docker compose up` (fail-closed).

3. **`.env` git-ignored, `.env.example` eingecheckt** mit Platzhaltern +
   Generierungshinweisen (`openssl rand -base64 48`).

4. **Kein Core-Change für den Secret-Kern:** Guard + Config-Binding existieren;
   Baustein 3 ist reine Konfiguration im Production-Repo.

5. **AllowedHosts/HostFiltering → Baustein 4 (ENTSCHIEDEN):** heute kein
   HostFiltering im Core; das Aktivieren ist eine Core-Middleware-Änderung
   (`UseHostFiltering` + Ordering + Tests), keine Config-Hygiene. Es hängt zudem
   an der Domain — genau das, was der Reverse-Proxy in Baustein 4 bringt (der
   Proxy filtert den Host-Header ohnehin; im App-Layer wäre es dort Defense-in-
   depth). Bewusst nach Baustein 4 vertagt, nicht weggelassen.

## Dateien

- `appsettings.json` (mod): 4 Secrets raus.
- `appsettings.Development.json` (mod): Dev-ApiKey ergänzen.
- `appsettings.Production.json` (neu): sichere Prod-Defaults.
- `.env.example` (neu): dokumentierte Platzhalter.
- `.gitignore` (mod): `.env` (falls nicht schon abgedeckt).
- `docker-compose.yml` (mod): Secret-Env-Mapping mit `${VAR:?}`, DB-Connection
  aus `POSTGRES_PASSWORD`.
- `README.md` (mod): Betreiber-Schritt „`.env` aus `.env.example` befüllen".

## Arbeitsschritte
1. Feature-Branch `feat/config-hygiene` (Callora-Production).
2. appsettings-Dreiteilung.
3. `.env.example` + `.gitignore`.
4. docker-compose Secret-Mapping.
5. AllowedHosts bewertet → nach Baustein 4 vertagt (s. Entscheidung 5).
6. Verify:
   - Prod-Boot mit einer Test-`.env`: `docker compose up` ohne Dev-Flag → Guard
     grün (kein „Refusing to start"), `/admin` HTTP 200, App läuft (Migration).
   - Negativ: ohne `.env` → `docker compose up` bricht (`${VAR:?}`), fail-closed.
   - Regression: Development-Boot (Baustein-2-Weg) weiter grün.
7. README; Reviewer; Merge.

## Durchstich-Kriterium (Spec-Erfolgskriterium 4)
Keine Secrets im eingecheckten `appsettings.json`; `.env.example` dokumentiert
alle nötigen Werte; `docker compose up` (Production) bootet gehärtet, `/admin`
lädt; ohne `.env` bricht der Start bewusst.

## Risiken
- **Dev-Regression:** die appsettings-Umschichtung darf den Development-Boot
  (DemoAdmin-Login) nicht brechen → im Verify beide Umgebungen testen.
- **DataProtection-Keys:** ephemer im Container → Cookies überleben Neustart
  nicht. Für den ersten Wurf akzeptiert (persistentes Volume = späteres Thema);
  im Plan vermerkt, nicht stillschweigend.
