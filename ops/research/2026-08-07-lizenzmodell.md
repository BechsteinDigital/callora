# Lizenzmodell für Callora — Analyse und Empfehlung

**Datum:** 2026-08-07
**Anlass:** Vor der Doku-Überarbeitung muss feststehen, was die README behauptet.

> **Keine Rechtsberatung.** Was hier steht, ist eine technische Einordnung mit Blick auf das
> Geschäftsmodell. Die Wahl einer Lizenz und besonders die Formulierung einer Ausnahme (§4.3)
> gehört vor einen Anwalt.

---

## 1. Der Ist-Stand ist widersprüchlich

`LICENSE` sagt **„All rights reserved"** — Callora ist heute proprietär. Gleichzeitig
deklarieren zwei npm-Pakete `Apache-2.0`:

- `src/Administration/Resources/app/administration` (`@callora/admin`)
- `src/Surface.Rendering/Resources/app/surface` (`@callora/surface`)

Beide sind die Pakete, gegen die ein Plugin-Autor kompiliert. Die Absicht dahinter ist richtig
und soll bleiben — nur widerspricht sie der Repo-Lizenz. Wer die Pakete heute nutzt, hat eine
Apache-Angabe in der `package.json` und ein „All rights reserved" im selben Repository.

**Das ist unabhängig von jeder weiteren Entscheidung zu klären.** Solange es steht, weiß niemand,
was gilt.

## 2. Warum die Frage bei Callora schärfer ist als anderswo

Plugins laufen **im selben Prozess** (ADR-013, Trusted-in-Process). Sie referenzieren
`Callora.Core`, und der Plugin-`AssemblyLoadContext` teilt bewusst die **Typidentität** mit dem
Host — das ist die Voraussetzung dafür, dass `context.Export<T>` überhaupt funktioniert.

Damit ist ein Plugin technisch so eng mit dem Core verbunden, wie es nur geht. Unter GPL oder
AGPL wäre die Frage „ist das ein abgeleitetes Werk?" für ein Plugin sehr wahrscheinlich mit **ja**
zu beantworten — anders als bei einem Prozess, der über HTTP spricht.

Folge: **Unter einem AGPL-Core könnte niemand ein proprietäres Plugin verkaufen.** Auch wir nicht
— außer kraft Rechteinhaberschaft, was Dritte ausschließt und damit das Ökosystem.

## 3. Was die Vergleichbaren tun

| Projekt | Core-Lizenz | Warum |
|---|---|---|
| **Shopware** | MIT | Maximale Freiheit; Umsatz über Cloud, Enterprise-Erweiterungen, Store |
| **Odoo** | LGPLv3 | Genau damit dürfen die Enterprise-Module proprietär sein |
| **Nextcloud** | AGPLv3 | Apps sprechen über eine definierte App-API; Server-Schutz gegen SaaS-Wiederverkauf |
| **Grafana** | AGPLv3 (seit 2021) | Wechsel von Apache, um Cloud-Wiederverkäufern die Grundlage zu nehmen |

**Odoo ist der nächste Nachbar**: gleiche Bauform (Module im selben Prozess), gleiches
Geschäftsmodell (eigene Module verkaufen, Dritt-Ökosystem zulassen). Odoo hat dafür **LGPL**
gewählt, nicht AGPL — und das ist kein Zufall.

## 4. Die Optionen

### 4.1 Apache-2.0 für den Core

**Dafür:** Größtmögliche Verbreitung, keine Frage nach abgeleiteten Werken, Plugins beliebig
lizenzierbar. Patentklausel inklusive. Shopwares Weg (dort MIT).

**Dagegen:** Nichts fließt zurück, und jeder darf Callora als SaaS anbieten, ohne beizutragen —
für eine Plattform, deren Wert in der Fläche liegt, ist das die realistische Bedrohung.

### 4.2 LGPLv3 für den Core

**Dafür:** Ein Plugin darf proprietär sein — das ist die ausdrückliche Aufgabe der LGPL. Wer den
**Core selbst** ändert, muss die Änderungen offenlegen. Erprobt in genau dieser Konstellation
(Odoo).

**Dagegen:** Schützt nicht vor SaaS-Wiederverkauf. Und die LGPL ist auf dynamisches Linken von
Bibliotheken zugeschnitten; .NET-Assemblies in einem geteilten ALC passen da hinein, aber die
Grauzone ist real.

### 4.3 AGPLv3 plus ausdrückliche Plugin-Ausnahme

**Dafür:** Der schärfste Schutz gegen SaaS-Wiederverkauf, und die Ausnahme öffnet das Ökosystem
gezielt. Nextclouds Modell.

**Dagegen:** Die Ausnahme muss präzise sagen, **was ein Plugin ist** — und bei Callora ist genau
das schwer: Ein Plugin nutzt nicht nur eine schmale App-API, es exportiert Typen in den Host,
dekoriert Dienste und teilt den ALC. Eine unscharfe Ausnahme ist schlimmer als keine, weil sie
Rechtssicherheit vortäuscht.

Immerhin: Das Repo hat mit `[CalloraInternal]`, CAL0001/0002 und den PublicApiAnalyzers bereits
eine **erzwungene** Grenze zwischen Vertragsfläche und Innerem. Das ist genau das Material, auf
das eine Ausnahme sich beziehen könnte — „die als öffentlich deklarierte API" ist hier keine
Absichtserklärung, sondern eine, die der Compiler prüft.

### 4.4 Proprietär bleiben

Der Ist-Stand. Schließt ein Dritt-Ökosystem aus und macht die Apache-Angaben in den npm-Paketen
zu einem Fehler.

## 4.5 Was das künftige Repository-Modell ändert

**Nachtrag 2026-08-07, nach Klärung des Zielmodells.** Communication und Composer werden eigene
Repositories und **private** NuGet-Pakete; ein Installationsskript zieht sie in eine
Distribution. `@callora/surface` und `@callora/admin` werden eigene Repositories und **öffentlich**
auf npm. Was in diesem Repository bleibt, ist Callora selbst.

Damit liegt der gesamte kommerzielle Wert **außerhalb** des offenen Repositories. Eine
Copyleft-Lizenz am Kern schützte dann nichts, was Wert hat — sie kostete nur Verbreitung.

Das entwertet das Argument aus §4.3: AGPL schützt vor SaaS-Wiederverkauf des KERNS. Wenn der
Kern die Einstiegsdroge ist und nicht das Produkt, ist dieser Schutz kein Gewinn, sondern eine
Hürde vor der Verbreitung, von der das Modell lebt.

## 4.6 Warum Shopware MIT wählt

Vier Gründe, von denen drei hier genauso gelten:

1. **Der Umsatz liegt nicht im Core.** Cloud, Enterprise-Erweiterungen, Store-Provision,
   Partnerprogramm. Der Core ist Kundengewinnung.
2. **Das Ökosystem ist der Wert.** Agenturen bauen Kundenprojekte mit proprietärem Code; jede
   Copyleft-Klausel wäre Reibung, die MIT gar nicht erst entstehen lässt.
3. **SaaS-Wiederverkauf ist im E-Commerce kein reales Risiko.** Ein Shop ist Integration,
   Zahlungsanbindung, Betrieb, Support — wer Shopware als SaaS anbietet, ist Partner. Genau
   deshalb greifen Grafana und MongoDB zu AGPL und Shopware nicht: Dort IST der Kern das Produkt.
4. **MIT geht durch jede Rechtsabteilung.** AGPL löst bei vielen Konzernen ein pauschales Verbot
   aus — und ein Verbot ist teurer als jede entgangene Copyleft-Wirkung.

Punkt 3 ist der, an dem sich alles entscheidet, und er trifft auf ein Contact Center genauso zu
wie auf einen Shop: Telefonie-Anbindung, Betrieb, Compliance und Support sind das Geschäft, nicht
der Quelltext.

## 5. Empfehlung

**Dreistufig, entlang der Grenzen, die im Code ohnehin schon gezogen sind:**

| Schicht | Lizenz | Begründung |
|---|---|---|
| **Vertragspakete** (`@callora/surface`, `@callora/admin`, künftige `*.Abstractions`) | **Apache-2.0** | Wogegen kompiliert wird, muss reibungsfrei nutzbar sein — auch von proprietären Plugins. Bleibt wie heute deklariert. |
| **Core** (`Callora.Core`, Host, Module) | **Apache-2.0** | Siehe unten |
| **Eigene kommerzielle Plugins** | proprietär | Der Umsatzträger. Unberührt von beidem. |

**Apache-2.0 statt Copyleft**, weil die Wertträger das Repository ohnehin verlassen (§4.5). Was
bleibt, ist Infrastruktur, und Infrastruktur lebt von Verbreitung.

**Apache-2.0 statt MIT** aus einem Grund: der ausdrücklichen **Patentklausel**. Bei einer
Voice-Plattform — Codecs, SIP, Echo Cancellation, Sprachmodelle — ist Patentrecht ein reales
Thema, und MIT adressiert es schlicht nicht. Apache-2.0 gewährt Nutzern die Patentlizenz
ausdrücklich und entzieht sie dem, der wegen Patentverletzung klagt. Für einen Shop ist das
Beiwerk; für Telefonie ist es keins.

Es ist außerdem das, was `@callora/surface`, `@callora/admin` und die VoIP-SDK bereits
deklarieren — die Entscheidung räumt damit denselben Widerspruch aus, den §1 benennt, statt einen
neuen zu schaffen.

**Ein Copyleft-Wechsel bliebe später möglich**, solange die Rechte gebündelt sind (§6.4). Grafana
hat genau das getan, in dieser Richtung. Umgekehrt — von AGPL zu Apache — ginge es nicht, sobald
Dritte beigetragen haben.

## 6. Was unabhängig davon zu tun ist

1. **Den Widerspruch auflösen.** Solange `LICENSE` „All rights reserved" sagt, ist jede
   Apache-Angabe in einer `package.json` irreführend.
2. **Je Paket eine `LICENSE`-Datei.** Eine Repo-weite Angabe trägt nicht, wenn drei Schichten
   verschieden lizenziert sind.
3. **Einen `NOTICE`- oder Lizenz-Abschnitt in der Doku**, der die drei Schichten benennt. Ein
   Plugin-Autor muss ohne Nachfrage wissen, was für ihn gilt.
4. **Contributor-Frage klären.** Wer beiträgt, muss die Rechte so einräumen, dass ein späterer
   Lizenzwechsel oder eine kommerzielle Ausnahme möglich bleibt (CLA oder DCO plus
   Lizenzhinweis). Ohne das ist §5 in einem Jahr nicht mehr umsetzbar.
