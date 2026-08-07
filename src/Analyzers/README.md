# Callora.Analyzers

Die Roslyn-Analyzer, die Callora's Vertragsgrenze bewachen. Ein Plugin referenziert sie,
damit ein Grenzübertritt den Build bricht statt ein Review.

| Regel | Was sie verhindert |
|---|---|
| **CAL0001** | Zugriff auf `[CalloraInternal]`-Fläche aus einem Plugin |
| **CAL0002** | Ableiten von Typen, die nicht zur Ableitung freigegeben sind |
| **CAL0003** | Vertragsfläche ohne Dokumentation |

```xml
<PackageReference Include="Callora.Analyzers" Version="0.9.0" PrivateAssets="all" />
```

`PrivateAssets="all"` ist richtig: Der Analyzer prüft deinen Code, er gehört nicht zu dem,
was dein Plugin weitergibt.

Framework-Assemblies der Plattform setzen `CalloraFrameworkAssembly=true` und dürfen die
interne Fläche konsumieren. Alles andere — insbesondere Plugins — wird geprüft.
