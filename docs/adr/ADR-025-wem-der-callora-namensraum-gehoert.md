# ADR-025 — Wem der `Callora.`-Namensraum gehört

**Status:** Proposed
**Datum:** 2026-08-16
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-012 — Single-Core-Extensibility (Typidentität, geteilter Load-Context)
* ADR-020 — Repo-Schnitt und Paketgrenzen (§3: vier Fehler an der Paketkante)
* `PluginAssemblyLoadContext` / `SharedContractAssemblyRegistry` (PLAT-256)
* Anlass: `callora-communication` #41 — `dotnet publish` ohne die eigene Haupt-DLL

---

## 1. Kontext

Der Ladekontext eines Plugins beantwortete die Frage „gehört diese Assembly dem Host?"
über das Namenspräfix: Jeder Name, der `Callora` ist oder mit `Callora.` beginnt, wurde
in den Default-Kontext des Hosts geleitet. Dieselbe Regel stand in drei Fassungen im
System — im Ladekontext, in der Contract-Registry und als Build-Filter im
`Callora.Plugin.Sdk`.

Die Regel setzt eine Annahme voraus, die nie ausgesprochen wurde: dass **alles** unter
`Callora.` vom Host kommt. Das trifft für Plattform-Assemblies zu. Es trifft nicht zu für
Plugins, die Bechstein.Digital selbst baut — die heißen `Callora.Plugin.<Name>`, und sie
bringen eigene Vertrags-Assemblies mit (`Callora.Plugin.Communication.Abstractions`), die
kein Host stellt.

Wo die Annahme brach, brach sie dreimal auf unterschiedliche Weise:

| Stelle | Wirkung |
|---|---|
| Build-Filter (SDK) | Das Plugin filterte sich aus seinem eigenen Ausgabeordner. `dotnet publish` lieferte ein Verzeichnis ohne Haupt-DLL aus; der Signierschritt fand die deklarierte Assembly nicht. |
| Ladekontext | Eine plugin-eigene `Callora.*`-Vertrags-Assembly wurde in den Default-Kontext geleitet, der sie nicht hat → `FileNotFoundException` beim ersten Typzugriff. |
| Contract-Registry | Dieselbe Deklaration wurde mit der Auflage abgewiesen, die Assembly umzubenennen. |

Ein Fremdanbieter-Plugin (`Acme.Plugin.Foo`) trifft keinen der drei Fälle. Der Fehler war
also **ausschließlich** bei Erstanbieter-Plugins sichtbar — und blieb dort lange
unsichtbar, weil interne Plugins im Dev-Host statisch geladen werden und den
Ladekontext gar nicht durchlaufen.

## 2. Entscheidung

**Interne Plugins bleiben im `Callora.`-Namensraum.** Das Präfix ist eine Herkunftsangabe
des Herstellers, keine Reservierung für den Host.

Daraus folgt: **Kein Mechanismus darf aus dem Namen ableiten, wer eine Assembly stellt.**
Wo diese Frage beantwortet werden muss, wird sie an der Quelle beantwortet, die sie
tatsächlich kennt:

1. **Bauzeit** (`Callora.Plugin.Sdk.targets`): `%(NuGetPackageId)`. Gesetzt heißt
   Paket-Asset und damit Plattform; leer heißt eigene Bauausgabe oder eigene
   ProjectReference und bleibt.
2. **Ladezeit** (`PluginAssemblyLoadContext.Load`): der Default-Kontext selbst. Die
   Reihenfolge ist geteilte Verträge → was der Host stellt → plugin-lokal. `Callora.Core`
   löst auf die Host-Kopie auf, weil der Host sie referenziert; eine plugin-eigene
   `Callora.Plugin.X.Abstractions` landet plugin-lokal, weil niemand sonst sie hat.
3. **Registrierung** (`SharedContractAssemblyRegistry`): dieselbe Frage an denselben
   Kontext. Hat der Prozess die Assembly schon, wird sie aufgezeichnet, aber nicht
   geladen; sonst wird sie geteilt.

## 3. Warum so

### Warum keine Allowlist der Host-Assemblies

Eine gepflegte Liste (`Callora.Core`, `Callora.Workspace`, `Callora.Administration`, …)
wäre exakt und im Zweifel lesbarer. Sie driftet aber genau dann, wenn es darauf ankommt:
Wer eine Assembly zur Plattform hinzufügt, denkt nicht an eine Liste in einem Ladekontext,
und der Fehler fällt erst beim Laden auf. Der Default-Kontext ist dieselbe Aussage, ohne
Pflegeaufwand — er *ist* die Menge dessen, was der Host stellt.

### Warum der Frühausstieg nichts geschützt hat, was der Fallback nicht schützt

Der Verdacht liegt nahe, dass die Präfix-Regel eine Sicherheitsgrenze war: Ein Plugin
könnte eine eigene `Callora.Core.dll` mitliefern und Host-Typen ersetzen. Das verhindert
der Fallback ebenso, und zwar früher — `TryResolveFromDefault` wird vor der plugin-lokalen
Auflösung gefragt und liefert die Host-Kopie, solange der Host die Assembly hat. Der
Frühausstieg schützte also nur den Fall, in dem der Host sie **nicht** hat — und genau
dieser Fall ist der erwünschte.

### Warum die Registry-Prüfung breiter wird statt schmaler

Die Präfix-Prüfung war nicht nur zu breit, sie war an anderer Stelle zu schmal: Für jeden
Namen **ohne** `Callora.` fand gar keine Host-Prüfung statt. Ein Plugin, das
`Microsoft.Extensions.Logging.Abstractions.dll` unter `contracts` deklarierte, bekam seine
Kopie neben die des Hosts in den Default-Kontext geladen. Dieselbe Frage für alle Namen zu
stellen schließt diese Lücke, statt eine zweite Sonderregel dafür zu erfinden.

### Der Kern rechnete längst mit dieser Entscheidung

`CuratedPluginServiceProvider.IsPublishedContract` gibt einen Typ frei, wenn seine Assembly
`Callora.Plugin.*` heißt und auf `.Abstractions` endet — mit dem Kommentar, solche
Foundation-Contract-Pakete seien „unified in the shared load context and published
cross-plugin". Genau diese Assemblies hat die Registry bis jetzt abgewiesen und der
Ladekontext an den Host weitergereicht, der sie nicht hat.

Der Kern widersprach sich also selbst: Eine Stelle setzt voraus, dass plugin-eigene
Vertrags-Assemblies im `Callora.`-Raum geteilt werden, zwei andere machten es unmöglich.
Diese ADR ändert die Architektur nicht, sie stellt sie her.

### Was diese Entscheidung für den Marktplatz bedeutet

Sie ändert für Fremdanbieter nichts — deren Assemblies waren von keiner der drei Regeln
betroffen. Sie beseitigt aber die Asymmetrie, dass Erstanbieter-Plugins anders behandelt
wurden als Drittanbieter-Plugins, allein wegen ihres Namens. Ein Testaufbau, der ein
Plugin `Acme.*` nennt, deckt danach denselben Pfad ab wie ein interner Name; vorher fand
er keinen der drei Fehler.

## 4. Konsequenzen

Positiv:

* Interne Plugins können eigene Vertrags-Assemblies mitliefern und unter `contracts`
  deklarieren, ohne sie umzubenennen.
* Eine Regel weniger, die an zwei Stellen synchron gehalten werden muss. Der Kommentar im
  Build-Filter verlangte ausdrücklich, ihn „zeichengenau" mit dem Ladekontext zu spiegeln —
  diese Kopplung entfällt, weil beide Seiten dieselbe Frage jetzt an ihre eigene Quelle
  stellen.
* Die Host-Prüfung der Registry gilt für alle Namen statt nur für `Callora.*`.

Tradeoffs:

* Die Aussage „Callora.* kommt immer vom Host" ist als Faustregel nicht mehr wahr. Wer sie
  im Kopf hat, liest den Ladepfad falsch. Deshalb steht sie in keinem Kommentar mehr, und
  die Stellen, die sie trugen, benennen den Grund ihrer Ablösung.
* Die Reihenfolge im Ladekontext wird bedeutungstragend: Die Shared-Registry steht vor dem
  Default-Kontext, weil nur sie die Major-Version-Prüfung trägt. Dass darüber keine
  host-gestellte Assembly unterschoben werden kann, stellt die Registry sicher — nicht
  mehr der Ladekontext. Diese Kopplung ist neu und in beiden Dateien vermerkt.

## 5. Abgrenzung

Nicht Gegenstand dieser Entscheidung:

* Ob `Callora.Contracts.*` als eigener Namensraum für geteilte Verträge weitergeführt wird
  (`CuratedPluginServiceProvider` kennt ihn) — davon ist hier nichts berührt.
* Die Frage, ob Erstanbieter-Plugins eigene Paket-IDs im `Callora.`-Raum auf nuget.org
  belegen sollen. Das ist eine Vertriebsentscheidung, keine Ladezeitfrage.

## 6. Offen

* `registry.json` der internen Plugins deklariert die mitgelieferte Vertrags-Assembly heute
  unter `dependencies` (ein Versionsgate) statt unter `contracts` (der Ladepfad). Solange
  das so ist, wird sie nicht geteilt. Das ist nach dieser ADR möglich, aber noch nicht
  getan — ein Umzug pro Plugin-Repository.
