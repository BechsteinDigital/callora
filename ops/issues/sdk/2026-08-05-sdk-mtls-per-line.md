> **Konsument-getrieben.** Aufgekommen bei der Callora-Audit-Remediation
> (BechsteinDigital/callora#111): Callora bewirbt mutual-TLS-SIP-Accounts in UI und API,
> kann sie mit SDK 4.7.3 aber nicht pro Account abbilden.

### Kontext / Konsument

Callora ist mandantenfähig: SIP-Accounts gehören je einem Workspace, und ein
`MutualTlsAuthentication` trägt eine **Referenz auf das Client-Zertifikat im geschützten
Secret-Store** — nicht das Zertifikat selbst und keinen Dateipfad. Zwei Workspaces desselben
Deployments können unterschiedliche Zertifikate desselben Carriers oder unterschiedlicher
Carrier haben.

### Ist-Zustand (verifiziert gegen 4.7.3-local.202608032047)

- Die SIP-TLS-Konfiguration hängt am **Client**, nicht an der Line:
  `VoipOptions.Tls` → `CalloraVoipSdk.Core.Application.Ports.Security.TlsConfiguration`
  mit `CertificatePath`, `CertificatePassword`, `TrustMode`, `ExpectedSipDomain`,
  `AcceptUntrustedCertificates`.
- `CalloraVoipSdk.Core.Domain.Lines.SipAccount` trägt **kein** TLS-/Zertifikatsfeld
  (`DisplayName`, `Username`, `Password`, `SipServer`, `Transport`, `Port`,
  `RegistrationExpiry`, `OutboundProxy`, `PublicSipHost`, `PublicSipPort`,
  `PublicMediaHost`, `InboundNumbers`, `AcceptTrunkInbound`, `Reregister`).
- `SipTlsCertificateProvider` hält genau **ein** `_certificate` und lädt es über
  `LoadCertificate(path, password)` — also **nur aus einer Datei**.

Daraus folgen zwei Blocker:

1. **Ein Zertifikat je Prozess-Client.** Für zwei Accounts mit verschiedenen Zertifikaten
   müsste der Konsument je Zertifikat eine eigene `IVoipClient`-Instanz bauen und die
   Accounts danach gruppieren. Das vervielfacht Sockets, Ports und Lifecycle-Zustand für
   etwas, das eine Per-Line-Eigenschaft ist.
2. **Zertifikat muss auf die Platte.** Ein Secret aus einem geschützten Store muss zum
   Verbinden in eine Datei materialisiert werden — genau das, was ein Secret-Store
   verhindern soll. `X509Certificate2` liegt im Prozess bereits vor (`VoipOptions.DtlsCertificate`
   nimmt es für DTLS ja auch direkt entgegen), nur der SIP-TLS-Pfad verlangt einen Pfad.

### Gewünschtes Ergebnis

- **TLS-Konfiguration pro Line/Account**: `SipAccount` (oder ein Parameter beim Verbinden der
  Line) trägt eine optionale `TlsConfiguration`, die die Client-weite überschreibt. Ohne
  Angabe bleibt das heutige Verhalten unverändert.
- **Zertifikat aus dem Speicher**: `TlsConfiguration` akzeptiert ein `X509Certificate2`
  (bzw. `ReadOnlyMemory<byte>` + Passwort) als Alternative zu `CertificatePath`. Analog zu
  `VoipOptions.DtlsCertificate`.
- **Mehrere Zertifikate parallel**: `SipTlsCertificateProvider` löst das Zertifikat je
  Line/Verbindung auf statt einmal je Client.

### Akzeptanzkriterien

- [ ] Zwei Lines eines einzigen `IVoipClient` können mit **unterschiedlichen**
      Client-Zertifikaten gegen denselben oder verschiedene Registrare verbinden.
- [ ] Ein Zertifikat kann als `X509Certificate2` übergeben werden, ohne dass es je auf der
      Platte liegt; der Dateipfad-Weg bleibt erhalten.
- [ ] `ExpectedSipDomain` und `TrustMode` sind ebenfalls pro Line setzbar (verschiedene
      Carrier, verschiedene Domains).
- [ ] Interop end-to-end gegen einen Registrar mit `require_client_cert` (z. B. Asterisk
      `pjsip.conf` mit `verify_client=yes`), inklusive Ablehnung bei falschem Zertifikat.
- [ ] XML-/API-Doku beschreibt Vorrang (Line-Konfiguration schlägt Client-Konfiguration) und
      den Lifecycle des im Speicher übergebenen Zertifikats (wer disposed).

### Downstream

Entsperrt in Callora den Account-Typ `MutualTlsAuthentication`
(BechsteinDigital/callora#111). Bis dahin lehnt Callora mutual-TLS-Accounts bei der Anlage
mit 422 ab, statt sie zu bewerben und beim Provisionieren zu überspringen.

### Verwandt

- #159 — SIP-TLS-Trust, RFC-5922-Identitäten und Zertifikats-Lifecycle (geschlossen); dieses
  Issue baut auf dessen Trust-Modell auf und ergänzt nur die Granularität und die Quelle des
  Zertifikats.
- #104 — registrierungsloser / IP-authentifizierter Trunk; der zweite Account-Typ, den
  Callora modelliert und das SDK noch nicht tragen kann.
