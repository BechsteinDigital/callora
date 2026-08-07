# ops

Betriebs- und Testkonfiguration, die dieses Repository zum Bauen und Betreiben braucht.

| Pfad | Was |
|---|---|
| `spikes/asterisk-b4deep3/` | Asterisk-Konfiguration für die Interop-CI; `communication-interop.yml` mountet `pjsip.conf` und `extensions.conf` in den Container |
| `local-frontdoor/` | Caddyfiles für die lokale Frontdoor; `docker-compose.frontdoor.yml` mountet `Caddyfile.dev` |
| `shell-runtime/` | Caddy-Konfiguration für das statische Ausliefern der Shells |
| `runbooks/` | Betriebsanleitungen für den Host |
| `compliance/` | Begründete Ausnahmen zu `npm audit` |

## Was hier nicht mehr liegt

Specs, Pläne, Recherche, Analysen, Issues, das Audit und das Verarbeitungsverzeichnis
sind in das private Repository **`callora-ops`** gezogen, mit ihrer vollständigen
Historie.

Der Grund ist nicht Ordnung, sondern Sichtbarkeit: Ein öffentliches Repository legt
seine **gesamte Historie** offen, nicht nur den aktuellen Stand. Dort lagen die
Stundenrekonstruktion für die Forschungszulage, das Geschäftsmodell-Zielbild, die
Marketplace-Spec mit Provisionsmodell und Zahlungsanbieter und das
DSGVO-Verarbeitungsverzeichnis. Diese Dateien aus dem Kopf zu löschen hätte nichts
geändert — sie stünden weiterhin in jedem Commit, der sie enthielt.
