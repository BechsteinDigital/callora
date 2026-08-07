# Callora.Plugin.Sdk

Alles, was ein Callora-Plugin zum Bauen braucht — in einer Referenz.

```xml
<PackageReference Include="Callora.Plugin.Sdk" Version="0.9.0" />
```

Das bringt mit:

- **`Callora.Core`** — die Vertragsfläche, gegen die du kompilierst
- **`Callora.Analyzers`** — CAL0001–0003, damit ein Grenzübertritt den Build bricht statt
  ein Review
- **Build-Regeln**, die Plattform-Assemblies aus deinem Ausgabeordner heraushalten

## Warum die dritte Zeile die wichtigste ist

Zur Laufzeit leitet Callora jede Assembly namens `Callora` oder `Callora.*` an den
Ladekontext des Hosts weiter, damit Host und Plugin dieselben Vertragstypen benutzen.
Läge daneben eine eigene Kopie von `Callora.Core.dll`, gäbe es denselben Typ zweimal —
und der Fehler fiele erst beim Laden auf, mit einer Meldung, die nach einem Fehler des
Hosts aussieht.

Bisher musste jedes Plugin dafür `ExcludeAssets="runtime"` von Hand setzen. Dieses Paket
nimmt dir das ab: Der Ausgabeordner bleibt frei von Plattform-Assemblies, egal wie du
referenzierst.

`CalloraVoipSdk` ist davon ausgenommen — kein Punkt hinter `Callora`, also plugin-lokal,
zur Laufzeit wie beim Bauen.

## Ein Plugin anlegen

```bash
dotnet tool install -g Callora.Cli
callora plugin new MeinPlugin --id mein-plugin
```
