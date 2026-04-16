# Compliance Baseline: DSGVO + EU AI Act (Platform-wide)

Stand: 2026-04-16  
Scope: Engine + Host + Plugins + Admin UI + Workspace UI

Hinweis: Dieses Dokument ist technische Baseline und keine Rechtsberatung.

## 1. Verbindliche Architekturprinzipien

1. Privacy-by-Design und Privacy-by-Default fuer alle neuen Features.
2. Datenminimierung: nur notwendige Daten erfassen, speichern, weitergeben.
3. Zweckbindung je Datenfluss und je Plugin zwingend dokumentieren.
4. AI-Features ohne deklarierte Human-Oversight sind nicht releasefaehig.

## 2. DSGVO Mindestanforderungen (Engineering)

1. Dateninventar:
   - Kategorien: Identitaetsdaten, Kommunikationsmetadaten, Inhaltsdaten, AI-Ableitungen.
   - Feldweise Klassifikation in API-/Schema-Doku.
2. Rechtsgrundlage je Verarbeitungspfad:
   - im Host pro Feature/Plugin konfigurierbar und auditierbar.
3. Betroffenenrechte:
   - Export API (Art. 15/20) und Loeschpfade (Art. 17) verpflichtend.
4. Aufbewahrung:
   - technische TTL/Retention Policies je Datenklasse.
5. Audit:
   - manipulationsarme Audit-Trails fuer sicherheits-/compliance-relevante Aktionen.
6. Region:
   - EU Datenresidenz fuer Cloud-Betrieb; bei Self-hosted deklarierte Betreiberverantwortung.

## 3. EU AI Act Mindestanforderungen (Engineering)

1. AI Feature Register:
   - jedes AI-Plugin muss Risikoeinstufung + Zweck + Modellquelle deklarieren.
2. Human Oversight:
   - Eingriffs-/Override-Moeglichkeit in kritischen Entscheidungen.
3. Transparenz:
   - KI-Beteiligung in UI/API nachvollziehbar markieren.
4. Traceability:
   - Version von Modell/Prompt/Policy/Plugin revisionssicher protokollieren.
5. Safety Gates:
   - Release ohne dokumentierte Guardrails/Monitoring fuer AI-Features verboten.

## 4. Plugin-spezifische Compliance Gates

1. Plugin Manifest muss enthalten:
   - verarbeitete Datenkategorien
   - Zweck der Verarbeitung
   - AI-Nutzung ja/nein, Risikoeinstufung
   - benoetigte Berechtigungen/Scopes
2. Aktivierung nur bei:
   - gueltigen Entitlements
   - kompatibler Host-Version
   - erfolgreich bestandenen Compliance-Pruefungen
3. Deaktivierung:
   - muss Datenfluss sofort stoppen; laufende Jobs kontrolliert auslaufen.

## 5. Definition of Done (DoD) fuer neue Features

Ein Feature ist nur dann DONE, wenn:

1. DSGVO-Checkliste ausgefuellt ist.
2. Bei AI-Nutzung die EU-AI-Act-Checkliste ausgefuellt ist.
3. Retention + Export + Delete technisch implementiert und getestet sind.
4. Audit Events dokumentiert und getestet sind.
