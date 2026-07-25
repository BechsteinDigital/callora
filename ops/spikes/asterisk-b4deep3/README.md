# B4-deep-3 — Voice-Registrierung gegen echtes Asterisk

Beweist end-to-end, dass der CalloraVoipSdk-Client sich real gegen einen SIP-Registrar registriert
und dadurch die Runtime-Capability `communication.voice` scharf wird. Ergänzt die fake-gestützten
Unit-Tests (`SdkVoiceChannelConnectorTests`, `CommunicationPluginWiringTests`) um den echten SIP-Pfad.

Die Tests sind **opt-in**: ohne `CALLORA_ASTERISK_TESTS=1` und laufendes Asterisk werden sie
übersprungen (`[SkippableFact]`), damit CI ohne SIP-Server grün bleibt.

## Asterisk starten

```bash
docker run -d --name callora-asterisk --network host \
  -v "$PWD/ops/spikes/asterisk-b4deep3/pjsip.conf:/etc/asterisk/pjsip.conf" \
  -v "$PWD/ops/spikes/asterisk-b4deep3/extensions.conf:/etc/asterisk/extensions.conf" \
  andrius/asterisk:latest
```

`--network host` ist nötig, weil SIP/RTP mehrere UDP-Ports aushandelt (NAT-freier Pfad zu 127.0.0.1).

Konfiguration: ein UDP-Transport auf `0.0.0.0:5060`, ein registrierender Endpoint `callora`
(Passwort `callora`, digest). Der AOR heißt bewusst `callora` — der pjsip-Registrar sucht den AOR
über den User-Teil der REGISTER-To-URI, nicht über den Endpoint-Namen. Der Endpoint hat
`media_encryption=sdes`, weil der CalloraVoipSdk-Client per Default **SRTP** anbietet
(`m=audio … RTP/SAVP` + `a=crypto`); ohne das lehnt Asterisk das Angebot mit `488 Not Acceptable
Here` ab. Extension `600` im Dialplan beantwortet und echoed Audio zurück (RTP↔Media-Round-Trip).

## Tests ausführen

```bash
CALLORA_ASTERISK_TESTS=1 dotnet test tests/Callora.Core.Tests/Callora.Core.Tests.csproj \
  --filter "FullyQualifiedName~Integration.Asterisk"
```

- `AsteriskVoiceIntegrationTests` — der SDK-Client registriert das SIP-Konto (`LineState.Registered`).
- `AsteriskRuntimeCapabilityIntegrationTests` — die volle Produktionskette (`VoipClientVoiceRuntime`
  → `SdkVoiceChannelConnector` → `SdkVoiceChannel` → Registry → `CommunicationRuntimeCapabilitySource`)
  grantet nach der echten Registrierung `communication.voice` für den Workspace.
- `AsteriskAudioEchoIntegrationTests` — platziert einen Call zur Echo-Extension 600, öffnet die
  Foundation-Audio-Bridge (`ICallAudioStream`), streamt ein markantes µ-law-Muster und prüft, dass
  dieselben Bytes über SRTP zurückkommen (RTP↔Media-Bridge round-trippt echtes Medium).

## Aufräumen

```bash
docker rm -f callora-asterisk
```

> Hinweis: Bind-gemountete Einzeldateien werden bei einem Host-seitigen Edit über einen neuen Inode
> ersetzt; der Container sieht die Änderung dann erst nach `docker rm -f` + erneutem `docker run`
> (ein `pjsip reload` allein greift auf den alten Inode).
