# Produkt-Telemetrie über Struktur-Kennzahlen — Design

**Datum:** 2026-08-06
**Status:** Konzept, **nachrangig** — bewusst nicht eingeplant. Das Dokument hält eine Idee fest,
die später hilft; es begründet keinen Bauauftrag. Vor rund fünfzig Installationen im Bestand trägt
die Übertragung ohnehin nicht (§9.1), und bis dahin ist das Kundengespräch das bessere Verfahren.
**Kontext:** Für Produktentscheidungen und Case Studies soll erkennbar werden, **was Kunden
prinzipiell bauen** — wie viele Surfaces eine typische Installation hat, wie groß Layouts sind,
welche Blöcke gemeinsam auftreten, wo Nutzer scheitern. Ausdrücklich nicht, mit welchen Daten sie
arbeiten, und ausdrücklich nicht, **wer** was baut.

Ergänzt [2026-08-06 — Admin-SDK, SDK-Familie und Surface Composer](./2026-08-06-admin-sdk-und-surface-composer-design.md);
die Kennzahlen werden aus dem dortigen Layout-Dokument (§7.1/§7.2) abgeleitet.

---

## 1. Nicht-Ziele

Zuerst, weil sie den Entwurf stärker formen als die Ziele:

- **Keine Identifizierung.** Weder direkt noch über Umwege. Es soll nicht möglich sein zu sagen,
  welche Installation etwas gebaut hat — auch nicht durch Kombination von Merkmalen, auch nicht
  durch Verfolgung über die Zeit.
- **Keine Inhalte.** Texte, Bilder, Rufnummern, jeder `static`-Wert einer Block-Konfiguration —
  verlässt die Installation nie.
- **Keine Strukturen.** Kein Layout-Baum, kein Dokument, keine Anordnung. Ein hinreichend
  detaillierter Strukturbaum ist selbst ohne Namen ein Fingerabdruck. Übertragen werden
  **Kennzahlen und Häufigkeiten** (§3).
- **Keine Klickpfade, keine Verweildauer, kein Session-Recording.**
- **Keine personenbezogenen Nutzungsdaten.** „Operator X hat 47 Layouts bearbeitet" wird nie
  erhoben; „in diesem Zeitfenster wurden 47 Layouts bearbeitet" schon.
- **Kein stiller Betrieb.** Keine Erhebung ohne ausdrückliche, sichtbare Zustimmung (§5).
- **Kein Ersatz für eine Case-Study-Freigabe.** Ein namentliches Kundenporträt entsteht durch
  Nachfragen, nicht durch Telemetrie. Die Telemetrie liefert das aggregierte Bild — „so bauen
  Kunden typischerweise" —, nie das Einzelporträt.

---

## 2. Drei Datenklassen

| Klasse | Was | Entsteht wie | Behandlung |
|---|---|---|---|
| **Struktur** | Was Kunden bauen | im Layout-Dokument vorhanden, wird zu Kennzahlen verdichtet | §3 |
| **Nutzung** | Was wie oft geschieht | über den vorhandenen Business-Event-Bus | §4 |
| **Inhalt** | Womit gearbeitet wird | — | Nicht-Ziel |

---

## 3. Struktur-Kennzahlen

### 3.1 Verdichtung statt Projektion

Ein erster Entwurf sah einen „Struktur-Abdruck" vor: das Layout-Dokument minus aller statischen
Werte. Das ist zu reich. Ein Layout mit siebzehn Sektionen und vier seltenen Blöcken ist auch ohne
jeden Namen unverwechselbar, und über mehrere Übertragungen hinweg entsteht daraus faktisch eine
Identität.

Die Installation überträgt deshalb **keine Struktur, sondern Statistik**. Die Verdichtung passiert
am Ursprung; der Baum wird ausgewertet und verworfen, nicht gesendet.

```jsonc
{
  "schemaVersion": 1,
  "window": "2026-08",              // Monat, keine feinere Auflösung
  "cohort": "e3a1…",                // rotierend, siehe §6.1

  "topology": {
    "surfaceCount": 3,
    "accessModes": { "authenticated": 2, "public": 1 },
    "usesSharedContext": true,
    "anchorTypes": ["subject", "conversation"],
    "themeOverrideCount": 7
  },

  "layouts": {
    "count": 3,
    "sectionsPerLayout":  { "p50": 3, "p90": 6, "max": 8 },
    "blocksPerLayout":    { "p50": 7, "p90": 14, "max": 19 },
    "sectionLayouts":     { "single": 4, "two-2-1": 3, "sidebar-left": 1 },
    "publishedVersions":  { "p50": 4, "max": 12 }
  },

  "blocks": {
    "byCategory":  { "telephony": 7, "content": 4, "custom": 2 },
    "known":       { "communication.call-list": 3, "communication.dialer": 2 },
    "customShare": 0.18,
    "coOccurrence": [
      ["communication.call-list", "communication.dialer", 2]
    ],
    "bindingKinds": { "static": 24, "context": 6, "default": 31 }
  },

  "context": {
    "requiredKeys": { "communication.active-call/v1": 3 },
    "unresolvedRequirements": 1
  }
}
```

### 3.2 Bezeichner

Die Regel ist jetzt einfach, weil Identifizierung kein Ziel ist:

- **Bezeichner aus zentral kuratierter Quelle** — Blöcke, Kategorien, Kontext-Keys und Themes aus
  dem Marketplace bzw. von Callora signiert — sind öffentliche Vokabeln und stehen im Klartext.
- **Alles andere wird nicht übertragen, auch nicht gehasht**, sondern zählt in eine Sammelgröße:
  `custom`. Kein Hash, kein Salt, keine Rotationsfrage, keine Grauzone bei selbst signierten
  Kundenplugins.

Was dadurch nicht mehr sagbar ist: „dieser unbekannte Block läuft in 40 Installationen". Was
weiterhin sagbar ist: „18 % der eingesetzten Blöcke sind Eigenentwicklungen" — und das ist die
Frage, die tatsächlich gestellt wurde.

Frei benannte Layout-, Workspace- und Surface-Keys erscheinen nirgends; sie werden gezählt, nicht
benannt.

### 3.3 Welche Fragen beantwortet werden

- Wie viele Surfaces hat eine typische Installation, welche Access-Modes kombiniert sie?
- Bauen Kunden CRM, Dialer und Videokonferenz in **ein** Surface oder in mehrere, über Anker
  verbundene? (`usesSharedContext`, `anchorTypes`, `surfaceCount`)
- Wie groß ist ein typisches Layout? Perzentile statt Einzelwerte — der Ausreißer bleibt
  unsichtbar, der Median ist die interessante Zahl.
- Welche Blöcke treten gemeinsam auf? (`coOccurrence`, am Ursprung zu Paaren verdichtet, nur unter
  bekannten Bezeichnern)
- Wie stark greifen Kunden zu Eigenentwicklungen, bevor sie ein Theme bauen?
  (`customShare`, `themeOverrideCount`)
- Wird der geteilte Kontext in der Praxis gebraucht, oder ist die Mehr-Surface-Topologie Theorie?

Perzentile statt Listen sind dabei kein Detail, sondern der Kern: eine Verteilung über alle Layouts
einer Installation beschreibt, **was gebaut wird**, ohne ein einzelnes Layout beschreibbar zu
machen.

---

## 4. Nutzungsereignisse

### 4.1 Mechanismus

Kein neues System. Der Business-Event-Bus hat bereits stabile, dotted Namen (`workspace.created`,
`media.uploaded`, `surface.caller-promoted`); ein Produkt-Analytik-Konsument ist genau ein
`IBusinessEventListener`. Es braucht zusätzliche Ereignisnamen und einen Aggregator.

```
layout.created            layout.published          layout.rolled-back
layout.block-added        layout.block-removed      layout.section-added
composer.opened           composer.abandoned
```

Übertragen werden daraus ausschließlich **Zähler je Zeitfenster**, nie Einzelereignisse.

### 4.2 Was daraus abgeleitet wird

- **Time-to-Value** — Zeitspanne von `workspace.created` bis zum ersten `layout.published`, als
  Dauer in Tagen, nicht als Datumspaar. Die wichtigste einzelne Zahl für ein Produkt, das Laien
  befähigen will.
- **Abbrüche** — ein Block, der im selben Entwurf hinzugefügt und wieder entfernt wird, ist ein
  direktes Signal für einen Block, der nicht hält, was sein Name verspricht. Aussagekräftiger als
  jede Verwendungsstatistik.
- **Rückrollrate** — häufiges Zurückrollen deutet auf fehlende Vorschau-Treue oder unklare
  Guardrails.

### 4.3 Fehlersignale — die wertvollste Klasse

Verwaiste Blöcke, Kontext-Keys ohne Publisher, Bundle-Ladefehler. Teile existieren bereits
(`__calloraSurfaceLoad`, `getPluginUiLoadResults`, die Fälle aus Composer-Design §7.8).

Diese Klasse ist am leichtesten zu rechtfertigen, weil sie dem Kunden unmittelbar nützt — und
zugleich die verwertbarste: ein Block, der in dreißig Installationen wegen eines fehlenden
Kontext-Publishers leer bleibt, ist ein Produktfehler, den sonst niemand meldet.

---

## 5. Bauform: das Plugin ist der Schalter

Callora ist Open-Core und self-hosted betreibbar. Telemetrie, die nach Hause telefoniert, ist in
dieser Welt ein Vertrauensbruch — Homebrew, VS Code und die .NET CLI haben das jeweils teuer
gelernt.

**Die Erhebung gehört deshalb nicht in den Core, sondern in ein optionales Plugin.** Wer es nicht
installiert, sendet nichts — nicht weil ein Flag auf `false` steht, sondern weil der Codepfad nicht
existiert. Das ist die einzige Bauform, die in einem AGPL-Core glaubwürdig vertretbar ist, und sie
fällt mit dem vorhandenen Plugin-Modell ohne Sonderlogik an.

Der Core steuert nur bei, was ohnehin allgemein nützlich ist: die Ereignisnamen auf dem
Business-Event-Bus und die Verdichtung, die aus Layouts Kennzahlen macht. Beides ist ohne
Übertragung sinnvoll (§6).

---

## 6. Kundenwert zuerst, Übertragung zweitens

Zwei getrennte Schalter, in dieser Reihenfolge:

**Schalter 1 — lokale Analyse.** Die Kennzahlen werden berechnet und dem Kunden **in seiner eigenen
Administration** gezeigt: „Drei deiner zwölf Blöcke rendern nie, weil ihr Kontext-Key auf dieser
Surface nirgends publiziert wird." „Dieses Layout hat seit acht Wochen einen Entwurf, der nie
veröffentlicht wurde." Ein Feature, kein Abfluss — es funktioniert vollständig, ohne dass etwas die
Installation verlässt.

**Schalter 2 — Weitergabe.** Separat, ausdrücklich, jederzeit widerrufbar, mit einer Ansicht, die
den **tatsächlichen Payload** zeigt — nicht dessen Beschreibung, sondern das Dokument, das gesendet
würde.

Umgekehrt gebaut — erst Übertragung, dann vielleicht ein Nutzen für den Kunden — wäre es
Überwachung mit Extraschritten.

### 6.1 Kohorten-Kennung statt Instanz-Kennung

Eine dauerhafte Installationskennung wäre eine Identität, auch wenn sie zufällig ist: über zwölf
Übertragungen hinweg entsteht ein verfolgbares Profil. Sie entfällt.

Stattdessen trägt jede Übertragung eine **rotierende, zufällig erzeugte Kohorten-Kennung**, die in
festem Rhythmus (Vorschlag: quartalsweise) neu gebildet wird und aus nichts abgeleitet ist — nicht
aus Domain, Lizenz, Konto oder Hardware. Sie erlaubt, die Angaben einer Übertragung als
zusammengehörig zu erkennen und Doppelzählung innerhalb des Zeitraums zu vermeiden. Sie erlaubt
**nicht**, eine Installation über Quartale hinweg zu verfolgen.

Was dadurch verloren geht: Aussagen über die Entwicklung einer einzelnen Installation („wächst
typischerweise von einem auf drei Surfaces"). Was bleibt: die Entwicklung des Bestands („der Anteil
von Mehr-Surface-Installationen ist von 20 auf 35 % gestiegen"). Für die gestellte Frage — was bauen
Kunden prinzipiell — genügt Letzteres.

Einen „zugeordneten" Modus gibt es nicht. Support-Fälle laufen über den Support-Kanal, in dem der
Kunde sich ohnehin zu erkennen gibt; sie brauchen keine Telemetrie-Zuordnung.

---

## 7. Aggregation am Ursprung

**Was die Installation verlässt, ist bereits verdichtet.** Keine Einzelereignisse, keine
Subject-IDs, keine Layout-Bäume, keine Zeitauflösung feiner als der Monat. Ein
`IRecurringJobProvider` fasst Ereignisse und Layouts eines Fensters zu Kennzahlen zusammen und
verwirft die Rohdaten.

Der Unterschied zwischen identifizierbar und nicht liegt **in der Reihenfolge**: erst verdichten,
dann senden. Nachträgliches Anonymisieren beim Empfänger zählt nicht — die Daten waren dann bereits
identifizierend, als sie übertragen wurden.

---

## 8. Rechtsrahmen

Ziel des Entwurfs ist, dass die übertragenen Daten **keinen Personenbezug** haben: keine
Identifizierung der Installation, keine Struktur, keine Inhalte, keine Personenkennungen, Auflösung
auf Monat und Perzentil. Damit liegt der Schwerpunkt nicht auf einer Rechtsgrundlage für
personenbezogene Verarbeitung, sondern auf **Transparenz und Zustimmung** — auch eine anonyme
Übertragung ohne Wissen des Betreibers wäre ein Vertrauensbruch, unabhängig von der Rechtslage.

| Betriebsform | Grundlage |
|---|---|
| gehostet | gesonderter, sichtbarer Hinweis zur Produktanalytik; Abschaltung jederzeit möglich |
| self-hosted | ausdrückliche Zustimmung bei Installation des Telemetrie-Plugins |
| Case Study mit Namen | eigenständige Freigabe des Kunden — davon unabhängig |

Die Einordnung als nicht personenbezogen trägt **nur, solange** Verdichtung (§7),
Bezeichner-Regel (§3.2) und Kohorten-Rotation (§6.1) greifen. Fällt eines davon weg, ändert sie
sich. Eine juristische Prüfung vor Inbetriebnahme bleibt angezeigt.

### 8.1 Mindestkohorte bei der Auswertung

Auch nicht personenbezogene Daten können durch Kombination aussagekräftig über wenige Betreiber
werden. Wenn nur drei Installationen ein bestimmtes Plugin einsetzen, ist „Installationen mit
Plugin X haben im Schnitt Y" faktisch eine Aussage über diese drei.

**Die Mindestkohortengröße ist k = 5.** Der Wert ist an der Zellensuppression der amtlichen
Statistik orientiert und damit begründbar statt willkürlich. Unterschreitet ein Segment ihn, wird
es nicht ausgewiesen — weder intern noch in einer Case Study.

Die Schwelle gilt **nur für segmentierte Aussagen**, nicht für die Telemetrie insgesamt:

| Aussagetyp | Beispiel | Braucht k |
|---|---|---|
| Bestandsweit | „Median Layoutgröße über alle Installationen" | nein |
| Segmentiert | „Median bei Installationen mit Videokonferenz-Plugin" | ja |

Bestandsweite Kennzahlen tragen bereits ab etwa zwanzig zustimmenden Installationen. Gerade die
interessanten Fragen sind aber segmentiert — und die brauchen entsprechend mehr (§9.1).

---

## 9. Umsetzungsschnitt

| # | Baustein | Liefert |
|---|---|---|
| 1 | Verdichtung im Core: Layouts → Kennzahlen, inklusive Bezeichner-Regel | Grundlage, ohne Übertragung nutzbar |
| 2 | Ereignisnamen auf dem Business-Event-Bus (`layout.*`, `composer.*`) | Grundlage, ohne Übertragung nutzbar |
| 3 | Lokale Analyse-Ansicht in der Administration (Schalter 1) | **Kundenwert, kein Abfluss** |
| 4 | Aggregator als `IRecurringJobProvider`, Kohorten-Rotation | verdichtete Kennzahlen |
| 5 | Telemetrie-Plugin: Payload-Vorschau, Zustimmung, Übertragung (Schalter 2) | Material für Case Studies |

Die Bausteine 1–3 sind eigenständig sinnvoll und enthalten keine Übertragung. Erst Baustein 5
verlässt die Installation, und er ist ein separat installierbares Plugin.

### 9.1 Wann Telemetrie trägt — und was vorher gilt

**Telemetrie ist ein Skalierungsinstrument, kein Startinstrument.** Bei fünf bis zwanzig Kunden ist
ein Gespräch jeder Statistik überlegen: es liefert das *Warum*, nicht nur das *Was*. Und es braucht
Baustein 5 nicht.

**Verfahren der frühen Phase:** Die lokale Analyse (Baustein 3) zeigt dem Kunden seine eigenen
Kennzahlen in seiner Administration. Im Kundengespräch wird danach gefragt. Das liefert Struktur
**und** Begründung, ohne dass eine Zeile übertragen wird — in dieser Phase nicht der Notbehelf,
sondern das bessere Verfahren.

**Erhebung beginnt nicht vor Auswertbarkeit.** Auf Vorrat zu sammeln wäre zweckbindungsrechtlich
schwach: erhoben würde für eine Auswertung, die nicht durchführbar ist. Das kostet die frühen
Monate und ist dafür sauber begründet.

**Schwelle für Baustein 4 und 5:** bei k = 5 (§8.1) und einer realistischen Zustimmungsrate von
dreißig bis fünfzig Prozent sind rund **fünfzig Installationen im Bestand** nötig, bevor sich die
Übertragung lohnt. Vorher ist die Kohorte zu klein für segmentierte Aussagen, und genau die sind
der Grund, Telemetrie überhaupt zu bauen.

Bausteine 1–3 sind von dieser Schwelle **unberührt** — sie sagen über eine einzelne Installation
aus und tragen ab der ersten.

---

## 10. Tests und Governance

1. **Keine Struktur im Payload** — die Verdichtung gibt ausschließlich Zahlen, Verteilungen und
   Häufigkeiten aus; ein verschachtelter Layout-Baum im Payload ist ein Testfehler.
2. **Kein statischer Wert** — aus einer Block-Konfiguration werden nur Bindungs*arten* gezählt, nie
   Bindungs*werte* übertragen.
3. **Bezeichner-Regel** — ein Bezeichner ohne kuratierte Herkunft erscheint weder im Klartext noch
   gehasht, sondern ausschließlich in der Sammelgröße `custom`.
4. **Keine frei benannten Schlüssel** — Layout-, Workspace- und Surface-Keys erscheinen in keiner
   Form im Payload.
5. **Keine Personenkennung** — kein Nutzungsereignis trägt eine Subject-ID über die Aggregation
   hinaus.
6. **Zeitauflösung** — kein übertragener Zeitbezug ist feiner als der Monat; Zeitspannen werden als
   Dauer ausgegeben, nie als Datumspaar.
7. **Kohorten-Rotation** — die Kennung ändert sich zum Rotationstermin und ist aus keinem
   installationsspezifischen Merkmal abgeleitet.
8. **Ohne Plugin keine Ausgänge** — ohne installiertes Telemetrie-Plugin existiert kein Codepfad,
   der Daten nach außen sendet.
9. **Payload-Vorschau ist der Payload** — was die Vorschau zeigt, ist byte-gleich mit dem, was
   gesendet wird.

Regel 9 ist die wichtigste: sie macht Transparenz prüfbar statt behauptet. Regel 1 schützt den
Entwurf gegen schleichende Ausweitung — Strukturen sind die naheliegendste und gefährlichste
Erweiterung.

---

## 11. Offene Punkte

- **Rotationsperiode der Kohorten-Kennung** — quartalsweise ist ein Vorschlag, kein Ergebnis.
  Kürzer heißt weniger Verfolgbarkeit und mehr Doppelzählung.
- **Übertragungsintervall und -format** (Push vs. Abholung, Endpunkt, Authentifizierung) — offen.
  Die Authentifizierung darf die Anonymität nicht unterlaufen: ein Übertragungs-Token, das einem
  Konto zugeordnet ist, hebt §6.1 auf.
- **Aufbewahrungsdauer beim Empfänger.**
- **Ob die lokale Analyse (Baustein 3) eigenständigen Produktwert hat**, der eine eigene
  Priorisierung rechtfertigt. Nach §9.1 ist sie zusätzlich das Erkenntnisverfahren der frühen
  Phase, was für eine frühe Priorisierung spricht — die Produktwert-Frage selbst bleibt zu
  validieren.

Entschieden und daher **nicht mehr offen**: Mindestkohortengröße (k = 5, §8.1), Beginn der
Erhebung (nicht vor Auswertbarkeit, §9.1), Verfahren der frühen Phase (lokale Analyse plus
Kundengespräch, §9.1).

---

## 12. Bezug

- **Composer-Design §7.1/§7.2** — das Layout-Dokument, aus dem verdichtet wird.
- **Composer-Design §7.8** — die Fehlerfälle, die zu den Fehlersignalen aus §4.3 werden.
- **Composer-Design §5.5** — dieselbe Haltung („der Server projiziert, der Client filtert nie"),
  hier als „die Installation verdichtet, der Empfänger aggregiert nicht nachträglich".
