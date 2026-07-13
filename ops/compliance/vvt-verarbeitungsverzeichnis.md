# Verzeichnis von Verarbeitungstätigkeiten (Art. 30 DSGVO) — Zuarbeit

Technische Zuarbeit für das VVT des Betreibers (PLAT-246). Kein
Rechtsdokument — Verantwortlicher, Rechtsgrundlagen und Fristen muss der
Betreiber je Einsatz festlegen.

## V-01: Telefonie (Calls über Voice-Plugins)

- **Zweck:** Auf- und Abbau von Sprachverbindungen, Live-Anzeige, Routing.
- **Datenkategorien:** Rufnummern (Ziel/Anrufer), Anzeigenamen, Zeitstempel,
  Call-Zustände, Workspace-Zuordnung.
- **Betroffene:** Anrufer, Angerufene, Agenten.
- **Speicherorte:** Live nur im Speicher (ActiveCallRegistry); Events in
  `background_jobs`-Payloads (Retention 14 Tage nach Abschluss).
- **Empfänger:** Webhook-Ziele erhalten per Default **maskierte**
  personenbezogene Felder; unmaskiert nur bei explizitem Opt-in der
  Subscription (`includeSensitiveData`).
- **Löschung:** Retention-Job (`host.retention.cleanup`); kaskadierende
  Löschung bei Workspace-Löschung.

## V-02: Benutzer- und Zugriffsverwaltung

- **Zweck:** Authentifizierung, RBAC, Workspace-Mitgliedschaften.
- **Datenkategorien:** Login/ExternalId, E-Mail, Anzeigename,
  Passwort-Hash (ASP.NET Identity v3), Rollen­zuordnung.
- **Betroffenenrechte:** Export über `GET /api/users/{id}/data-export`
  (Art. 15); Löschung inkl. Audit-Anonymisierung über
  `DELETE /api/users/{id}` (Art. 17).

## V-03: Audit-Protokollierung

- **Zweck:** Nachvollziehbarkeit sicherheitsrelevanter Aktionen
  (Plugin-Lifecycle, Konfiguration).
- **Datenkategorien:** Aktion, Zeitstempel, auslösender Nutzer (`RequestedBy`).
- **Besonderheit:** Append-only; bei Nutzerlöschung wird `RequestedBy`
  anonymisiert (`erased-user`), die Ereignisse bleiben erhalten.

## V-04: Benachrichtigungen & Mail

- **Zweck:** Systembenachrichtigungen, Mail-Versand über Jobs.
- **Datenkategorien:** Empfängeradressen, Betreff/Inhalt (in Job-Payloads).
- **Löschung:** Notifications 90 Tage; Mail-Job-Payloads 14 Tage nach
  Abschluss. Logs enthalten Empfänger nur maskiert.

## V-05: Webhooks (Auftragsweitergabe an Dritte)

- **Zweck:** Event-Zustellung an vom Betreiber konfigurierte Ziele.
- **Hinweis für den Betreiber:** Ziele mit `includeSensitiveData=true`
  erhalten Rufnummern im Klartext — dafür ist je Ziel eine
  Auftragsverarbeitungs- oder Übermittlungsgrundlage erforderlich.
- **Schutz:** HMAC-Signatur, SSRF-Egress-Guard, Secrets verschlüsselt at rest.

---

# DPIA-Gerüst: Gesprächsaufzeichnung (Art. 35) — VOR Feature-Bau

Recording/Voicemail ist noch nicht implementiert; `IRecordingConsentCall`
(PLAT-241) ist die verpflichtende technische Vorbedingung.

1. **Beschreibung:** Aufzeichnung von Gesprächsinhalten (Audio) —
   hohes Risiko: Inhaltsdaten der Telekommunikation, § 201 StGB.
2. **Notwendigkeit:** Zweck je Einsatz dokumentieren (z. B. Beweissicherung,
   Qualität); mildere Mittel prüfen (Transkript-Ausschnitte, Notizen).
3. **Technische Garantien (bereits im Vertrag verankert):**
   - Aufzeichnung nur bei `RecordingConsentState.Granted`; Timeout und
     Auflegen gelten als Ablehnung.
   - Einwilligungs-Transitions werden als Flow-Events
     (`call.consent-granted/-denied`) protokolliert.
4. **Noch festzulegen vor Umsetzung:** Speicherort und Verschlüsselung der
   Aufnahmen, Aufbewahrungsfrist + Retention-Job, Zugriffsberechtigungen
   (RBAC-Permission), Betroffenen-Auskunft über Aufnahmen, Löschpfad in der
   Workspace-Kaskade.
