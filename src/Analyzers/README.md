# Callora.Analyzers

Die Roslyn-Analyzer, die Callora's Vertragsgrenze bewachen. Ein Plugin referenziert sie,
damit ein Grenzübertritt den Build bricht statt ein Review.

| Regel | Was sie verhindert | Gilt für |
|---|---|---|
| **CAL0001** | Zugriff auf `[CalloraInternal]`-Fläche | Plugin-Kompilierungen |
| **CAL0002** | Ableiten von oder Implementieren eines `[CalloraInternal]`-Typs | Plugin-Kompilierungen |
| **CAL0003** | Vertragsfläche ohne XML-Dokumentation | jede Kompilierung |
| **CAL0004** | Roher String statt einer `CalloraExtensionPoints`-Konstante an einem `[ExtensionPointId]`-Parameter | jede Kompilierung |

Ausführlich, mit Beispielen und Unterdrückungsregeln:
[Analyzer rules](../../docs-site/reference/analyzer-rules.md).

```xml
<PackageReference Include="Callora.Analyzers" Version="0.9.0" PrivateAssets="all" />
```

`PrivateAssets="all"` ist richtig: Der Analyzer prüft deinen Code, er gehört nicht zu dem,
was dein Plugin weitergibt.

Framework-Assemblies der Plattform setzen `CalloraFrameworkAssembly=true` und dürfen die
interne Fläche konsumieren. Alles andere — insbesondere Plugins — wird geprüft.
