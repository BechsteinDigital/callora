# Spike: ID-Value-Objects — Reibungsmessung (2026-07-17)

**Frage:** Wie teuer ist die Grenzen-Reibung eines ID-Value-Objects im .NET-10-Stack
wirklich? (Entscheidungsgrundlage: `PluginId`/`TenantKey`/`WorkspaceKey` als Typen —
ChatGPT-Empfehlung — vs. `string` bleiben — R2-Entscheidung.)

**Aufbau:** Isoliertes Wegwerf-Web-Projekt (`/tmp/id-vo-spike`, net10.0), ein `WorkspaceKey`
in zwei Varianten durch vier Grenzen gezogen:
- **Variant A — Vogen 8.0.6** (Source-Generator, MIT-Lizenz, net10-kompatibel): `[ValueObject<string>(conversions: SystemTextJson | EfCoreValueConverter)]`
- **Variant B — DIY** (`readonly record struct` + handgeschriebener `IParsable`, `JsonConverter`, `ValueConverter`)

## Ergebnis pro Grenze

| Grenze | Vogen (naiv) | DIY (naiv) |
| --- | --- | --- |
| Build | ✅ 0 Fehler | ✅ 0 Fehler |
| EF persist + read-back | ✅ 1 Zeile `HasConversion<WsVo.EfCoreValueConverter>` | ✅ 1 Zeile + 4-Zeilen-Converter-Klasse |
| EF query-by-value (`.Where(d => d.Key == x)`) | ✅ zu SQL übersetzt | ✅ zu SQL übersetzt |
| Minimal-API Route-Binding (`{key}`) | ✅ **automatisch** (Vogen generiert `IParsable`) | ✅ ~10 Zeilen `IParsable` von Hand |
| JSON body round-trip | ✅ als `string` | ✅ als `string` (Converter + Attribut) |
| OpenAPI Route-Param-Schema | ✅ `type: string` | ✅ `type: string` |
| OpenAPI Body-Property-Schema | ⚠️ `{}` (leer) | ⚠️ `{}` (leer) |

## Erkenntnisse

1. **Die Reibung ist gering — geringer als die R2-Abwägung befürchtete.** Alle vier
   Grenzen funktionieren; mit Vogen weitgehend aus einem Attribut heraus (EF-Converter,
   JSON-Converter UND Route-Binding generiert).
2. **Der einzige echte Fallstrick ist für beide Varianten identisch:** das OpenAPI-Body-
   Property-Schema erscheint als leeres `{}` statt `type: string` (der .NET-OpenAPI-Generator
   sieht den Struct hinter dem JsonConverter nicht). Fix = **ein einmaliger Schema-Transformer**
   (~15 Zeilen, global, nicht pro Typ) — kein Blocker.
3. **Vogen vs. DIY:** Vogen ≈ 1 Attribut-Zeile/Typ; DIY ≈ 20 Zeilen Boilerplate/Typ, aber
   keine Dependency. Bei mehreren ID-Typen gewinnt Vogen deutlich an DX.

## Konsequenz für die Entscheidung

- Das Argument **„Grenzen-Reibung zu hoch"** (Hauptgegengrund gegen VOs bei R2) ist
  **widerlegt.** Value-Objects sind im .NET-10-Stack tragbar.
- Übrig bleiben als echte Faktoren: (a) einmaliger **Migrationsaufwand** der ~1850
  bestehenden `string`-Nutzungen (der Spike testete nur *einen* Typ auf grüner Wiese —
  die Massen-Migration der Call-Sites ist ungemessen), (b) **Vogen als Dependency**
  (Bau-vs-Kauf; aktuell MIT, aktiv), (c) **Peer-Untypik** (kein .NET-Portal nutzt ID-VOs).
- **Neue Surface-IDs** (SurfaceKey/RealmId/TemplateId): grüne Wiese, kein Migrationsaufwand,
  Reibung gering → klarer Fall **für** VOs bei Entstehung.
- **Bestehende drei Kern-IDs**: Entscheidung hängt jetzt nur noch an Migrationsaufwand vs.
  Verständlichkeitsgewinn — nicht mehr an Reibung.

Spike-Code verworfen (`/tmp`, nicht eingecheckt).
