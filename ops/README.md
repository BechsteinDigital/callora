# ops

Betriebs- und Testkonfiguration, die dieses Repository zum Bauen und Betreiben braucht.

| Pfad | Was |
|---|---|
| `local-frontdoor/` | Caddyfile für die lokale Frontdoor; `docker-compose.frontdoor.yml` mountet es |
| `runbooks/` | Betriebsanleitungen für den Host |
| `compliance/` | Begründete Ausnahmen zu `npm audit` |

Der Asterisk-Spike und die Shell-Runtime-Konfiguration sind entfallen: Der Spike gehörte
zur Communication-Interop-CI, die mit dem Plugin in dessen Repository gezogen ist
(ADR-020), und statisch ausgelieferte Shells gibt es nicht mehr — die Admin-Oberfläche
liegt colocated im Administration-Modul, die öffentliche Fläche rendert
`Callora.Surface.Rendering`.

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
