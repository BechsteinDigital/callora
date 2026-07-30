# ADR-015 — Surface-Rendering-Architektur und Template-Engine

**Status:** Accepted; **Engine-Wahl revidiert 2026-07-19 (Scriban → Nunjucks/Jint)**
**Datum:** 2026-07-16 (Rev. 2026-07-19)
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

> ## Revision 2026-07-19 — Engine A: Nunjucks auf Jint statt Scriban
>
> Die **geschichtete Architektur** (§2–§4, §7), der **A/B-Schnitt** (§5) und die
> **Sandbox-Pflichten** (§8) bleiben unverändert. Revidiert wird nur die **Base-
> Engine A**: statt Scriban wird **Nunjucks** (native Twig-artige Vererbung
> `extends`/`block`/`{{ super() }}`, Includes, Macros) auf dem **Jint**-JS-
> Interpreter im gehärteten Sandbox-Modus ausgeführt.
>
> **Warum:** Scriban hat **keine** native Block-Vererbung → Baustein B (§5/§9)
> hätte einen kompletten Multi-Inheritance-**Compiler** erfordert (der aufwändigste
> Eigenbau). Recherche (2026-07-19): **keine** reine-.NET-Engine vereint native
> Multi-Block-Vererbung UND untrusted-Sandbox (Razor kann es, ist aber RCE;
> Fluid/Liquid nur Single-Slot-Layout). Die **einzige** in-process-.NET-Option mit
> nativem Twig-Block-Modell ist **Nunjucks über Jint** — und die vorhandenen
> Template-Bundles sind bereits `.njk` (Nunjucks). Damit entfällt der Eigenbau-
> Compiler; Baustein B reduziert sich auf den **confined Bundle-Loader** (Pfad-
> Scoping/Traversal-Schutz, bereits gebaut).
>
> **Sandbox (jetzt Jint statt Scriban):** kein CLR-Interop (JS erreicht keinen
> .NET-Typ), Wanduhr-**Timeout** + **Memory-Limit** + Recursion-/Statement-Limits
> (stärker gegen DoS als Scriban), Context nur als **JSON** übergeben, **frischer
> Engine pro Render** (keine Cross-Contamination). Nunjucks-`autoescape` schützt
> gegen XSS. Datei-/Include-Zugriff ausschließlich über den .NET-Loader-Callback,
> confined auf die Surface-Bundles.
>
> **Konsequenz:** Der native Vererbungs-Payoff (§9 „Datei-/Block-Override,
> `parent()`") kommt aus Nunjucks selbst — kein Scriban-AST-/Text-Preprocessing-
> Compiler. Trade-off: eine JS-Engine-Abhängigkeit + Render-Perf (Nunjucks-Neuparse
> pro Render → Engine-Pooling/Caching als Follow-up). Paket bleibt
> `Callora.Surface.Rendering` (isoliert). Der Text unten beschreibt die
> ursprüngliche Scriban-Entscheidung; obige Revision hat Vorrang.

* ADR-014 — Administration, Workspaces, Surfaces, Identitäten und Template-Komposition
* ADR-013 — Plugin-Trust-Modell
* ADR-012 — Ein-Core-Extensibility
* ADR-009 — Pluginverträge und interne Grenzen

---

# 1. Kontext

ADR-014 etabliert **Surfaces** und **Surface-Bundles** (Struktur-Achse mit
`extend`/`parent()`/Blocks **und** Datei-Override über die Bundle-Prioritätskette;
Token-Achse mit Kaskade und `locked`). Ausdrücklich offen blieben dort (§13, §15) die
**Rendering-Architektur** und die **konkrete Template-Engine**.

Randbedingungen:

* Callora ist **API-First** und **domänen-neutral** („eigenes Shopware/Symfony für .NET").
* Template-Bundles können von **Dritten** stammen (Marketplace) → nicht vertrauenswürdig (ADR-013).
* Es existiert bereits ein Auflösungs-Fundament (`IWorkspaceTemplateResolutionService`,
  `WorkspaceUiChainResolver`) — es löst die Prioritäts-/Zuordnungskette auf, rendert aber nichts.
* Es existiert **keine** Template-Engine im Projekt (Central Package Management leer).

---

# 2. Entscheidung (Überblick)

1. Rendering ist **geschichtet**: ein API-First-Kern (immer vorhanden) plus ein
   serverseitiger SSR-Layer (optional komponierbar). Schicht 1 steht nicht zur Debatte.
2. Der SSR-Layer rendert **voll serverseitig** (Shopware-Storefront-Modell): Template-Bundles
   mit `extend`/`parent()`/Blocks und Datei-Override.
3. **Paket-Split:** `Callora.Surface` (API, frontend-neutral) + `Callora.Surface.Rendering`
   (Engine + SurfaceShell + mitgelieferte Views).
4. Die „Engine" ist **zwei Bausteine**: **A** — eine gekaufte, sandboxed Base-Engine (Syntax/
   Rendering); **B** — ein eigenes View-Kompositions-Layer (Namespaces, Prioritätskette,
   Datei-Override, `extends-underlying`, Block-`parent()`).
5. **Base-Engine (A): Scriban** (schneller .NET-Template-Compiler), im gehärteten Sandbox-Modus,
   erweitert um Custom-Funktionen/AST-Layer und einen Bundle-aware `ITemplateLoader`.
6. **Kompositions-Layer (B)** baut auf dem vorhandenen Chain-Resolver auf — kein Wegwurf.

---

# 3. Geschichtete Architektur

```text
Schicht 1 — Surface-API          (immer, API-First-Kern, Callora.Surface)
   Domain→Surface-Auflösung, Access-Policy, Surface-Context, dedizierte Endpunkte
   → headless-Consumer (eigene PWA/SPA) hängen sich hier an

Schicht 2 — SSR-Template-Layer   (Callora.Surface.Rendering, serverseitig)
   SurfaceShell + Scriban + Kompositions-Layer (B) + Token-Injektion
   → konsumiert denselben Surface-Context/dieselben Contracts wie ein headless-Consumer,
     nur in-process (kein HTTP-Round-Trip) — wie Shopware Storefront ↔ Store-API
```

**Contract-Gleichheit:** Auch der eigene SSR-Renderer geht über dieselben Surface-Context-
und Daten-Contracts wie ein externes Frontend — kein privilegierter Sonderpfad. Die API
bleibt die single source of truth; SSR ist ein in-process-Consumer.

Eine **rein headless** Distribution komponiert `Callora.Surface.Rendering` nicht und zieht
damit weder die Engine-Abhängigkeit noch die SurfaceShell.

---

# 4. Paket-Struktur

| Paket | Inhalt | Frontend-/Engine-Abhängigkeit |
| --- | --- | --- |
| **`Callora.Surface`** (= heutiges `Callora.Workspace`, umbenannt) | Surface-Auflösung, Access-Policy, Surface-Context, Surface-API | **keine** (frontend-neutral, ADR-014 §11) |
| **`Callora.Surface.Rendering`** | SurfaceShell + Scriban (A) + Kompositions-Layer (B) + mitgelieferte Basis-Views (`Resources/Views/…`) | trägt die Engine — isoliert |
| **Template-Bundles** (Website/Portal/Dialer) | konkrete Struktur + Token-Defaults | Plugins (nicht Distributionskern), ADR-014 §9 |

Die Umbenennung `Callora.Workspace → Callora.Surface` folgt ADR-014 §14 (Workspace ist per
Definition kein Frontend; die tenant-facing Fläche ist die Surface-Runtime).

---

# 5. Der A/B-Schnitt

| | Baustein | Bau vs. Kauf |
| --- | --- | --- |
| **A** | Rendering-Engine: Variablen, Kontrollfluss, Includes, Escaping | **kaufen** (Scriban) |
| **B** | View-Komposition: Bundle-Namespaces, deterministische Prioritätskette, Datei-Override + `extends-underlying`, Block-`parent()` | **bauen** — kein .NET-Paket bringt Shopwares Modell mit |

Baustein **B** ist der Callora-spezifische Kern und existiert unabhängig von der gewählten
Engine. Sein schwierigster Teil — die Prioritäts-/Zuordnungskette — liegt bereits als
`IWorkspaceTemplateResolutionService`/`WorkspaceUiChainResolver` vor und wird zum
Surface-Template-Resolver ausgebaut. Es fehlen der Renderer (A) und der
Datei-/Block-Override-Compiler obendrauf.

---

# 6. Engine-Wahl: Scriban

Die Auswahl entscheidet sich an **Trust/Sandbox** und der Eignung als Basis für Baustein B —
weil Template-Bundles von Dritten kommen können (ADR-013):

| Engine | Sandbox | Block-Inheritance nativ | Bewertung |
| --- | --- | --- | --- |
| **Razor** (RazorLight/RCL) | **nein** — beliebiger C#-Code | nein (Sections) | **verworfen**: RCE-Risiko bei untrusted Templates. |
| **Fluid** | ja, secure by default | nein | Alternative: sandboxed by default, Liquid vertraut — aber weniger AST-Zugriff für Baustein B. |
| **Scriban** | ja (**bewusst zu härten**) | nein | **gewählt** |

**Warum Scriban:**

* **AST-/Compiler-freundlich** — Scriban stellt einen inspizier- und transformierbaren
  Syntaxbaum bereit. Baustein B (Datei-/Block-Override, `extends-underlying`, `parent()`) lässt
  sich als AST-Transformation/Custom-Functions sauber darauf aufsetzen, statt per
  Text-Preprocessing. Das ist der eigentliche Hebel, weil B der Eigenbau-Kern ist.
* **Performance** — einer der schnellsten .NET-Template-Compiler; relevant, weil pro
  Surface-Request eine komponierte Bundle-Kette gerendert wird.
* **`ITemplateLoader`** — kontrollierter Lade-Mechanismus für `include`/`import`; ideal für den
  Bundle-aware Loader, der die Prioritätskette durchläuft und den `extends-underlying`-Fall bedient.
* **Mächtige, moderne Syntax** (mit optionalem Liquid-Modus) — ausdrucksstärker als reines Liquid.

**Trade-off (bewusst akzeptiert):** Scriban ist **nicht** secure-by-default — es exponiert
Model-Member breit (Reflection). Der untrusted-Fall erfordert daher eine **aktiv gehärtete
Sandbox** (explizites `ScriptObject`-Allowlist-Model, `MemberFilter`, Loop-/Recursive-Limits,
kein .NET-Typ-Durchgriff, §8). Das ist die eine Zusatzpflicht gegenüber Fluid — machbar, aber
ein sicherheitsrelevanter Baustein, der sorgfältig getestet gehört.

Fachlogik gehört weiterhin in Feature-Plugins/Contracts, nicht ins Template.

---

# 7. Rendering-Pipeline

```text
HTTP-Request (Hostname + Pfad)
  → Surface-Auflösung            (Callora.Surface: Domain → Surface, Tenant, Workspace)
  → Access-Policy                (Public / Authenticated / Mixed; Auth-Realm; §ADR-014 6)
  → Surface-Context aufbauen     (Identity, Membership, Profile, Permissions, Locale, Tokens)
  → Template-Chain auflösen      (B: Bundle-Prioritätskette; Surface→Workspace→Distribution)
  → kompilieren + cachen         (A+B: Datei-/Block-Override zu einer effektiven View verweben)
  → rendern                      (Scriban, Surface-Context als allowlisted ScriptObject)
  → HTML
```

**Cache-Key** mindestens aus: Tenant, Workspace, Surface, Template-Chain, Plugin-Versionen,
Konfiguration, Locale. **Invalidierung** bei Plugin-Aktivierung/-Deaktivierung/-Update
(ADR-014 Phase I).

---

# 8. Trust und Sandbox

Scriban führt keinen beliebigen Code aus, exponiert Model-Member standardmäßig aber breit
(Reflection). Für untrusted Bundles wird die Sandbox daher **bewusst gehärtet**:

* Model **nicht** als beliebiges POCO übergeben, sondern als explizit gebautes `ScriptObject`
  (Allowlist der sichtbaren Werte); zusätzlich `MemberFilter`, kein Relaxed Member Access, kein
  Durchgriff auf .NET-Typen/Reflection.
* **Ressourcenschutz:** `LoopLimit`, `RecursiveLimit`, Render-Timeout, Ausgabe-Grössenlimit,
  kontrollierter `ITemplateLoader` mit Tiefenlimit — gegen Template-DoS.
* Template-Bundles aus `custom/plugins` laufen im ADR-013-Trust-Modell; das Rendering fügt
  **keine** zusätzliche Vertrauensannahme hinzu.
* Zyklen in `extends`/`parent()` bzw. der Datei-Kette → **Kompilierungsfehler**, kein
  Laufzeit-Hang (ADR-014 §9.6).

---

# 9. Ist-Stand und Wiederverwendung

| Ist-Stand (heute) | Rolle in dieser ADR |
| --- | --- |
| `IWorkspaceTemplateResolutionService`, `WorkspaceUiChainResolver` | Fundament von Baustein B (Prioritäts-/Zuordnungskette); Ausbau zum Surface-Template-Resolver |
| `WorkspaceTemplateEffectiveApiResponse` | wird zum effektiven Surface-Template-Snapshot |
| `Callora.Workspace` | → `Callora.Surface` (API-Kern, Schicht 1) |
| — (keine Engine) | neu: `Callora.Surface.Rendering` mit Scriban (A) + Kompositions-Layer (B) |

---

# 10. Konsequenzen

## 10.1 Positiv

* Ein Modell bedient **beide** Welten: API-First/headless **und** mitgeliefertes,
  erweiterbares SSR-Template-System.
* HTML-/SEO-first von Haus aus (öffentliche Websites, Landingpages).
* Untrusted Template-Bundles sind durch die gehärtete Sandbox tragbar.
* Der teure Baustein (B) sitzt isoliert im Rendering-Paket; der API-Kern bleibt engine-frei.
* Der vorhandene Chain-Resolver wird wiederverwendet, nicht ersetzt.

## 10.2 Negativ

* Baustein B (Datei-/Block-Override-Compiler) ist echter Eigenbau — der aufwändigste Teil.
* Twig-artige serverseitige Vererbung hat in .NET kein etabliertes Vorbild; die Custom-Funktionen
  auf Scriban müssen sorgfältig spezifiziert und getestet werden.
* Cache-Korrektheit über Tenant×Workspace×Surface×Version ist nicht-trivial.
* Scribans Sandbox ist nicht secure-by-default — sie muss bewusst gehärtet werden (§8); ein Fehler dort ist ein Sicherheitsrisiko.
* Assets (SCSS/JS der Bundles) brauchen eine eigene Compile-/Manifest-Pipeline (siehe §11).

---

# 11. Nicht entschieden / offen

* Konkrete Custom-Funktions-/AST-Syntax für das `sw_extends`-Äquivalent, `block` und `parent()` auf Scriban.
* Cache-Backend (In-Memory vs. verteilt) und Warmup-Strategie.
* **Asset-Pipeline** der Bundles (SCSS→CSS, JS-Bundling, Token→CSS-Custom-Properties) — eigener
  Entscheidungspunkt, evtl. eigene ADR.
* Optionales Prerendering für SPA-Islands innerhalb serverseitiger Surfaces.
* Verhältnis zur offiziellen Administration (deren SPA-/Vue-Extension-Point-Weg bleibt getrennt
  vom Surface-SSR, ADR-014 §10.3).

---

# 12. TODOs

* [ ] `Callora.Surface` aus `Callora.Workspace` herausschneiden/umbenennen (Schicht 1).
* [ ] `Callora.Surface.Rendering`-Paket anlegen (Sdk.Web-Library, referenziert Core + Surface).
* [ ] Scriban als PackageReference (Central Package Management) aufnehmen.
* [ ] Bundle-aware `ITemplateLoader` implementieren (Prioritätskette, Datei-Override, `extends-underlying`).
* [ ] Custom-Funktionen/AST-Layer: `block`/`sw_extends`-Äquivalent/`parent()` auf Scriban.
* [ ] Surface-Template-Resolver aus `IWorkspaceTemplateResolutionService` ausbauen.
* [ ] Render-Pipeline (§7) inkl. Surface-Context-Aufbau.
* [ ] Cache + deterministische Invalidierung (§7).
* [ ] Sandbox-Härtung: ScriptObject-Allowlist, MemberFilter, Loop/Recursive-Limits, Timeouts, Zyklen-Erkennung (§8).
* [ ] SurfaceShell-Basis-Views + Referenz-Template-Bundle (ein SPA-Root, ein Document-Bundle).
* [ ] Contract-Tests: Datei-/Block-Override, `parent()`, Prioritätsreihenfolge, Zyklen-Fehler.
* [ ] ADR-014 §13/§15 auf diese Entscheidung verweisen.
