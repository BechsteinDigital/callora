# MCP Tool Framework + Call-Control-Adapter — Design

**Datum:** 2026-07-27
**Status:** in Freigabe (Kontur konvergiert)
**Kontext:** `ICallControlService` (Communication) bedient bereits in-process (DI) + REST/Webhooks.
Es fehlt das **agenten-native Gesicht**: MCP (Model Context Protocol), damit externe AI-Agenten
Call-Control als Tools konsumieren. „Ein Service, mehrere Gesichter" — MCP ist ein weiterer dünner Adapter.

## Understanding

- **Was:** Eine generische MCP-Server-Schale (Host) + ein neutraler Tool-Beitrags-Contract, sodass
  Plugins Fähigkeiten als MCP-Tools an externe AI-Agenten exponieren. Erster Consumer: Communication-Call-Control.
- **Warum:** Beachhead Voice-AI — externe Agenten (STT/LLM/TTS) steuern Calls über MCP ohne In-process-Kopplung.
  Keine Doppel-Logik: die Tools rufen `ICallControlService`.
- **Für wen:** out-of-process MCP-Clients / AI-Agenten.
- **Kernprinzip:** dünner Adapter; **Transport-Schale = generische Host-Infra** (wie die REST/WS-Catch-alls),
  **Tools = Inhalt im Plugin**. Kommerzialisierung liegt in einer **separaten, späteren Lizenz-Schicht**, nicht hier.

### Non-Goals

- Keine neue Call-Control-Logik (nutzt `ICallControlService`).
- Nur Call-Control-Tools v1 — keine flächendeckende Plugin-Exposition.
- Keine MCP-Resources/Prompts v1 (nur Tools). Live-`call.*`-Events laufen über Webhooks.
- Kein stdio-Transport (nur HTTP).
- **Kein Lizenz-/Kommerz-Gate in v1** — das Lizenz-Subsystem ist eine eigene, spätere Initiative (s. u.).
  MCP-Adapter v1 verhält sich voll offen (Community).
- **Keine OAuth-Authorization-Server-Seite** (Browser-Onboarding: `/authorize`+PKCE, Discovery, DCR) — gehört zur
  späteren Account-/Portal-Initiative. v1 = **Resource Server**, Agenten nutzen vorab ausgestellte Operator-Bearer-Tokens.

## Architektur — „Schale im Host, Inhalt im Plugin" (wie REST/WS)

```
Externer AI-Agent ──MCP / Streamable HTTP──▶ /mcp  (Host, einmal beim Start gemountet, authentifiziert)
                                               │
                            McpToolAggregator (Host — freie Infra, neben Webhooks)
                              · live McpServerTool-Collection
                              · synchron zu ICalloraPluginCatalog (activate/deactivate) → tools/list_changed
                              · pro Call: Auth → Workspace-Scope → RBAC-Permission → invoke
                                               │  sammelt
                            IMcpToolContributor-Exporte (Core-Contract, SDK-neutral)
                                               ▲
                            CommunicationMcpToolContributor (Plugin — der „Inhalt")
                              · place_call / hangup_call / get_call / list_recent_calls
                              · Handler rufen ICallControlService
```

**Warum die Schale im Host, nicht als hot-Plugin:** ASP.NET friert die Endpunkt-Tabelle nach `app.Build()`
ein; Plugins laden über Hosted-Services **danach**. Kein Plugin (auch kein static) kann `MapMcp` mounten —
genau wie Communications REST/WS-Mount host-seitig ist und das Plugin nur Routen beisteuert. Die Schale ist
generisch/plugin-agnostisch und bedient dynamisch, was gerade aktiv ist → **Tools eines Plugins sind bei
Aktivierung sofort live (kein Restart, kein Re-Mount, kein Verbindungsabbruch)**.

## Entscheidungen

1. **Transport: Streamable HTTP** über `ModelContextProtocol.AspNetCore` (1.4.1, offiziell, net8+), gemountet
   unter `/mcp`. stdio zurückgestellt.
2. **Neutraler Contract in Core:** `IMcpToolContributor` + `McpToolRegistration`. Plugins referenzieren das
   MCP-SDK **nicht** — der Host übersetzt neutrale Registrierungen in SDK-`McpServerTool`s (wie
   `HostAdminApiRouteRegistration` ASP.NET-neutral ist).
3. **Dynamische Tool-Menge:** live Collection synchron zum Plugin-Katalog (activate → Tools rein + `list_changed`,
   deactivate → raus). Konsistent mit Calloras Hot-Install-Modell.
4. **Auth = OAuth-2.1-Resource-Server (MCP-Standard, RFC 9728).** Der MCP-Server validiert **Bearer-JWTs**:
   `.AddJwtBearer` (Calloras Operator-Token — Issuer/Audience/SigningKey aus `BackendHostOptions`) +
   `.AddMcp(ResourceMetadata)` (offizielles SDK, `McpAuthenticationDefaults`) → exponiert
   `/.well-known/oauth-protected-resource` (Protected Resource Metadata) + `401`-`WWW-Authenticate`-Challenge mit
   `resource_metadata`-Zeiger. `MapMcp("/mcp").RequireAuthorization()`.
   **Pro Tool-Call** dann Callora-RBAC via `IHttpContextAccessor`→`HttpContext.User`: Workspace-Scope wie
   `CallAdminScope` (token-gebundener `workspace_key` gewinnt; Plattform-Operator übergibt `workspaceKey`-Arg) +
   Tool-`RequiredPermission` (`communication.calls.read/manage`). **Der Host scoped + prüft — der Plugin-Handler
   bekommt den fertigen Workspace.**
   **Authorization-Server-Seite ist NICHT hier** (Browser-OAuth: `/authorize`+PKCE, Discovery, DCR) — gehört zur
   späteren Account-/Portal-Initiative. v1: Agenten nutzen ein **vorab ausgestelltes Operator-Bearer-Token**; strikte
   per-Resource-`aud`-Bindung (Token-Audience = MCP-Resource-URL statt `callora-host-api`) als Follow-up.
5. **Transport-Schale + Aggregator = freie Host-Infra** (neben Webhooks; Core ist bereits `Microsoft.NET.Sdk.Web`).
   Das MCP-SDK ist internes Impl-Detail; der öffentliche Vertrag bleibt der neutrale `IMcpToolContributor`.
6. **Kommerzialisierung NICHT über Entitlements, sondern über eine separate Lizenz-Schicht** (s. u.) — in v1
   nicht verdrahtet.

## Contract (Core, SDK-neutral)

- `IMcpToolContributor { IReadOnlyList<McpToolRegistration> Tools { get; } }`
- `McpToolRegistration(string Name, string Description, JsonElement InputSchema, string RequiredPermission,
   Func<McpToolInvocation, CancellationToken, Task<McpToolResult>> Handler)`
  — **nur `RequiredPermission` (RBAC)**; kein Entitlement/Lizenz-Feld (das ist eine separate, spätere Achse).
- `McpToolInvocation`: geparste Argumente (`JsonElement`), **bereits aufgelöster** `WorkspaceKey`, Caller-Principal.
- `McpToolResult`: neutrales Ergebnis (Text-/JSON-Payload + `IsError`).

## Tools v1 (Communication)

| Tool | Permission | Args | Ergebnis |
|---|---|---|---|
| `place_call` | calls.manage | `{to, channelId?, displayName?}` | CallSnapshot |
| `hangup_call` | calls.manage | `{callId}` | `{hungUp: bool}` |
| `get_call` | calls.read | `{callId}` | CallSnapshot \| null |
| `list_recent_calls` | calls.read | `{limit?}` | CallHistoryEntry[] |

`workspaceKey`-Arg optional; nur für Plattform-Operatoren nötig.

## Kommerzialisierung / Lizenzierung (separates, späteres Vorhaben)

**Nicht Teil dieses Adapters.** Festgehalten als Nordstern:

- Modell **à la Shopware**: jede Instanz (auch self-hosted **Community-Edition**) hat eine **Instanz-Identität**,
  verknüpft mit einem **zentralen Callora-Konto/Portal**. Kommerzielle Fähigkeiten (z. B. „Agent-Access"/MCP-Tools)
  werden **pro Kunde im Portal** freigeschaltet; die Instanz **validiert die Lizenz periodisch, offline-tolerant**.
- **Getrennt von den internen Entitlements** (die interne Capability-Grants pro Workspace sind). Lizenz = externer,
  account-gebundener Kommerz-Grant.
- **Andockpunkt in MCP (additiv, später):** ein per-Aufrufer-Filter im `McpToolAggregator` + optionales `/mcp`-Gate,
  gespeist vom Lizenz-Subsystem — blendet nicht-lizenzierte Tools pro Aufrufer aus. Derselbe freie Endpunkt zeigt
  je Kunde andere Tools. Kein Redesign des Frameworks nötig.
- **Monetarisierungshebel** (später): „Agent-Access"-Tier schaltet `/mcp` frei; High-Value-Tool-Plugins bezahlt;
  Usage-Metering an Tool-Calls. Der Transport selbst bleibt frei (wertlos ohne Tools).

## Slices

### M1 — Host-MCP-Framework (freie Infra)
- Package-Ref `ModelContextProtocol.AspNetCore`; `IMcpToolContributor` + `McpToolRegistration`/`McpToolInvocation`/
  `McpToolResult` (Core); `McpToolAggregator` + Mount `/mcp` (Host-Composition) mit Auth+Scope+RBAC-Permission-Wrapper
  + dynamischer Katalog-Sync + `tools/list_changed`.
- Tests: Scope-Auflösung (token-bound vs. Arg), Permission-Deny, dynamisches Add/Remove spiegelt sich in der
  Collection, `list_changed` gefeuert.

### M2 — Communication-Tools (der Inhalt)
- `CommunicationMcpToolContributor` (4 Tools über `ICallControlService`); Export in `CommunicationPlugin.StartAsync`;
  Tests (je Tool Happy-Path + Arg-Validierung + Scope-Durchreichung).

## Testing

- **Aggregator:** Fake-Katalog + Fake-Contributor; Scope-Auflösung, Permission-Gate (Deny ohne Permission),
  dynamisches Add/Remove, `list_changed`.
- **Communication-Tools:** Fake-`ICallControlService`; je Tool Args→Service-Call→neutrales Ergebnis,
  Permission-Tags korrekt, Workspace durchgereicht.
- **E2E-Handshake (falls im Harness tragbar):** Host hochfahren + MCP-Client (SDK liefert einen) gegen `/mcp` mit
  Token: Tools listen, `place_call` gegen Fake-Service.

## Offene Risiken (bei Umsetzung an Paket-XML verifizieren)

- Exakte SDK-API für dynamische Collection + `tools/list_changed`.
- Wie ein Tool-Handler den authentifizierten `HttpContext`/Principal liest (SDK-`RequestContext`/`IHttpContextAccessor`).
- Nebenläufigkeit der live Collection (gleichzeitige Katalog-Änderung + Client-`tools/list`).

## Decision Log

- **Transport-Schale im Host, nicht als Plugin** — ASP.NET-Endpunkt-Tabelle friert nach `app.Build()` ein; Plugins
  laden danach. Gleiche Trennung wie REST/WS (Host mountet Schale, Plugin liefert Inhalt). Self-hosted-Port-Variante
  verworfen: eigener Port + Auth-Nachbau, weicht vom Muster ab, verkauft die billigste Schicht.
- **Kommerzialisierung über separate Lizenz-Schicht, nicht Entitlements** (User-Entscheid) — Lizenz = externer,
  account-gebundener Kommerz-Grant (Shopware-Modell, auch CE); Entitlement = interner Workspace-Capability-Grant.
  MCP-Transport bleibt freie Infra; Wert steckt in den Tools. v1 liefert offen aus; Lizenz-Gate dockt später additiv an.
- **Dynamische Collection statt statisch** (User-Entscheid) — konsistent mit Hot-Install; Tools live bei Aktivierung,
  kein Restart/Re-Mount/Verbindungsabbruch, weil nur die Tool-Liste mutiert (nicht der Mount).
- **Neutraler Core-Contract statt SDK-Typen in Plugins** — hält Plugins vom MCP-SDK entkoppelt (wie `ICall` SDK-neutral).
- **Host scoped, nicht der Handler** — eine Auth-Wahrheit (wie `PluginAdminExtensionEndpoints` + `CallAdminScope`).
- **Auth = OAuth-2.1-Resource-Server (MCP-Standard), nicht hand-rolled** — SDK liefert `.AddMcp(ResourceMetadata)` +
  Bearer-Validierung; RS validiert Calloras Operator-JWTs. Die AS-Seite (Browser-Onboarding/PKCE/DCR) ist bewusst
  vertagt an die Account-/Portal-Initiative; v1 nutzt vorab ausgestellte Operator-Tokens. Naive „nur
  `.RequireAuthorization()`"-Annahme verworfen: übersah PRM/`WWW-Authenticate`-Discovery.
