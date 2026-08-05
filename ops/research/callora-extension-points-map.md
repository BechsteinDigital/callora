# Callora Extension-Point-Landkarte — Ist-Zustand vs. .NET-Peers, mit DX-Lücken

**Zweck:** Kalibrierung von Calloras Erweiterbarkeit gegen die vier etablierten .NET-Plugin-Plattformen (Orchard Core, ABP, Umbraco, nopCommerce) und gegen Shopware/Symfony. Ziel ist nicht Gleichstand, sondern **besser und entwicklerfreundlicher** — die Punkte identifizieren, an denen Callora die Vorbilder übertreffen kann.

Stand 2026-07-17. Ergänzt [[callora-governance-analyzer-2026-07]], [[callora-plattform-reife-roadmap]], [[callora-extension-framework-2026-07]].

## Kern-These

Die Sichtbarkeits-Frage ("374 internal?") ist **entschieden: nein.** Alle vier .NET-Peers liegen näher an Shopware-Offenheit als an Symfony-Kapselung; keiner internalisiert aggressiv. Callora ist bereits **strenger als alle vier an den zwei richtigen Stellen** — 100 % `sealed` (vs. ABP/nop "virtual überall") und als einziges mit compiler-erzwungener Governance (CAL0001/2/3). Der Hebel für "entwicklerfreundlicher" ist daher **nicht Sichtbarkeit, sondern die Ergonomie und Explizitheit der Extension-Points** — mit Calloras einzigartigem Trumpf: ein Compiler, der den Plugin-Autor aktiv zum richtigen Weg führt.

## Positionierung im Spektrum

```
Symfony ──────── Orchard ──── ABP ──── Umbraco ──── nopCommerce
gekapselt        internal-    virtual  offen +      alles public+
@internal/@final Kern+SemVer  überall  ApiCompat    virtual, 0 Governance
                                   ▲
                              Callora sitzt sichtbarkeits-seitig hier (offen wie die Peers),
                              aber governance-seitig LINKS von allen (formaler als Symfony-Marker).
```

| | Sichtbarkeit | Vererbung | Extension dominant | BC-Governance |
|---|---|---|---|---|
| Orchard | public Verträge, schmaler `internal`-Kern via IVT | abstract+virtual | additiver Multi-Handler-Fan-out | SemVer + `[Obsolete]` |
| ABP | ~2400 public / 45 internal | `virtual` by design | DI-Replace + Vererbung + dualer Event-Bus | SemVer, Deprecation-Fenster |
| Umbraco | ~1270 public / 98 internal | Composition-first | **Notification-Handler (sync/async, mutable, cancel)** | SDK-ApiCompat |
| nopCommerce | alles public partial, 0 IVT | `virtual` überall | DI-Override-per-`Order` + `IConsumer<T>` | praktisch keine |
| **Callora** | 516 public, **312 sealed / 0 offen** | **100 % sealed** | Contract-Fan-out + Decoration + Events + plugin-wins | **CAL0001/2/3 + PublicApiAnalyzers** |

## Callora-Extension-Inventar (verdichtet)

Belegt via Code-Audit (`src/Core/Application/*/Contracts/`, `…/Plugins/`, `…/Events/`):

- **Contract-Handler (Plugins implementieren + `context.Export<T>`):** IBackgroundJobHandler, IRecurringJobProvider, IFlowActionHandler, IRuleConditionEvaluator (sync), IBusinessEventListener, IBusinessEventProvider, IHostEventSubscriber<T>, ICalloraConsoleCommand, IWorkspaceDataPurgeContributor, IServiceDecorator<T>, IHostPluginExtensionContributor, IHostAdminApiExtensionContributor, IHostAdminApiRouteHandler, IPluginMigration.
- **Zwei Event-Kanäle:** Business-Event-Bus (named, `MergeWithHost`, Priority, fehlertolerant) + Host-Application-Events (typsicher `IHostEventSubscriber<T>`, Priority, Propagation-Stop optional).
- **Service-Decoration:** `IServiceDecorator<T>` mit `Order` — aber nur auf Services, die der Host explizit durch `PluginServiceDecoration.Decorate()` schleust.
- **Präzedenz:** plugin-wins + `[HostProtected]` (HostPluginResolution), zentral.
- **Daten:** IPluginDbContextFactory<T> (eigenes Postgres-Schema `plugin_<id>`, EF-Migrations, Advisory-Lock), IPluginDataStore (JSON-Doc-Store), ICustomFieldAccessor (JSON-Extra-Felder auf workspace/call/user), IPluginDataProtector (isolierte Verschlüsselung).
- **Host-Services konsumierbar:** IMailSender (dekorierbar), INotificationPublisher, IMediaLibrary, IWebhookEventPublisher, IPluginConfigReader (Workspace>Tenant>Global>Default).
- **UI/API:** Admin-API-Routes + Navigation über Contributor + Scope-Guard (Extension-Point-Registry mit Text-IDs).
- **Discovery/Lifecycle:** ALC-Isolation, `IHostManagedPlugin.StartAsync` → `Export`, ICalloraPluginCatalog.

## Gegenüberstellung → DX-Lücken

| Extension-Bereich | Bester Peer-Stand | Callora-Ist | Lücke |
|---|---|---|---|
| **Reaktive Events** | Umbraco: sync+async, **mutable**, **cancelable**, before/after (`*ing`/`*ed`), Auto-Discovery-from-Assembly | Business-Events **read-only**, nicht cancelable; Host-Events cancel nur wenn Event `IHostEventPropagationState` implementiert (uneinheitlich) | **groß** — Handler können nur *reagieren*, nicht *eingreifen* |
| **Extension-Point-Katalog** | keiner hat Typsicherheit | Text-IDs ("workspace.navigation.main"), Typo erst beim Sync | **Calloras Trumpf ungenutzt** — hier könnte der Compiler führen |
| **Selbst-Discovery der Points** | keiner | `[CalloraExtensible]` existiert, aber praktisch nirgends gesetzt | **verpasster DX-Trumpf** |
| **UI-Erweiterung** | nopCommerce Widget-Zones + ViewComponents; Umbraco Collection-Builder | nur deklarativ (Navigation/Routes), keine Render-Slots | **relevant für Admin-Shell (Task #30)** |
| **Datenmodell-Erweiterung** | ABP ObjectExtensionManager (Extra-Props auto über DB/API/UI, query-fähig) | Custom Fields = JSON, kein Query; volle Entities nur per DbContext | **mittel** |
| **Verteilte Events** | ABP: Local+Distributed, identische API (Monolith→Microservice) | nur in-process | **klein** (später relevant) |
| **Geordnete Kollektionen** | Umbraco OrderedCollectionBuilder (Insert-Before/After/Replace) | `MergeWithHost` + Priority (kein Insert-relativ) | **klein** |

## Priorisierte DX-Hebel (wo Callora die Peers übertrifft)

### A. Mutable + cancelable Events — "eingreifen statt nur reagieren" (größter Hebel)
**Vorbild:** Umbraco `INotificationHandler<T>` / `INotificationAsyncHandler<T>`, before/after-Paare (`*ing` cancelable, `*ed` nicht), mutables `Target` + `State`-Bag. **Der Wunsch des Product-Owners** ("besser mit EventHandlern für Listener/Subscriber").
**Callora-Ansatz:** ein cancelable/mutable "Before"-Event-Modell einführen (z. B. `IMutableHostEvent` mit `Cancel`/`State`), klare `*ing`/`*ed`-Namenskonvention, plus sync- **und** async-Handler-Split. Host-Events haben die Propagation-Stop-Basis schon — verallgemeinern und dokumentieren.
**Warum besser:** Callora kombiniert das mit Priority + plugin-wins + `[HostProtected]` — Umbraco hat kein Schutz-Konzept für kritische Handler.

### B. Compiler-geführte Extension-Point-IDs — Calloras Alleinstellungsmerkmal
**Problem:** Extension-Point-IDs sind lose Strings; kein Peer prüft sie.
**Callora-Ansatz:** generierte Konstanten (`CalloraExtensionPoints.Workspace.NavigationMain`) + ein CAL-Analyzer, der unbekannte/vertippte IDs *zur Compile-Zeit* meldet — mit Vorschlag der gültigen Points. Das nutzt euren einzigen echten Vorteil (Roslyn-Governance) an einer Stelle, wo alle vier Peers blind sind.
**Warum besser:** "der Compiler kennt die Extension-Points" — kein Peer bietet das.

### C. `[CalloraExtensible]` als aktive Discovery-Fläche
**Problem:** Der Marker existiert (G4), wird aber fast nirgends gesetzt; die Doku-Pflicht CAL0003 greift schon.
**Callora-Ansatz:** `[CalloraExtensible]` auf alle Extension-Interfaces (mit einem "was ein Plugin damit tut"-Satz) → CAL0003 erzwingt die Doku, und ein `plugin:points`-Konsolenbefehl kann alle markierten Points + ihre Doku auflisten. Der Plugin-Autor findet die gesamte Erweiterungsfläche über *eine* Konvention.
**Warum besser:** selbst-dokumentierende, compiler-erzwungene Extension-Fläche — kein Peer hat das.

### D. Render-Slot-/Widget-Modell für die Admin-Shell
**Vorbild:** nopCommerce Widget-Zones + ViewComponents; Umbraco Collection-Builder.
**Callora-Ansatz:** beim anstehenden Admin-Shell-Neuaufbau (Task #30) benannte UI-Slots einführen, in die Plugins deklarativ Komponenten/Cards hängen — statt nur Navigation+Routes. Jetzt entscheiden, damit die neue Shell den Extension-Punkt von Anfang an hat.
**Warum besser:** wenn die Slots dieselbe `[CalloraExtensible]`+ID-Governance nutzen wie B/C, sind UI-Extensions genauso compiler-geführt wie Code-Extensions.

### E. Query-fähige / auto-propagierende Custom-Entity-Extension
**Vorbild:** ABP ObjectExtensionManager (Extra-Props ohne Migration, propagiert über DB/API/UI).
**Callora-Ansatz:** optionaler Schritt — Custom Fields um ein schmales Query-/Filter-API erweitern oder ein leichtgewichtiges "extra column"-Muster neben dem vollen DbContext. Niedrigere Priorität.

### F. Politur: Collection-Builder-Ordering + DbContext-Scope-Ergonomie
Insert-relative Ordering (before/after statt nur Priority-Zahl) und scoped DbContext-Lifetime. Kleinteilig, später.

## Empfohlene Reihenfolge

1. **B + C zusammen** (Extension-Point-Governance + `[CalloraExtensible]`-Discovery) — kleiner, hoher DX-Hebel, nutzt vorhandene Analyzer-Infrastruktur, macht die gesamte Fläche auffindbar und compiler-geprüft. Der beste "sofort spürbar besser als die Peers"-Schritt.
2. **A** (mutable/cancelable Events) — der inhaltlich größte Freiheits-Hebel, aber Design-lastiger (Event-Modell, Migration der bestehenden read-only-Events). Braucht ein eigenes Design-Paket.
3. **D** (UI-Slots) — an Task #30 (Admin-Shell) koppeln, nicht davor.
4. **E, F** — später, optional.

## Nicht-Ziele (bewusst)

- **Keine aggressive Internalisierung** der 374 — gegen die gesamte .NET-Praxis, nimmt Freiheit ohne Sicherheitsgewinn (sealed genügt).
- **Kein `virtual`-überall** (ABP/nop) — das Fragile-Base-Class-/BC-Problem ist mit sealed+Decoration bereits gelöst.
- **Optional:** InternalsVisibleTo-"Trusted-Inner-Circle" (Orchard-Stil) für die ~74 rein framework-internen Typen — nur wenn die public-Baseline-Pflege drückt; kein DX-Gewinn für Plugin-Autoren.
