# Callora — Architektur-Assessment & Refactoring-Backlog

> Stand 2026-07-16. Gemessen gegen drei Maßstäbe: (1) weltweite .NET-/Software-Standards
> (Microsoft Framework Design Guidelines, SOLID, Clean Architecture, .NET-Analyzer-Konventionen),
> (2) die GoF-Design-Patterns, (3) den Refactoring-Guru-/Fowler-Katalog (Code Smells → Refactorings).
> Grundlage: 4 read-only Fan-out-Audits über Domain/Application/Infrastructure/Module + Tooling-Prüfung.
> Companion: [framework-discovery-boundaries.md](framework-discovery-boundaries.md) (Symfony/Shopware-Vergleich).

## Stand 2026-07-17 (Update)

**Erledigt + gepusht:** R1 (Registrar/Collector + Naht), R5 (R5a `EnforceCodeStyleInBuild` + R5c CAL0003-Vertragsdoku; R5b Roslynator verworfen — Code gegen `AnalysisMode=All` bereits 0 Findings), R7 (HostProtected), **R4-Fundament** (`CalloraException` + zentraler RFC-9457-Handler + WebhookTargetException als Vorlage). Plus die DX-Landkarte B/C/A (siehe [callora-extension-points-map.md](callora-extension-points-map.md)).

**Neubewertung durch die Plattform-Linse** (gegen Shopware/Symfony + .NET-Peers Orchard/ABP/Umbraco/nopCommerce, nicht reine Smell-Sicht): Für ein *erweiterbares Skelett* zählt die Vertragsfläche, nicht interne DDD-Reinheit. Damit:
- **R4 rauf** — echte Peer-Lücke: Shopware hat „Domain Exceptions" als eigene Guideline (Error-Codes + HTTP-Mapping), Symfony/ABP ebenso; Callora hatte 0. Fundament steht; **Ausrollen offen** aber GEZIELT: nur client-facing erwartbare throws migrieren (RBAC-not-found, Plugin-not-found), NICHT die Guards/„cannot happen" (bleiben `InvalidOperationException` → 500) und NICHT die Job-Retry-Signale (WebhookDelivery/Flow-Job-Handler — der throw löst Retry aus, kein HTTP-Response).
- **R2 → Positionierungs-Entscheidung, reif geschnitten** — KEIN Peer nutzt ID-Value-Objects (alle `string`/`Guid`). Empfehlung: KEINE VOs; die echten Smells gezielt heilen — Parameter-Objekt für Data Clumps (pluginId+workspaceKey co-reise) + lange Signaturen, Konstanten/Enums für Magic-String-Scopes. String-IDs behalten (vertraut).
- **R3 → runter/vertagen** — die Mehrheit der Peers (Shopware DAL, Symfony/Doctrine, Umbraco, nop) ist bewusst anämisch/Service-orientiert; nur ABP ist reich-aggregat. Calloras anämische Domain ist plattform-konform. Nur gezielt die ~5-8 echten Invarianten-Träger, wenn überhaupt.
- **R6 (Visitor)** — an Flows/Admin-Shell gekoppelt, nicht vorziehen.

## Kurzurteil

Callora liegt **deutlich über dem Industrie-Median**: moderne .NET-Hygiene (Nullable, Analyzer,
WarningsAsErrors, .editorconfig, disziplinierte Async-Pfade), starke SOLID-/Clean-Architecture-Umsetzung,
ein echter Governance-Analyzer. Der Abstand zu *world-class* ist **Härtung + Disziplin, kein Umbau** —
konkret die unten priorisierten Refactorings R1–R6.

## 1. Dimensions-Scorecard (weltweiter Maßstab)

| Dimension | Note | Kernbeleg / Lücke |
|---|---|---|
| Schichtung/Ports | stark | Ports EF-frei, 0 `DbSet` in Application — Symfony-Niveau |
| SOLID | stark | SRP/OCP/ISP/DIP durchgängig; 1 DIP-Bruch (C1) |
| Async/Concurrency | stark | ConfigureAwait ~94%, CancellationToken propagiert, kein async void/`.Result` |
| Coding-Standards-Disziplin | gut–stark | 0 nested types (656 Dateien), kein stilles catch, WarningsAsErrors projektweit |
| Design-Patterns (GoF) | stark | 11 passende Patterns stark/solide, unpassende korrekt gemieden (s. §2) |
| Style-Enforcement | ausreichend | `.editorconfig` da, aber `EnforceCodeStyleInBuild` **aus**, keine StyleCop/Roslynator |
| Exception-Design (FDG) | schwach | 0 eigene Exception-Typen; 22/23 `throw` = blankes `InvalidOperationException` |
| DDD-Modellierung | ausreichend | ~90% anämische Domain-Typen (Data-Class-Smell); keine Value Objects |
| Public-Doku | schwach | ~60% Public-API dokumentiert, CS1591 per NoWarn abgeschaltet |

## 2. GoF-Pattern-Scorecard

Legende: ★ stark · ◐ solide/punktuell · ○ bewusst ausgelassen (modern-.NET-Ersatz) · △ echte Lücke.
**Ziel ist nicht 23/23** — reifer Code nutzt die passenden ~11 und meidet den Rest (Pattern-Astronomie
wäre selbst ein Anti-Pattern).

**Creational:** Factory Method ★ · Abstract Factory ○ · Builder ◐ · Prototype ○ (records `with`) · Singleton ○ (→ DI-Lifetime)
**Structural:** Adapter ★ · Bridge ○ · Composite ★ (`RuleConditionNode`) · Decorator ★ (§9.2) · Facade ★ (`PluginLifecycleService`) · Flyweight ○ · Proxy ◐
**Behavioral:** Chain of Responsibility ◐ (Middleware echt, `ChainedSecretStore` = Fallback) · Command ★ · Iterator ○ (`IEnumerable`) · Mediator ○ · Memento ○ (Snapshots ≠ Memento) · Observer ★ (`IBusinessEventBus`) · State ○/★ (Konzept ja, GoF-Objekte bewusst nein) · Strategy ★ (`RuleEvaluator`) · Template Method ◐ (`PluginConsoleCommandBase`) · **Visitor △**

**Einzige echte Lücke — Visitor:** Der Composite-Regelbaum (`RuleConditionNode`, and/or/not + Blätter)
wird in `RuleEvaluator` per Typ-Verzweigung abgearbeitet. Sobald eine zweite Operation über denselben Baum
kommt — *validieren* (Config-Check beim Speichern), *erklären/serialisieren* (Flow-Editor-Vorschau) —
ist Visitor das saubere Muster statt duplizierter Typ-Switches. Klein, lokal, an die Flows-Arbeit gekoppelt.

## 3. Refactoring-Backlog (Refactoring-Guru-*Techniken* → konkrete Sites)

Refactoring-Spalte nennt die kanonischen Technik-Namen aus <https://refactoring.guru/refactoring/techniques>.

| # | Code Smell(s) | Refactoring-Techniken | Konkrete Stellen | Prio |
|---|---|---|---|---|
| **R1** | Shotgun Surgery · Duplicate Code · Alternative Classes w/ Different Interfaces | **Extract Superclass** · **Form Template Method** · **Pull Up Method** · **Substitute Algorithm** (die 9 divergenten Merge-Algorithmen vereinheitlichen) · **Move Class** (C1) | 9× Host+Plugin-Collector-Sites (`BusinessEventBus`, `BusinessEventRegistry`, `BackgroundJobHandlerResolver`, `FlowActionRegistry`, `RecurringJobEnqueuer`, `PluginExtensionSynchronizer`, `HostApplicationEventDispatcher`, `PluginWorkspaceDataPurger`, `CalloraConsoleRunner`) — je andere Merge/Order/Dedup/Precedence; Host-Registrierung manuell (Scan nur für Commands). C1: `Domain/Plugins/Contracts/IHostManagedPlugin.cs` → Application. | **HOCH** |
| **R2** | Primitive Obsession · Data Clumps · Long Parameter List | **Replace Data Value with Object** · **Replace Type Code with Class** (Scopes) · **Introduce Parameter Object** · **Preserve Whole Object** · **Encapsulate Field** | 229× `string pluginId/workspaceKey/tenantId`; Magic-String-Scopes (`IntegrationCredential`, `SystemConfigValue`); 24× `workspaceKey`+`tenantKey`-Co-Reise; `WriteAuditAsync`/`PublishEventAsync` (7 Params), `PluginDataDocument.Create` (6). Fix: `PluginId`/`WorkspaceKey`/`TenantKey`/`Scope` als `readonly record struct` + `PluginScope`-Parameter-Objekt. | **HOCH** |
| **R3** | Data Class (Anemic Domain) · Inappropriate Intimacy | **Move Method** · **Encapsulate Field** · **Replace Constructor with Factory Method** · **Introduce Assertion** (Invarianten) · **Change Bidirectional Association to Unidirectional** (H2) | ~33/37 Domain-Typen sind Property-Bags; Vorlage existiert (`BackgroundJob`/`PluginInstallation`). Start bei Invarianten-Trägern: `PluginEntitlement.Revoke()`, `IntegrationCredential`, `WebhookSubscription.Deactivate()`. H2: Cross-Aggregate-Navigation Security↔Workspaces↔Tenants → Id-Referenzen. | **HOCH** |
| **R4** | Inkonsistentes Fehlermodell | **Replace Exception with Test** (Validierungs-throws → Precondition/Result) · typisierte Domain-Exceptions (FDG) | `WebhookEgressGuard`, Flow-Action-Handler, `RuleEvaluator` „not", `EfBackendRbacStore` werfen `InvalidOperationException` für erwartbare Fehler; 0 eigene Exception-Typen. Nordstern = Lifecycle-Result-Familie. | MITTEL |
| **R5** | (Enforcement-Härtung, kein Smell) | `EnforceCodeStyleInBuild=true` + StyleCop/Roslynator; `NoWarn;CS1591` raus + Doku-Lücke schließen; `.editorconfig` verdichten | `Directory.Build.props`; ~200 Public-Typen ohne `///`. Breit wirkend, mechanisch. | MITTEL |
| **R6** | Switch Statements (Type-Code) | **Replace Conditional with Polymorphism** → **Visitor** | `RuleEvaluator` Typ-Dispatch über `RuleConditionNode` — sobald 2. Baum-Operation (Validierung/Erklärung) dazukommt. | NIEDRIG (an Flows gekoppelt) |
| **R7** | (Sicherheit, Folge aus R1 plugin-wins) | ✅ ERLEDIGT — `[HostProtected]`-Marker + geteilter `HostPluginResolution.ResolvePluginWins<T>` | Alle 5 Host-Infrastruktur-Job-Handler markiert (Retention/GDPR, Entitlement/Billing, FlowExecute, Mail, Webhook); Prinzip „Host-Infra geschützt, plugin-wins für offene Extension-Points". Guard-Test sichert die Posture. `CalloraConsoleRunner` bleibt host-wins. Commits `4927ed3`, `1461242`. | ✅ |

### Techniken, die wir bereits sauber anwenden (Kalibrierung)
**Introduce Null Object** (`EmptyCalloraPluginCatalog`, `EmptyPluginExtensionRegistrationStore`) · **Replace Constructor with Factory Method** (`BackgroundJob.Create`, `PluginInstallation`) · **Extract Class** (`PluginLifecycleService` → 12 fokussierte Kollaboratoren) · **Replace Conditional with Polymorphism** (Ansatz: `RuleEvaluator`-Dictionary-Strategy). Das Team spricht die Vokabel bereits — R1–R6 treiben sie nur konsequent durch.

## 4. Empfohlene Reihenfolge

1. **R5** als schnelles, breit wirkendes Vorschalt-Paket (setzt den Enforcement-Boden, bevor wir umbauen).
2. **R1** (zentraler Registrar/Collector + C1) — höchster struktureller Wert, mechanisch (Zielmuster existiert).
3. **R2** (Value/Parameter Objects) — heilt Primitive Obsession + Data Clumps + Long Parameter List in einem.
4. **R3** (reiche Aggregate) — inkrementell, größte DDD-Distanz; baut auf R2 (Value Objects) auf.
5. **R4** (Result/Exception-Konvention).
6. **R6** (Visitor) — gekoppelt an die spätere Flows-/Surface-Arbeit.

Danach zurück zur Produkterstellung (Surface/Admin-Shell).
