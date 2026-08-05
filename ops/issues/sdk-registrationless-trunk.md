> **Optional / niedrige Priorität.** Der Massenmarkt (sipgate-Trunk, easybell, Telekom CompanyFlex)
> registriert mit Credentials und wird plugin-seitig als „registrierender Trunk" abgedeckt (kein
> SDK-Change nötig). Dieses Issue betrifft nur den reinen **registrierungslosen Static-IP-Fall**.

### Kontext / Konsument

Callora modelliert einen `SipAccountMode.Trunk` mit `IpAuthentication` (bewusst **ohne** Credentials,
ohne Registration-Expiry) — ein klassischer IP-authentifizierter Static-IP-Trunk. Das lässt sich auf
das SDK 4.6 aktuell **nicht** abbilden.

### Ist-Zustand (4.6.0-preview.3)

- `SipAccount.Username` ist **required** (dient auch als AOR-User-part), `SipServer` ist der
  **required** Registrar.
- Das SDK **registriert immer** — es gibt keinen „REGISTER überspringen"-Schalter.
  `ReregisterOptions.Disabled` schaltet nur die **Re**-Registrierung nach Verbindungsverlust ab,
  nicht das **initiale** REGISTER.
- `Password` ist bereits optional (gut für IP-Auth-Registrare, die nicht challengen) — aber ein
  Account **ohne** Username und **ohne** REGISTER ist nicht darstellbar.
- `LineState` ist rein registrierungszentriert
  (`Unregistered`/`Registering`/`Registered`/`RegistrationFailed`/`Reconnecting`/`Failed`) — es fehlt
  ein Zustand „erreichbar, aber nicht registriert".

### Gewünschtes Ergebnis

- Eine Line **ohne initiales REGISTER** verbinden können (IP-Auth-Trunk): Outbound-INVITEs direkt an
  `SipServer`/`OutboundProxy`, Inbound über `AcceptTrunkInbound`/`InboundNumbers`.
- Ein Health/State-Modell für „nicht registriert, aber betriebsbereit", damit die Line nicht als
  `Unregistered` → unbrauchbar erscheint.
- Optional: `Username` in diesem Modus optional machen bzw. eine AOR ohne Auth-User erlauben.

### Akzeptanzkriterien

- `SipAccount` (oder `ConnectOptions`) trägt einen expliziten „nicht registrieren"-Modus.
- `IPhoneLine.State`/`LineState` unterscheidet „registrierungslos betriebsbereit" von
  „Registrierung fehlgeschlagen".
- Outbound + Inbound gegen einen IP-authentifizierten Trunk-Peer (z. B. Asterisk `type=peer`,
  `insecure=invite`, kein Auth) end-to-end validiert.
- XML-/API-Doku aktualisiert.

### Downstream

Erlaubt Callora, `SipAccountMode.Trunk` + `IpAuthentication` (registrierungslos) real zu verbinden.
Bis dahin deckt Callora nur den **registrierenden** Trunk ab.
