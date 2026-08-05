### Kontext / Konsument

Callora (Communication-Plugin) leitet aus dem SDK-Call-Lifecycle die Call-Disposition ab
(CallLog + `call.*`-Business-Events). Aktuell kann es **remote-terminierte / unbeantwortete**
Calls nicht unterscheiden — alles landet generisch als `Failed`/`Missed`, weil die Ursache an
der öffentlichen Fläche fehlt.

### Ist-Zustand (4.6.0-preview.3)

- Einziges öffentliches Terminal-Signal ist `CalloraVoipSdk.Core.Domain.Events.CallStateChangedEventArgs`
  mit nur `Call`, `OldState`, `NewState` — **keine Ursache**.
- `CalloraVoipSdk.Core.Domain.Calls.ICall` hat **keine** Property für die Terminierungs-Ursache
  (kein `LastTerminationReason`/`EndReason`).
- Kein `CallEnded`/`CallRemoved`-Event trägt eine Ursache.
- `CallActionResult.Reason`/`.SipStatusCode` existiert, aber nur für **selbst** ausgelöste Aktionen
  (Hangup/Reject/Dial) — nicht für remote-initiierte Terminierungen (Busy 486 / NoAnswer 408/480 /
  Reject 603).

### Die Ursache wird intern bereits berechnet

Es geht **nicht** um einen neuen Mechanismus, sondern nur ums Durchreichen eines vorhandenen Werts:

- `CalloraVoipSdk.Core.Infrastructure.Sip.Signaling.SipDialogTerminationReason`
  (ctor `(string, int?, string, int?)`).
- `ISipCallSession.LastTerminationReason` / `SipCallSession.LastTerminationReason`.
- `SipReasonHeader.TryParseFirst(...)` / `SipCallSessionInboundService.TryParseReasonHeader(...)`.
- `SipCallSession.TransitionTo(SipDialogState, SipDialogTerminationReason)` — der interne Terminate
  trägt die Ursache also bereits.

### Gewünschtes Ergebnis

Eine öffentliche, protokoll-neutrale Terminierungs-Ursache am Call, die mindestens trägt:

- SIP-Status-Code (`int?`, z. B. 486/408/480/603/200),
- Reason-Phrase / Text,
- eine grobe Kategorie (Enum, z. B. `Completed`, `Busy`, `NoAnswer`, `Rejected`, `Failed`, `Canceled`),
  abgeleitet aus dem Status-Code,
- wer terminiert hat (lokal/remote), falls verfügbar.

### Vorschlag (Umsetzung offen)

1. **Bevorzugt:** Property `ICall.TerminationReason` (nullable), gesetzt **bevor** der Übergang nach
   `CallState.Terminated` gemeldet wird — Consumer liest die Ursache im vorhandenen
   `StateChanged == Terminated`-Handler; optional zusätzlich als Feld an `CallStateChangedEventArgs`
   für den `Terminated`-Übergang.
2. Alternativ: dediziertes `CallEnded`-Event mit `(CallId, SipStatusCode?, Reason, Category, terminatedBy)`.

### Akzeptanzkriterien

- Öffentliche API exponiert die Terminierungs-Ursache auch für **remote-initiierte** Terminierungen
  (nicht nur selbst ausgelöste).
- Deckt Outbound (Busy/NoAnswer/Rejected/Completed) **und** Inbound (Missed/Rejected/Completed) ab.
- Die Ursache ist **vor oder mit** dem `Terminated`-State-Change lesbar (nicht erst danach — der Call
  wird oft sofort verworfen).
- Interop-Test/Beispiel gegen einen Registrar/Peer (Busy → 486, NoAnswer → 408/480, Remote-Hangup → 200).
- XML-Doku + öffentliche API-Doku aktualisiert.

### Downstream

Schaltet Calloras `CallOutcome`-Anreicherung frei (Busy/NoAnswer statt generisch `Failed`) für CallLog,
Webhooks, MCP und die späteren Dialer/CRM/Analytics-Vertikalen. Fundamental für jeden Consumer.
