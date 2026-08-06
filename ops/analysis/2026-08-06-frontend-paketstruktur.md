# Frontend-Paketstruktur — wie andere es machen und was für uns gilt

**Datum:** 2026-08-06
**Status:** entschieden
**Frage:** Wo leben die Frontend-Pakete, gegen die Plugin-Autoren bauen — und was genau ist bei
uns ein „SDK"?

---

## 1. Entscheidung

**Zwei Pakete, je im Modul, mit Unterpfad-Exporten. Apache-2.0.**

```
src/Administration/Resources/app/administration/   → @callora/admin
  ./extensions   registrieren, eingreifen, Dienste ersetzen
  ./components   die Cal*-Primitive
  ./tokens       Design-Tokens
  ./patterns     CalListPage & Co.

src/Surface.Rendering/Resources/app/surface/       → @callora/surface
  ./views        registerSurfaceView, Islands
  ./context      Kontext-Kanal
  ./components   Surface-Primitive
```

Kein geteiltes `ui-core`, kein `sdk/`-Verzeichnis, kein eigenes Repo — jedenfalls jetzt nicht
(§5).

---

## 2. Was die Vorbilder tun

| | Struktur | Ablage | Lizenz |
|---|---|---|---|
| **Umbraco** (v14+) | **ein** Paket `@umbraco-cms/backoffice` mit ~80 Unterpfad-Exporten | **im Modul**: `src/Umbraco.Web.UI.Client/` | MIT |
| **ABP** | 16 Pakete, fachlich geschnitten (`core`, `components`, `theme-shared`, `identity`, `tenant-management`, …) | eigenes Verzeichnis `npm/ng-packs/packages/` | LGPL |
| **Shopware** | drei **Repos** — `platform` (Kern), `meteor` (Admin), `frontends` (Storefront), je mit `packages/` | getrennt | MIT |
| **Orchard Core, nopCommerce** | keins — Razor/Liquid, kein npm-Vertrag | — | — |

**Umbraco ist die nächste Analogie**: .NET-Backend, modernes JS-Frontend, Extension-Ökosystem,
und die Lösung liegt im Modulverzeichnis. Zwei Vite-Konfigurationen nebeneinander — eine baut
die App, eine die Bibliothek.

**Was Shopware verrät:** Meteor und Frontends teilen sich **nichts**, nicht einmal Design-Tokens
(`meteor/tokens` gegen `frontends/unocss-design-tokens-layer`). Zwei Frontends, zwei Token-Sätze,
zwei Repos. Das ist kein Versäumnis, sondern Absicht: Administration ist eine Vue-SPA, die
Storefront Twig mit Bootstrap — geteilte Komponenten wären ohnehin unmöglich.

**Was „SDK" bei Shopware heißt:** `meteor-admin-sdk` ist die **Fähigkeits-API**
(`sw.notification.dispatch(…)`, Kontext lesen, UI erweitern) mit ausdrücklichem BC-Versprechen —
nicht die Bausteine. Komponenten, Tokens und Icons sind daneben eigene Bibliotheken und
ausdrücklich kein SDK. Diese Trennung übernehmen wir begrifflich: ein SDK beschreibt, was ein
Plugin *tun* kann, nicht welche Knöpfe es hat.

---

## 3. Warum kein geteiltes `ui-core`

Der ursprüngliche Entwurf sah einen flächenneutralen Kern vor, begründet mit dem
Composer-Canvas: ein Block sollte im Editor und live durch denselben Code rendern, damit keine
Vorschau-Drift entsteht.

**Das Argument entfällt, weil der Composer nur Surfaces bearbeitet.** Seine Blöcke laufen
deshalb immer in der Surface-Runtime — im Editor wie in Produktion. Portabilität *zwischen* den
Flächen wird nie gebraucht.

Was dann noch für einen Kern spräche, ist schwächer: ein Plugin wie Communication bedient beide
Flächen und nutzt zwei Komponentensätze. Real, aber kein Grund für ein drittes Paket, bevor es
wehtut — und abspaltbar, sobald es das tut.

**Nebeneffekt:** Damit löst sich auch D1 der [Bestandsaufnahme](./2026-08-06-frontend-sdk-bestandsaufnahme.md)
auf. `custom/surface-sdk` existiert nur, weil die Runtime privat ist; wird die Runtime selbst
zum Paket, gibt es die doppelten Typdeklarationen nicht mehr — nicht durch einen Test abgesichert,
sondern durch Wegfall.

---

## 4. Lizenz

**Apache-2.0 für beide Pakete**, mit Lizenztext im Paket und im `files`-Feld.

Die tragende Überlegung: **ein copyleft-lizenziertes Frontend-Paket steckt jedes Plugin an, das
es einbindet.** Ein AGPL-`@callora/admin` würde jedes Admin-Plugin zu AGPL zwingen und ein
kommerzielles Ökosystem ausschließen — also genau das, was das Geschäftsmodell tragen soll.
Deshalb ist die Aufteilung in der Open-Core-Welt praktisch immer dieselbe: Server copyleft,
Client-Bibliotheken permissiv. Shopware und Umbraco liefern beide MIT.

Apache-2.0 statt MIT wegen der ausdrücklichen Patentklausel — bei Telefonie mit Codecs und
Protokollen relevanter als bei einem Shop — und weil das VoIP-SDK bereits Apache-2.0 ist.

Die Kern-Lizenz (AGPL, proprietär oder anderes) ist davon **unabhängig** und eine
Geschäftsentscheidung. Offen zu klären ist, dass die Root-`LICENSE` heute „All rights reserved"
sagt, während die Pakete Apache-2.0 behaupten, ohne Lizenztext mitzuliefern.

---

## 5. Wann ein eigenes Repo

Nicht jetzt: Jede Vertragsänderung berührt derzeit gleichzeitig einen Konsumenten, und
Cross-Repo hieße zwei PRs mit einem Zwischenzustand, in dem eine Seite gegen einen
unveröffentlichten Vertrag baut.

Später ja, mit dieser Schwelle: **Vertrags-Freeze auf 1.0 plus der erste externe Konsument.**
Dann kehrt sich die Rechnung um — stabile Verträge, eigener Veröffentlichungsrhythmus, und ein
Beitragender klont nicht die ganze Plattform. Liegen die Pakete bis dahin in ihren Modulen, ist
der Split ein `git subtree split` je Paket.

---

## 6. Wo wir besser sein können als alle drei

**Der Compiler kennt die Extension-Points.** Umbraco registriert über JSON-Manifeste mit
String-Typen (`type: 'dashboard'`), Shopware über Strings, ABP über Angular-Provider — ein
Tippfehler ist überall ein stiller No-Op. Wir generieren den Katalog aus der Shell und erzeugen
daraus Literal-Unions: ein falscher Slot-Name bricht die Kompilierung. Keiner der drei hat das.

**Server- und Client-Deklaration sind aneinander gebunden.** Bei allen dreien deklariert man im
Frontend, und der Server weiß nichts davon. Unser Konsistenztest bindet
`HostSurfaceViewRegistration.ViewId` an `registerSurfaceView` — ein Typo bricht den Build statt
der Produktion. Auch das hat keiner.

**Ein Plugin, beide Flächen, ein Vorgang.** Bei Shopware braucht ein Plugin mit Admin- und
Storefront-Teil zwei SDKs aus zwei Repos mit getrennten Versionen. Bei uns liegen beide Pakete
im selben Repo, gehen im selben Release, und ein Scaffold kann beides zugleich anlegen.

**Die Vorschau ist die Sache selbst.** Shopwares CMS-Editor rendert mit Admin-Komponenten, das
Frontend mit Storefront-Komponenten — dieselbe Seite, zwei Implementierungen, dauerhafte Drift.
Unser Canvas lädt die echte Surface-Runtime. Das ist der Punkt, an dem wir konkret besser sind,
und die Entscheidung gegen `ui-core` macht ihn *einfacher* zu erreichen, nicht schwerer.

**Live-Daten im Editor.** Keiner der drei kennt ein Konzept für Blöcke, die auf Ereignisse
reagieren; Shopwares `mapped` löst zur Request-Zeit auf und friert dann ein. Unser
`source: 'context'` bindet an einen versionierten Kontext-Key, und der Editor kann ihn mit
Beispielwerten belegen, um einen dynamischen Block ohne echten Anruf zu zeigen.

---

## 7. Folgen für Spec und Plan

- [Composer-Design](../specs/2026-08-06-admin-sdk-und-surface-composer-design.md) §6.1: der
  Drei-Paket-Schnitt entfällt, Bausteine 1–3 werden neu geschnitten.
- [Umsetzungsplan](../plans/2026-08-06-sdk-familie-bausteine-1-3.md): Tasks 1–8 (Kern) entfallen
  bzw. gehen in die Paketierung der Shell auf; Task 17/18 (Surface-SDK auf den Kern) wird zur
  Auflösung von `custom/surface-sdk` in die Runtime.
- `custom/ui-core` ist mit `77cceb6` bereits zurückgebaut; `custom/surface-sdk` folgt.
