# Callora.Cli

Das Werkzeug, mit dem ein Callora-Plugin entsteht, geprüft und für die Auslieferung
signiert wird.

```bash
dotnet tool install -g Callora.Cli
```

## Drei Schritte

```bash
# 1 — Gerüst anlegen: csproj, registry.json, Einstiegstyp
callora plugin new MeinPlugin --id mein-plugin

# 2 — gegen den Plattform-Vertrag prüfen: Manifest, Einstiegstyp, Lebenszyklus
callora plugin test-contract --assembly bin/Release/net10.0/MeinPlugin.dll

# 3 — signieren; ohne Signatur lädt keine Distribution das Plugin
callora plugin sign --plugin . --key mein-schluessel.pem
```

`plugin test-contract` lädt die Assembly in einen eigenen Ladekontext und prüft, was der
Host beim Installieren prüfen würde — nur eben auf deiner Maschine statt beim Kunden.

Die Signatur ist ein Content-Manifest über ECDSA-P256: Sie deckt jede ausgelieferte Datei
ab, nicht nur die Assembly. Vertrauen entsteht über den Fingerabdruck des öffentlichen
Schlüssels, den der Betreiber hinterlegt.
