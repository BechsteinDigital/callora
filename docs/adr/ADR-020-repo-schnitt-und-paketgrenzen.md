# ADR-020 — Repo-Schnitt: Verträge öffentlich, Implementierungen privat

**Status:** Accepted
**Datum:** 2026-08-07
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-012 — Ein-Core-Extensibility (domänenneutrale Plattform)
* ADR-013 — Trust-Modell „Trusted in-process by Provenance"
* `LICENSE` (Apache-2.0), `CONTRIBUTING.md`

---

## 1. Kontext

Callora wird öffentlich und geht als Version 0.9 (Release Candidate) nach nuget.org. Die
Plugins, mit denen Geld verdient wird — heute Communication und Composer —, bleiben
proprietär und ziehen in eigene, private Repositories.

Damit stellt sich eine Frage, die vorher keine war: **Was darf ein öffentliches
Repository referenzieren?** Solange alles in einem Monorepo lag, war jede
`ProjectReference` gleich billig. Nach dem Schnitt entscheidet jede einzelne darüber, ob
der Klon eines Außenstehenden restauriert.

Erschwerend: Die Frage ist nicht nur juristisch, sondern technisch. Der Plugin-
Ladekontext leitet jede Assembly namens `Callora` oder `Callora.*` an den Default-Kontext
weiter, damit Host und Plugin dieselben Vertragstypen benutzen. Der Vertrag eines Plugins
muss also im Prozess des Hosts liegen — die Distribution *muss* ihn referenzieren.

## 2. Entscheidung

**Die Repo-Grenze verläuft entlang der Vertrauens- und Lizenzgrenze, nicht entlang der
heutigen Ordner.** Daraus folgt eine Regel, aus der alles andere fällt:

> **Verträge sind öffentlich, Implementierungen sind privat.**

| Repository | Inhalt | Sichtbar | Lizenz |
|---|---|---|---|
| `callora` | Core, Administration, Workspace, Surface.Rendering, Analyzers, CLI, Plugin.Sdk | öffentlich | Apache-2.0 |
| `callora-communication` | Voice-Plugin **+ sein Vertragspaket** | privat | proprietär / Vertrag Apache-2.0 |
| `callora-composer` | Flächen-Editor | privat | proprietär |
| `callora-ops` | Specs, Pläne, Recherche, Geschäftsunterlagen | privat | — |
| `Callora-Production` | Distribution | privat | — |

**Ein Repository je verkaufbarer Einheit.** Version, Release-Tag, Signatur, Preis und
Entitlement laufen pro Plugin; ein Plugin-Monorepo zwänge Communication und Composer in
einen Versionsstrang, den sie nicht teilen, und ließe die 30-minütige Asterisk-CI bei
jeder Composer-Änderung anlaufen.

### 2.1 Das Vertragspaket bleibt beim Plugin

Ein privates Repository darf ein **öffentliches** Paket veröffentlichen.
`Callora.Plugin.Communication.Abstractions` geht Apache-2.0 nach nuget.org, während
`Callora.Plugin.Communication` in einen privaten Feed geht — aus demselben Repository, in
einem Release.

**Verworfen:** ein eigenes `callora-contracts`-Repository. Es klang sauberer, kostet aber
genau das, was es zu vermeiden vorgibt. Ein Vertrag ändert sich, wenn das Produkt sich
ändert; läge er woanders, wäre jede Vertragserweiterung ein zweistufiger Release über
Repo-Grenzen hinweg. Und für den Zweck — ein Dritter baut ein eigenes Voice-Plugin gegen
`ICommunicationChannelRegistry` — genügt ein öffentliches Paket vollständig: Es trägt
XML-Doku, und `EmbedAllSources` legt den Quelltext ins Symbolpaket, ohne dass das
Repository offen sein muss.

**Verworfen:** den Vertrag ins `callora`-Repo zu ziehen. Dann bestimmte der Takt der
Plattform, wann ein Produktvertrag wachsen darf.

### 2.2 Die Plattform darf Verträge referenzieren, Implementierungen nie

Bestehende und erlaubte Kanten:

* `Host.Dev` → `Communication.Abstractions` — die Dev-Distribution bringt den Vertrag in
  den Default-Ladekontext, sonst bricht die Typidentität beim ersten Plugin-Laden.
* `Host.Cli` → `Communication.Abstractions` — der Inspektionskontext von
  `plugin test-contract` gibt bei unbekannten Assemblies `null` zurück und fällt damit
  ebenfalls auf den Default-Kontext.

Nach dem Schnitt werden beide zu `PackageReference` auf das öffentliche Vertragspaket.
Das öffentliche Repository hängt damit an einem öffentlichen Paket — nicht an einem
privaten Repository.

Erzwungen wird das nicht durch Disziplin, sondern durch
`PlatformDependsOnPluginContractsOnlyTests`: Jede `ProjectReference` aus `src/` nach
`custom/` muss auf ein Projekt zeigen, dessen Name auf `.Abstractions` endet. Die Regel
fällt auf, sobald jemand sie schreibt — nicht erst, wenn ein Außenstehender nicht
restaurieren kann.

### 2.3 Die Frontend-Pakete werden veröffentlicht, nicht ausgegliedert

`@callora/surface` und `@callora/admin` gehen öffentlich nach npm, bleiben aber im
`callora`-Repository. **Publizieren ist nicht dasselbe wie Ausgliedern.**
`@callora/surface` ist die Client-Hälfte desselben Vertrags, dessen Server-Hälfte
`Callora.Surface.Rendering` ist; driften sie auseinander, merkt das nur ein gemeinsamer
Build.

Der Grund, ein npm-Paket in ein eigenes Repository zu heben, ist externe
Beitragsfähigkeit. Solange keine Dritt-PRs kommen, zahlt man Synchronisationskosten ohne
Gegenwert. Wenn es so weit ist, wird daraus **ein** `callora-js`-Monorepo mit beiden
Paketen, nicht zwei Repositories.

### 2.4 Beispiel-Plugins liegen außerhalb

Ein Referenzplugin im selben Repository kann das nicht nachweisen, wofür es da ist: Es
kompiliert per `ProjectReference` und umgeht damit jede Paketgrenze. Beispiel-Plugins
bekommen ein eigenes Repository und gehen denselben Weg wie ein fremder Autor.

Was an ihrer Stelle bleibt, ist `scripts/golden-path.sh`: packen → `dotnet tool install`
→ `plugin new` → `publish` → `test-contract` → `sign`, gegen die gebauten Pakete, in der
CI. Das ist der einzige Lauf, der die Paketgrenze überquert.

## 3. Was daraus folgte

Die Grenze war nicht kostenlos. Vier Fehler wurden erst sichtbar, als zum ersten Mal ein
Plugin **außerhalb** des Repositories gegen die Pakete baute — keiner davon hätte je eine
Suite rot gemacht:

1. Ein Direkt-Pin gegen eine HIGH-Advisory schützte nur dieses Repository; .NET 10 prunt
   framework-nahe Pakete aus dem Graphen, der Pin kam nie in die nuspec.
2. `Callora.Plugin.Sdk` gab seine Analyzer nicht weiter — NuGets Vorgabe für
   `ProjectReference` schließt `analyzers` von der Weitergabe aus.
3. Ein Filter gegen mitgelieferte Plattform-Assemblies griff beim Build, aber nicht beim
   Publish; für ein Bibliotheksprojekt sieht ein Build-Test dabei grün aus, ohne etwas
   geprüft zu haben.
4. Derselbe Filter löschte in seiner zweiten Fassung das Plugin selbst —
   Scaffold-Plugins heißen `Callora.Plugins.<Name>`.

Das ist die eigentliche Begründung für §2.4: **Was die Paketgrenze verbirgt, findet nur,
wer sie überquert.**

## 4. Reihenfolge

1. Hygiene — Lizenzangaben, Paket-Metadaten ✔
2. Enabler — Analyzer packbar, CLI als `dotnet tool`, `Callora.Plugin.Sdk` ✔
3. Beweis — Golden Path in der CI ✔
4. Vertragskante festgeschrieben ✔ *(dieses ADR)*
5. Communication, dann Composer in eigene Repositories
6. npm-Push für `@callora/surface` und `@callora/admin`
7. Repository öffentlich, Tag `v0.9.0`, NuGet-Push

## 5. Konsequenzen

**Gut:** Das öffentliche Repository bleibt für Außenstehende baubar. Ein Dritter kann ein
Voice-Plugin gegen den Vertrag bauen, ohne die Implementierung zu sehen — genau die
Fläche, die ein Marketplace braucht. Jedes private Plugin-Repository wird zu genau einem
Store-Artikel: Tag → signiertes Bundle → Upload.

**Schlecht:** Mehr Repositories bedeuten mehr Release-Schritte und mehr
Versionsabstimmung. Getragen wird das von `Callora.Plugin.Sdk` (eine Referenz statt
einer Seite Boilerplate) und vom bereits vorhandenen `PluginDependencyVersionGate`, der
die `dependencies`-Bereiche aus `registry.json` gegen die geladenen Vertragsversionen
prüft — die Kompatibilitätsaussage über Repo-Grenzen ist gebaut, nicht offen.

**Offen:** Ob der Vertrag eines Plugins auf Dauer beim Plugin bleibt. Kommt ein zweites
Plugin hinzu, das denselben Vertrag *definiert* statt nur konsumiert, ist die Zuordnung
neu zu prüfen.
