# ADR-014 — Administration, Workspaces, Surfaces, Identitäten und Template-Komposition

**Status:** Accepted
**Datum:** 2026-07-16
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* `CALLORA_ZIELARCHITEKTUR_DOMAENENNEUTRALE_PLUGIN_PLATTFORM_REV2`
* ADR-009 — Pluginverträge und interne Grenzen
* ADR-012 — Ein-Core-Extensibility
* ADR-013 — Plugin-Trust-Modell

---

## 1. Kontext

Callora wird als domänenneutrale Plugin-Plattform entwickelt. Die offizielle Distribution bleibt zunächst auf programmierbare Kommunikation ausgerichtet, die Plattform soll jedoch auch andere Anwendungen und digitale Oberflächen unterstützen.

Insbesondere müssen folgende Szenarien abbildbar sein:

* Callora-Plattformadministration
* Tenant-Administration
* Workspace-Administration
* Dialer und Agent Desktop
* Kunden- und Partnerportale
* öffentliche Websites
* eingebettete Widgets
* weitere durch Plugins definierte Anwendungen

Die bisherige Bezeichnung `Workspace` darf dabei nicht gleichzeitig für folgende unterschiedliche Konzepte stehen:

* fachliche und technische Umgebung,
* Administrationsoberfläche,
* Benutzerfrontend,
* Website oder Anwendung,
* Login-Kontext.

Ebenso soll Callora nicht auf feste Frontendtypen wie Dialer, Website oder Portal beschränkt werden.

Für die Darstellung soll ein Shopware-ähnliches Modell verwendet werden:

* minimales Basistemplate,
* Template-Vererbung,
* Blocks,
* `extend`,
* `parent()`,
* Template-Plugins,
* workspace- oder surfacebezogene Zuweisung.

Die konkrete Frontendtechnologie darf dabei nicht Bestandteil des stabilen Core-Vertrags werden.

---

# 2. Entscheidung

Callora unterscheidet zukünftig ausdrücklich zwischen:

```text
Administration
Workspace
Surface
Identity / Principal
Surface-Bundle (Template + Theme)
Feature-Plugin
```

Die Begriffe besitzen jeweils eine klar abgegrenzte Verantwortung. `Template` und
`Theme` sind dabei kein getrenntes Begriffspaar mehr, sondern zwei Achsen desselben
Surface-Bundles (§9/§10): Struktur (Blocks) und Tokens (Branding) — ein Plugin-Typ,
ein Manifest, ein Lifecycle.

---

# 3. Administration

## 3.1 Definition

`Callora.Administration` ist die zentrale Control Plane der Callora-Installation.

Sie ist keine separate Anwendung pro Benutzertyp. Stattdessen existieren:

* eine Administration,
* ein Admin-Login,
* eine Admin-Shell,
* ein gemeinsames Identity-System,
* rollen-, permission- und scopeabhängige Ansichten.

## 3.2 Administratorgruppen

Die Administration unterstützt mindestens folgende administrative Ebenen:

### Callora SuperAdmin

Kann plattformweit verwalten:

* alle Tenants,
* alle Workspaces,
* Plattformbenutzer,
* globale Plugins,
* Pluginpakete und Signer,
* Entitlements und Lizenzen,
* globale Jobs,
* Plattformkonfiguration,
* Monitoring und Operations,
* systemweite Integrationen.

### Tenant Admin

Kann innerhalb eines oder mehrerer zugewiesener Tenants verwalten:

* Tenant-Einstellungen,
* Tenant-Benutzer,
* Tenant-Rollen,
* Tenant-Entitlements,
* Tenant-Integrationen,
* Workspaces des Tenants,
* workspaceübergreifende Tenant-Daten.

### Workspace Admin

Kann ausschließlich zugewiesene Workspaces verwalten:

* Workspace-Einstellungen,
* Workspace-Mitglieder,
* Rollen und Permissions,
* Plugins und Capabilities,
* Plugin-Konfiguration,
* Surfaces,
* Flows,
* Webhooks,
* Medien,
* Custom Fields,
* fachliche Pluginressourcen.

## 3.3 Gemeinsamer Admin-Login

Alle administrativen Rollen verwenden denselben Admin-Login.

Nach erfolgreicher Authentifizierung wird der effektive Administrationskontext ermittelt:

```text
Identity
+ Rollen
+ Permissions
+ Tenant Scopes
+ Workspace Scopes
+ gegebenenfalls Surface Scopes
```

Es wird nicht ausschließlich eine globale Rolle geprüft.

Beispiel:

```json
{
  "identityId": "identity-123",
  "roles": [
    "tenant-admin",
    "workspace-admin"
  ],
  "tenantScopes": [
    "tenant-acme"
  ],
  "workspaceScopes": [
    "sales-de",
    "support-de"
  ]
}
```

## 3.4 Autorisierung

Die Admin-Shell darf Navigation und Funktionen anhand der effektiven Permissions ausblenden.

Die tatsächliche Zugriffskontrolle erfolgt jedoch immer serverseitig.

Jede administrative Operation prüft mindestens:

```text
Identity
+ Permission
+ Resource Scope
+ Tenant-Zugehörigkeit
+ Workspace-Zugehörigkeit
```

UI-Ausblendung ist keine Sicherheitsgrenze.

## 3.5 Rollen und Permissions

Rollen sind Sammlungen feingranularer Permissions.

Beispiel:

```text
Rolle: workspace-admin

Permissions:
- workspace.read
- workspace.settings.write
- workspace.members.manage
- workspace.plugins.manage
- workspace.surfaces.manage
- workspace.flows.manage
```

Ein Benutzer kann mehrere Rollen und unterschiedliche Berechtigungen in verschiedenen Scopes besitzen.

Es gibt keine zwingende Auswahl einer einzigen „höchsten Rolle“.

## 3.6 Administrations-Sicherheitskontext

Die Administration verwendet einen eigenen Authentifizierungs- und Session-Kontext.

Dieser soll von normalen Surface-Sessions getrennt bleiben.

Mögliche Trennung:

* eigene OIDC-Audience,
* eigenes Cookie,
* eigene Session Policy,
* verpflichtende MFA,
* separate Login- und Callback-Routen,
* strengere Timeout-Regeln.

Eine Anmeldung an einer Kunden-, Website- oder Dialer-Surface erzeugt nicht automatisch eine gültige Administrationssession.

---

# 4. Workspace

## 4.1 Definition

Ein Workspace ist eine tenantgebundene fachliche und technische Umgebung.

Ein Workspace ist:

* kein Administrationsfrontend,
* kein konkretes Benutzerfrontend,
* kein Login,
* kein Template,
* keine Website.

Er ist der Kontext, in dem Plugins, Daten, Mitglieder, Konfiguration und Surfaces zusammengeführt werden.

## 4.2 Inhalt eines Workspaces

Ein Workspace besitzt beziehungsweise referenziert mindestens:

```text
Workspace
├── Tenant
├── Status
├── Mitglieder
├── Rollen und Permissions
├── aktivierte Plugins
├── Capabilities
├── Entitlements
├── Konfiguration
├── Daten
├── Events und Flows
├── Jobs
├── Integrationen
├── Kommunikationsressourcen
├── Theme- und Template-Defaults
└── Surfaces
```

## 4.3 Verwaltung

Der Workspace „verwaltet“ nicht selbst.

Die Verwaltung erfolgt über den workspacebezogenen Bereich von `Callora.Administration`.

Die Begriffe werden deshalb getrennt verwendet:

```text
Workspace
= die Umgebung

Workspace Administration
= Administrationsansicht für diese Umgebung

Workspace Surface
= konkrete Nutzungsoberfläche dieser Umgebung
```

## 4.4 Mehrere Workspaces pro Tenant

Ein Tenant kann mehrere Workspaces besitzen.

Beispiel:

```text
Tenant „Muster GmbH“
├── Workspace „Corporate Website“
├── Workspace „Inside Sales“
├── Workspace „Customer Support“
└── Workspace „Partner Operations“
```

Workspaces dürfen unabhängig konfiguriert, berechtigt und lizenziert werden.

---

# 5. Surface

## 5.1 Definition

> **Abgelöst durch ADR-019.** Surfaces bilden seither einen **Baum**. Was hier beschrieben
> ist, gilt weiterhin für eine **Anwendungswurzel** — einen Knoten ohne Elternteil. Ein
> Kind-Knoten erbt Host, Zugriffspolitik, Theme und Locale von ihr und überschreibt nur,
> was es eigenes braucht; er entspricht dem, was Shopware eine Kategorie nennt. Der Grund:
> Es gab genau ein Layout je Surface, also hätte eine Website mit drei Seiten drei
> Zugangsflächen gebraucht.

Eine Surface ist eine konkrete Nutzungs-, Zugangs- oder Ausgabefläche innerhalb eines Workspaces.

Eine Surface kann beispielsweise sein:

* Website,
* Dialer,
* Agent Desktop,
* Kundenportal,
* Partnerportal,
* Supervisor Dashboard,
* Embedded Widget,
* öffentliche API,
* durch Plugins definierte Anwendung.

Eine Surface ist kein Benutzertyp.

## 5.2 Verantwortung

Eine Surface definiert mindestens:

* Workspace-Zugehörigkeit,
* technischen Schlüssel,
* Surface-Typ,
* Domain oder Basisroute,
* Entry Route,
* Zugriffspolitik,
* Authentication Realm,
* erlaubte Audience,
* Template-Zuweisung,
* Theme-Zuweisung,
* Navigation,
* Locale beziehungsweise Sprache,
* surfacebezogene Konfiguration.

Beispiel:

```json
{
  "key": "sales-dialer",
  "type": "dialer",
  "domain": "dialer.example.de",
  "entryRoute": "/campaigns",
  "accessMode": "authenticated",
  "audience": [
    "employee",
    "agent"
  ],
  "authenticationRealm": "internal-workforce",
  "templatePluginId": "callora.template.dialer",
  "themePluginId": "customer.theme.internal"
}
```

## 5.3 Mehrere Surfaces pro Workspace

> **Abgelöst durch ADR-019.** Die Liste unten beschreibt die **Wurzeln** eines Workspaces —
> die getrennten Anwendungen auf gemeinsamen Daten. Unter jeder von ihnen hängt seither ein
> Baum von Kind-Surfaces, aus dem die Navigation entsteht und in dem jeder Knoten eine
> eigene Erlebniswelt tragen kann. Die Aufzählung der Unterschiede (Domains, Templates,
> Themes, Zielgruppen, Loginverfahren) gilt weiterhin — für Wurzeln.

Ein Workspace kann mehrere Surfaces besitzen.

Beispiel:

```text
Workspace „Customer Operations“
├── öffentliche Website
├── Kundenportal
├── Agent Desktop
├── Dialer
└── Supervisor Dashboard
```

Die Surfaces können auf gemeinsame Workspace-Daten zugreifen, besitzen aber unterschiedliche:

* Domains,
* Templates,
* Themes,
* Zielgruppen,
* Loginverfahren,
* Berechtigungen,
* Navigationsstrukturen.

---

# 6. Surface-Zugriff und Login

## 6.1 Access Modes

Jede Surface besitzt eine explizite Zugriffspolitik.

Mindestens folgende Modi werden unterstützt:

```csharp
public enum SurfaceAccessMode
{
    Public,
    Authenticated,
    Mixed
}
```

### Public

Die Surface ist ohne Anmeldung erreichbar.

Beispiele:

* Unternehmenswebsite,
* Landingpage,
* öffentliche Dokumentation,
* Rückruf-Widget.

### Authenticated

Die Surface erfordert eine gültige Anmeldung.

Beispiele:

* Dialer,
* Agent Desktop,
* internes Portal,
* Supervisor Dashboard.

### Mixed

Die Surface besitzt sowohl öffentliche als auch geschützte Routen.

Beispiele:

* Website mit Kundenkonto,
* öffentliches Portal mit Partnerbereich,
* Dokumentationsseite mit geschützten Downloads.

## 6.2 Surface-Login

> **Für Surfaces abgelöst durch ADR-017.** Die fachliche Identität eines
> Surface-Besuchers gehört seit ADR-017 dem Plugin, dem die Daten gehören; der Core
> transportiert sie nur (`IHostSurfaceIdentityProvider`, Operator-Zuweisung pro
> Surface, Gast-Kontext + optionale Identität). Die Access-Modes aus §6.1 gelten
> unverändert weiter. Für die Backend-/Admin-Anmeldung bleibt dieser Abschnitt gültig.


Geschützte und gemischte Surfaces verwenden einen surfacebezogenen Authentifizierungsfluss.

Die Surface bestimmt:

* verwendbares Authentication Scheme,
* Authentication Realm,
* Loginroute,
* Logoutroute,
* Callbackroute,
* Return Route,
* MFA-Anforderung,
* Audience.

Die eigentliche Authentifizierungslogik bleibt unter Kontrolle des Core beziehungsweise eines ausdrücklich freigegebenen Authentifizierungsmechanismus.

Das Template rendert nur die Oberfläche.

## 6.3 Audiences und Principal-Typen

Eine Surface kann für eine oder mehrere Audiences vorgesehen sein.

Beispiele:

```text
Website
→ Anonymous, Customer

Kundenportal
→ Customer

Dialer
→ Employee, Agent

Supervisor Dashboard
→ Employee, Supervisor

Partnerportal
→ Partner
```

`Customer`, `Employee`, `Agent`, `Supervisor` und `Partner` sind keine Surface-Typen.

Sie sind Principal- beziehungsweise Profilrollen innerhalb eines Identity-Kontexts.

## 6.4 Identity und Profile

> **Für Surfaces abgelöst durch ADR-017.** Ein Identity-/Profilmodell im Core
> widerspricht der Domänen-Neutralität (ADR-012). Surface-Identität ist stattdessen
> `Issuer + SubjectId` plus namespaced Claims, ausgestellt vom zuständigen Plugin.


Die Identität einer Person und ihre fachlichen Profile werden getrennt.

```text
Identity
├── Credentials
├── Tenant Memberships
├── Workspace Memberships
└── Principal Profiles
    ├── Employee Profile
    ├── Agent Profile
    ├── Customer Profile
    ├── Partner Profile
    └── weitere Plugin-Profile
```

Eine Identity kann mehrere Profile besitzen.

Beispiel:

```text
Identity „max@example.de“
├── Employee im Tenant
├── Agent im Workspace „Sales“
└── Customer in einem anderen Workspace
```

Die Authentifizierung beantwortet:

> Wer ist die Person?

Die Autorisierung beantwortet:

> Darf diese Person diese Surface, Route oder Funktion verwenden?

---

# 7. Scope-Modell

Callora unterscheidet mindestens folgende Scopes:

```text
Platform Scope
└── Tenant Scope
    └── Workspace Scope
        └── Surface Scope
```

Plugins müssen für ihre Daten und Ressourcen einen geeigneten Scope festlegen.

Beispiele:

| Ressource                 | Typischer Scope        |
| ------------------------- | ---------------------- |
| Plattformkonfiguration    | Platform               |
| Tenant-Abonnement         | Tenant                 |
| gemeinsamer Kundenkontakt | Tenant oder Workspace  |
| Dialer-Kampagne           | Workspace              |
| SIP-Konto                 | Workspace              |
| Website-Seite             | Workspace oder Surface |
| Domain                    | Surface                |
| Surface-Theme             | Surface                |
| Pluginlizenz              | Tenant oder Workspace  |

Der konkrete Scope wird nicht pauschal vom Core erzwungen, sondern durch den jeweiligen fachlichen Vertrag des Plugins definiert.

---

# 8. SurfaceShell

## 8.1 Ein einziges minimales Basistemplate

Callora stellt genau ein minimales Root-Template bereit:

```text
SurfaceShell
```

Es werden keine festen Core-Templates wie `website`, `portal`, `dialer` oder `application` vorgeschrieben.

Diese konkreten Oberflächen entstehen durch Template-Plugins.

## 8.2 Verantwortung der SurfaceShell

Die SurfaceShell enthält ausschließlich die strukturell und technisch notwendigen Root-Bereiche.

Beispiel:

```text
document
├── head
├── body
│   ├── surface_before
│   ├── surface
│   │   └── surface_content
│   └── surface_after
└── scripts
```

Sinngemäße Template-Struktur:

```twig
<!doctype html>
<html>
<head>
    {% block surface_head %}
        {% block surface_metadata %}{% endblock %}
        {% block surface_styles %}{% endblock %}
    {% endblock %}
</head>
<body>
    {% block surface_body %}
        {% block surface_before %}{% endblock %}

        {% block surface %}
            {% block surface_content %}{% endblock %}
        {% endblock %}

        {% block surface_after %}{% endblock %}
    {% endblock %}

    {% block surface_scripts %}{% endblock %}
</body>
</html>
```

Die SurfaceShell enthält ausdrücklich keine feste:

* Navigation,
* Sidebar,
* Website-Struktur,
* Dialer-Struktur,
* Headerstruktur,
* Footerstruktur,
* CMS-Logik,
* Fachfunktion.

---

# 9. Surface-Bundle — Struktur-Achse (Templates)

Dieser Abschnitt beschreibt die **Struktur-Achse** eines Surface-Bundles (Blocks,
Vererbung). Die **Token-Achse** (Theme/Branding) und die gemeinsame Bundle-Semantik
— ein Plugin-Typ, ein Manifest, ein Lifecycle — stehen in §10.

## 9.1 Definition

Ein Template-Plugin erbt von der SurfaceShell oder einem anderen Template-Plugin und definiert die konkrete strukturelle Oberfläche.

Beispiele:

```text
SurfaceShell
├── Callora.Template.Website
│   └── Customer.Template.Corporate
│
├── Callora.Template.Dialer
│   └── Customer.Template.Sales
│
└── Callora.Template.Portal
```

Die Typen `Website`, `Dialer` oder `Portal` werden somit nicht im Core fest verdrahtet.

## 9.2 Vererbung und Override

Die Struktur-Achse kennt **zwei** Override-Ebenen — beide analog Shopware:

**Datei-Ebene (über die Bundle-Prioritätskette):** Ein Bundle kann eine View-Datei
desselben logischen Pfads der darunterliegenden Bundles entweder **komplett ersetzen**
(keine Vererbung — die Datei wird ohne `extend` neu geschrieben) oder von der
nächst-niedrigeren Version **erben und erweitern** (`extend`). Die Auflösung erfolgt
über Namespace + deterministische Bundle-Reihenfolge (analog Shopware `sw_extends` /
`TemplateFinder`). `base.html`/`shell.html` kann so je nach Bundle vollständig ersetzt
**oder** inkrementell erweitert werden.

**Block-Ebene (innerhalb einer Datei):** Template-Plugins unterstützen:

* `extend`,
* Blocks,
* `parent()`,
* mehrstufige Vererbung,
* deterministische Erweiterungsreihenfolge,
* explizite Parent-Abhängigkeiten.

Beispiel:

```twig
{% extend "@SurfaceShell/shell.html" %}

{% block surface %}
    <header>
        {% block layout_header %}{% endblock %}
    </header>

    <nav>
        {% block layout_navigation %}{% endblock %}
    </nav>

    <main>
        {% block surface_content %}{% endblock %}
    </main>

    <footer>
        {% block layout_footer %}{% endblock %}
    </footer>
{% endblock %}
```

Ein erbendes Template:

```twig
{% extend "@CalloraTemplateWebsite/layout.html" %}

{% block layout_header %}
    {{ parent() }}

    {% block customer_branding %}{% endblock %}
{% endblock %}
```

## 9.3 Zuweisung

Das aktive Template-Plugin wird konfiguriert über:

```text
Surface Template
→ überschreibt Workspace Default Template
→ überschreibt Distribution Default Template
```

Auflösung:

```text
Surface besitzt Template?
├── Ja → Surface-Template
└── Nein
    └── Workspace besitzt Default-Template?
        ├── Ja → Workspace-Template
        └── Nein → Distribution-Default
```

## 9.4 Template-Manifest

Template-Plugins deklarieren mindestens:

* Plugin-ID,
* Version,
* Parent-Template,
* unterstützte Surface-Typen,
* View-Pfade,
* Style-Pfade,
* Script-Pfade,
* Asset-Pfade,
* öffentliche Blocks,
* Template-Konfiguration.

Beispiel:

```json
{
  "id": "customer.template.corporate",
  "type": "surface-template",
  "version": "1.0.0",
  "extends": "callora.template.website",
  "supportsSurfaceTypes": [
    "website",
    "portal"
  ],
  "publicBlocks": [
    "layout.header",
    "layout.navigation",
    "surface.content.before",
    "surface.content",
    "surface.content.after",
    "layout.footer"
  ]
}
```

## 9.5 Öffentliche Blocks

Öffentliche Blocks sind Teil des stabilen Template-Plugin-Vertrags.

Template-Plugins müssen deklarieren, welche Blocks von Feature- oder Kundentemplates stabil erweitert werden dürfen.

Interne Blocks dürfen existieren, gelten aber nicht automatisch als stabil.

## 9.6 Konfliktauflösung

Wenn mehrere Plugins denselben Block erweitern, muss die Reihenfolge deterministisch sein.

Reihenfolge:

1. Dependency-Reihenfolge,
2. Template-Vererbung,
3. Plugin-Tier,
4. explizite Priorität,
5. Plugin-ID als stabiler Tie-Breaker.

Zyklen in der Template-Vererbung führen zu einem Aktivierungs- beziehungsweise Kompilierungsfehler.

---

# 10. Surface-Bundle — Token-Achse (Theme) und Auflösung

Template und Theme sind **keine getrennten Plugin-Typen**, sondern zwei Achsen
desselben Artefakts — analog zum Shopware-Theme, das Twig-Block-Overrides (Struktur)
und `theme.json`/SCSS-Variablen (Branding) in **einem** Bundle mit **einem** Manifest
und **einem** Lifecycle bündelt.

```text
Surface-Bundle
├── Struktur-Achse (Template)   → Blocks, extend, parent()   (§9)
└── Token-Achse (Theme)         → Design Tokens, CSS Custom Properties, Branding
```

Ein Bundle kann eine oder beide Achsen beitragen (Farben, Typografie, Abstände,
Design Tokens, Logos, Icons, Fonts, Component Variants, Branding auf der Token-Seite).
Der Grund, die Achsen dennoch **getrennt auflösbar** zu halten, ist Multi-Tenant-
kritisch: dieselbe Struktur muss mit unterschiedlichem Branding wiederverwendbar sein,
**ohne** das Template zu forken. Ein „Portal"-Gerüst wird von vielen Tenants/Surfaces
genutzt — jeweils mit eigenem Logo/Farben, aber gemeinsamer, weiter erbbarer Struktur.

## 10.1 Token-Kaskade

Tokens werden über eine deterministische Kaskade aufgelöst:

```text
Distribution-Default
→ Tenant            (White-Label des Tenants)
→ Workspace-Default (Default für die Surfaces des Workspaces)
→ Surface           (konkrete Zuweisung — wie ein Shopware-Theme am SalesChannel)
```

Die Struktur-Achse (Template-Zuweisung) wird dagegen nur auf **Surface**-Ebene gewählt
(mit Workspace-/Distribution-Default als Fallback, §9.3). Ein Workspace hat kein
eigenes Frontend — er liefert nur Token-Defaults für seine Surfaces.

## 10.2 Locked-Semantik (White-Label-Governance)

Jede Token-Gruppe kann von einer höheren Ebene als `locked` markiert werden. Gesperrte
Tokens dürfen von tieferen Ebenen **nicht** überschrieben werden.

Damit trägt die Kaskade den Reseller-/Agentur-Fall: Ein Tenant (z. B. eine Agentur)
kann sein Corporate-Branding (Logo, Primärfarbe) für alle Workspaces und Surfaces
**erzwingen**, während er andere Tokens (z. B. Akzentfarben) zur freien Anpassung offen
lässt. Ohne `locked` ist jede Ebene frei überschreibbar (Default).

## 10.3 Achsen je Oberflächen-Typ

Administration und Surface nutzen **unterschiedliche** Erweiterbarkeits-Modelle — sie
werden bewusst nicht in dieselbe Template-Maschinerie gezwungen:

| Fläche         | Struktur-Achse                                  | Token-Achse             | Plugin-Erweiterung                                  |
| -------------- | ----------------------------------------------- | ----------------------- | --------------------------------------------------- |
| **Admin-Shell** | **fix** (Distribution, kein Austausch)          | pro **Tenant** (White-Label) | JS-/Vue-Extension-Points (Navigation, Routen, Seiten, Widgets) |
| **Surface**     | **variabel** (Surface-Bundle: Blocks, `extend`, `parent()`) | pro **Surface**         | Feature-Plugins erweitern öffentliche Blocks         |

Konsequenz: Der aufwändige Multi-Inheritance-Template-Compiler (Phase I) wird
**ausschließlich für Surfaces** benötigt. Die Administration braucht ihn nie — sie ist
eine feste Shell mit Extension-Points plus Token-Theming. Die beiden schwierigen
Bausteine sind damit entkoppelt: die Admin-Shell kann fertiggestellt werden, bevor der
Surface-Template-Compiler steht.

## 10.4 Ausblick: Page-Builder (späteres, separates Plugin)

Statische Struktur (Bundle-Blocks) und dynamische Inhaltskomposition sind getrennt.
Zunächst bringen Plugins Blöcke/Elemente mit, die **direkt in das Template integriert**
werden (statisch, zur Aktivierungs- bzw. Kompilierungszeit).

Ein späterer **Page-Builder** (analog Shopware „Erlebniswelten") wird als **eigenes
Plugin** ergänzt und erlaubt die dynamische, redaktionelle Komposition von
Elementen/Blöcken zur Laufzeit. Die Bundle- und Block-Verträge dieser ADR sind die
Grundlage; der Page-Builder ist kein Bestandteil des Basis-Mechanismus.

---

# 11. Frontendtechnologie

## 11.1 Frameworkneutraler Host

`Callora.Core`, `Callora.Administration` und `Callora.Workspace` dürfen keine harte Abhängigkeit zu Vue, React, Nuxt oder einer anderen konkreten Frontendtechnologie erhalten.

Der Host stellt bereit:

* APIs,
* Authentifizierung,
* Autorisierung,
* Surface-Auflösung,
* Template-Auflösung,
* Asset-Manifeste,
* Extension Points,
* Renderingverträge.

## 11.2 Template-Plugin-Freiheit

Ein Template-Plugin kann intern verwenden:

* serverseitige Templates,
* Vue,
* React,
* Web Components,
* Vanilla JavaScript,
* eine externe SPA,
* einen rein serverseitigen Renderer.

Ein Template kann beispielsweise lediglich einen SPA-Root bereitstellen:

```twig
{% extend "@SurfaceShell/shell.html" %}

{% block surface %}
    <div
        id="callora-app"
        data-workspace="{{ workspace.key }}"
        data-surface="{{ surface.key }}">
    </div>
{% endblock %}
```

Die konkrete Frontendtechnologie ist Implementierungsdetail des Template-Plugins.

## 11.3 Administration

Die offizielle Administration darf intern eine opinionierte Frontendtechnologie verwenden, beispielsweise Vue.

Diese Technologie wird jedoch nicht automatisch Teil des öffentlichen Pluginvertrags.

Administrationsplugins sollen bevorzugt über definierte Beiträge integrieren:

* Navigation,
* Routen,
* Seiten,
* Actions,
* Formulare,
* Widgets,
* Asset-Manifeste.

---

# 12. Konsequenzen

## 12.1 Positive Konsequenzen

* Eine gemeinsame Administration genügt für alle Administratorrollen.
* Autorisierung bleibt rollen-, permission- und scopebasiert.
* Workspaces bleiben fachlich und technisch klar abgegrenzt.
* Ein Workspace kann mehrere völlig unterschiedliche Frontends besitzen.
* Website, Dialer und Portal werden nicht im Core fest verdrahtet.
* Surface-Logins können unterschiedliche Zielgruppen und Identity Provider verwenden.
* Template-Plugins können Shopware-artig erben und erweitern.
* Template und Theme sind ein Bundle (ein Manifest, ein Lifecycle), aber zwei unabhängig auflösbare Achsen: Struktur wiederverwendbar, Branding pro Surface/Tenant ohne Template-Fork.
* Administration und Surface bleiben in unterschiedlichen Erweiterbarkeits-Modellen (feste Shell + Extension-Points vs. Template-Vererbung); der teure Template-Compiler wird nur für Surfaces gebraucht.
* Der Host bleibt frontendtechnologisch neutral.
* Callora kann später als Website-, Portal-, Communication- oder Application-Plattform verwendet werden.
* Feature-Plugins können verschiedene Template-Plugins erweitern.

## 12.2 Negative Konsequenzen

* Das Scope- und Berechtigungsmodell wird komplexer.
* Surface-Auflösung muss Domain, Workspace, Template und Access Policy kombinieren.
* Template-Blocks werden zu versionierten öffentlichen Verträgen.
* Multi-Inheritance erfordert einen eigenen Resolver und Compiler.
* Asset-Reihenfolgen und Cache-Invalidierung müssen deterministisch sein.
* Surface- und Administrationssessions müssen getrennt behandelt werden.
* Plugin-Kompatibilität muss auch Template- und Blockverträge berücksichtigen.

---

# 13. Nicht entschieden

Folgende Punkte werden bewusst noch nicht abschließend festgelegt:

* konkrete Template-Engine,
* serverseitiges versus clientseitiges Rendering,
* konkreter Vue-/React-Einsatz für offizielle Surfaces,
* genauer Name und Aufbau des Template-Manifests,
* vollständiges CMS- und Page-Builder-Modell,
* konkrete Implementierung der Identity-Profile,
* konkrete OIDC-, SAML-, Passkey- und Magic-Link-Provider,
* genauer Cache- und Compilation-Mechanismus,
* konkrete Theme-Compiler-Implementierung.

Die **Template-Engine-Entscheidung** (erster Punkt) ist in **ADR-015 —
Surface-Rendering-Architektur und Template-Engine** getroffen: geschichtete Architektur
(API-First-Kern + serverseitiger SSR-Layer), Paket-Split `Callora.Surface` /
`Callora.Surface.Rendering`, Engine **Scriban** (gehärtete Sandbox) plus ein eigenes
View-Kompositions-Layer, das auf dem vorhandenen `IWorkspaceTemplateResolutionService`
aufsetzt. Die übrigen Punkte dieser Liste bleiben offen.

---

# 14. Ist-Stand und Migration

Callora hat den Modul-Schnitt dieser ADR bereits teilweise umgesetzt (REV2 §4). Die ADR
baut darauf auf; nichts davon ist Wegwurf:

| Ist-Stand (heute) | Rolle nach dieser ADR |
| --- | --- |
| `Callora.Administration` (Operator-`/api/*`, inkl. `/api/workspaces`) | bleibt die Control Plane (§3); Workspace-Verwaltung ist ihr workspacebezogener Bereich (§4.3) |
| `Callora.Workspace` (heute: WorkspacePublic + WorkspaceTheme) | wird zur **Surface-Runtime**; voraussichtlich Umbenennung nach `Callora.Surface`, da „Workspace" per Definition kein Frontend ist (§4.1) |
| `IWorkspaceTemplateResolutionService`, `WorkspaceTemplateEffectiveApiResponse` | Vorläufer des Surface-Template-Resolvers (Phase I); um die Surface-Ebene und die Struktur-/Token-Achsen zu erweitern |
| `WorkspaceThemeEndpoints`, `WorkspaceTheme*`-DTOs | gehen in die Surface-Bundle-Zuweisung + Token-Kaskade (§10) über |
| `/workspace/auth/login` (WorkspaceMembership.Role) | ist konzeptionell der **Workspace-Admin**-Zugang → in den gemeinsamen Admin-Login (§3.3) überführen, **nicht** ein eigener Workspace-Login. Der Surface-Login (§6.2) ist davon getrennt |
| `PluginAdminExtensionEndpoints`, `PluginAdminNavigation` (permission-/scope-gefiltert) | genau der Admin-Extension-Point-Mechanismus aus §3/§11.3 — bereits vorhanden; im Kern fehlt nur die Token-Achse (White-Label) |

Kernkorrektur gegenüber der bisherigen Planung: Die Auth-Trennung verläuft entlang
**Administration vs. Surface**, nicht entlang **Platform vs. Workspace**.

---

# 15. Reihenfolge (Framework-Kern zuerst)

Die Surface-Engine ist **Kern des domänenneutralen Frameworks**, nicht
kommunikationsspezifisches Beiwerk — ein „eigenes Symfony/Shopware für .NET" ohne
erweiterbare Oberflächen wäre keins. Die Reihenfolge priorisiert den tragenden Kern:

1. **Admin-Vereinheitlichung** (Phase B, klein): gemeinsamer Admin-Login inkl. Workspace-Admin; effektiver Kontext-Endpunkt.
2. **Surface-Domänenmodell** (Phase A): Surface, AccessMode, Audience, Realm, Domain-Auflösung; Surface-Scope in die bestehende `BackendClaimTypes`/Scope-Mechanik.
3. **Basis-Template + Token-Kaskade** (Phase F + §10): SurfaceShell, Token-Achse mit `locked`. Für die erste Distribution genügt das **SPA-Root-Template** (§11.2) — die vorhandenen Shells laufen als je eine Surface mit SPA-Root.
4. **Surface-Bundle-Mechanismus** (Phasen G/H zusammengefasst): ein Bundle-Typ, ein Manifest, zwei Achsen.
5. **Engine-Entscheidung + Template-Compiler** (Phase I): die Bau-vs-Kauf-Entscheidung zur Template-Engine ist der eigentliche Blocker und bekommt eine **eigene, vorgelagerte ADR** (§13). Das volle Multi-Inheritance-Block-System lohnt erst, sobald ein zweiter Oberflächentyp (Website/Portal) real existiert.
6. **Page-Builder** (§10.4): separates Plugin, später.

---

# 16. Explizite TODOs

## Phase A — Domänenmodell

* [ ] `SurfaceId` als Value Object einführen.
* [ ] `WorkspaceSurface` als Domain Entity einführen.
* [ ] `SurfaceType` als erweiterbaren Schlüssel statt als geschlossenes Enum modellieren.
* [ ] `SurfaceAccessMode` mit `Public`, `Authenticated` und `Mixed` einführen.
* [ ] `SurfaceAudience` beziehungsweise Audience-Schlüssel modellieren.
* [ ] `AuthenticationRealm` als surfacebezogene Konfiguration modellieren.
* [ ] Domain- und Basisroutenzuordnung zu Surfaces implementieren.
* [ ] Workspace-Default-Template und Surface-Template-Override modellieren.
* [ ] Workspace-Default-Theme und Surface-Theme-Override modellieren.
* [ ] Surface-Status und Lifecycle definieren.
* [ ] Lösch- und Purge-Verhalten für Surfaces definieren.
* [ ] Audit Events für Erstellung, Änderung, Aktivierung und Löschung ergänzen.

## Phase B — Administration und Scope-Autorisierung

* [ ] Gemeinsamen Admin-Login als offiziellen Administrationszugang festlegen.
* [ ] Administrationssession von Surface-Sessions trennen.
* [ ] `/api/admin/context` oder äquivalenten Context-Endpunkt definieren.
* [ ] Platform-, Tenant-, Workspace- und Surface-Scopes formal modellieren.
* [ ] Zuordnung von Rollen und Permissions zu Scopes implementieren.
* [ ] effektive Berechtigungsberechnung pro Ressource implementieren.
* [ ] Mehrfachrollen und überlappende Scope-Zuweisungen unterstützen.
* [ ] serverseitige Scope-Guards für alle Administrationsendpunkte prüfen.
* [ ] Tests gegen Cross-Tenant- und Cross-Workspace-Zugriffe ergänzen.
* [ ] Navigation der Admin-Shell aus dem effektiven Context ableiten.
* [ ] Plugin-Admin-Navigation ebenfalls permission- und scopeabhängig filtern.
* [ ] MFA-Policy für die Administration vorbereiten.
* [ ] OIDC-Audience und Cookie-Policy für Administration separat konfigurierbar machen.

## Phase C — Surface-Administration

* [ ] Administrationsendpunkte für Surface CRUD erstellen.
* [ ] Domainzuweisung und Domain-Eindeutigkeit validieren.
* [ ] Surface-Typ-Auswahl über Plugin-Contributors ermöglichen.
* [ ] Access Mode administrierbar machen.
* [ ] Audience und Authentication Realm administrierbar machen.
* [ ] Template-Plugin einer Surface zuweisen.
* [ ] Workspace-Default-Template konfigurieren.
* [ ] Theme-Plugin einer Surface zuweisen.
* [ ] Workspace-Default-Theme konfigurieren.
* [ ] Surface-Aktivierung und effektive Verfügbarkeit implementieren.
* [ ] Surface-Kompatibilität mit Plugins und Capabilities prüfen.
* [ ] Preview- beziehungsweise Testmodus für Surfaces vorbereiten.

## Phase D — Surface Runtime

* [ ] Surface anhand Hostname und Pfad auflösen.
* [ ] Tenant und Workspace aus der Surface-Auflösung bestimmen.
* [ ] Surface Access Policy vor dem Rendering anwenden.
* [ ] Routenabhängige Overrides bei `Mixed`-Surfaces unterstützen.
* [ ] reservierte Auth-Routen definieren.
* [ ] Return-URL-Validierung implementieren.
* [ ] Login-, Logout-, Callback- und Access-Denied-Flows implementieren.
* [ ] Surface Context für Templates und Plugins bereitstellen.
* [ ] aktuelle Identity, Membership, Profile und Permissions in den Context integrieren.
* [ ] Surface-spezifische Locale- und Theme-Auflösung implementieren.
* [ ] Health- und Diagnoseinformationen für Surface-Auflösung ergänzen.

## Phase E — Identity und Principal-Profile

* [ ] Identity von fachlichen Profilen trennen.
* [ ] Tenant Membership formal definieren.
* [ ] Workspace Membership formal definieren.
* [ ] Principal-Profile als pluginerweiterbaren Mechanismus definieren.
* [ ] Standardprofile `Employee`, `Agent`, `Customer`, `Partner` bewerten.
* [ ] festlegen, welche Profile im Core und welche in Plugins liegen.
* [ ] Surface-Audience gegen Membership und Profile prüfen.
* [ ] getrennte Authentifizierungsrealms pro Surface unterstützen.
* [ ] mehrere Identity Provider pro Tenant beziehungsweise Surface unterstützen.
* [ ] Account-Linking und Mehrfachprofile konzeptionell festlegen.
* [ ] Benutzerexport und -löschung um Profile und Surface-Zugänge erweitern.

## Phase F — SurfaceShell

* [ ] minimales `SurfaceShell`-Template definieren.
* [ ] stabile Root-Blocks festlegen.
* [ ] Login-Template beziehungsweise Login-Blocks definieren.
* [ ] Access-Denied- und Error-Blocks definieren.
* [ ] Asset-, Metadata-, Style- und Script-Blocks definieren.
* [ ] XML-/Markdown-Dokumentation für alle öffentlichen Blocks erstellen.
* [ ] Public-Block-Baseline für Kompatibilitätsprüfungen einführen.
* [ ] Referenztests für `extend` und `parent()` erstellen.
* [ ] SurfaceShell frei von fachlichen Layoutvorgaben halten.

## Phase G — Template-Plugins

* [ ] Capability beziehungsweise Plugin-Typ `surface.template` definieren.
* [ ] Template-Manifest definieren.
* [ ] Parent-Template-Abhängigkeiten modellieren.
* [ ] unterstützte Surface-Typen deklarierbar machen.
* [ ] View-, Style-, Script- und Asset-Ketten getrennt modellieren.
* [ ] öffentliche Blocks deklarierbar machen.
* [ ] Template-Konfiguration deklarierbar machen.
* [ ] zyklische Template-Abhängigkeiten erkennen.
* [ ] fehlende Parent-Templates als Aktivierungsfehler behandeln.
* [ ] deterministische Plugin- und Template-Reihenfolge implementieren.
* [ ] Prioritätsmodell für Block-Erweiterungen definieren.
* [ ] Kompatibilitätsprüfung bei Template-Updates implementieren.
* [ ] Template-Plugin-Deaktivierung blockieren, wenn es aktiven Surfaces zugewiesen ist.
* [ ] Fallback auf Workspace- oder Distribution-Template definieren.

## Phase H — Token-Achse (Theme) des Surface-Bundles

> Teil desselben Bundle-Mechanismus wie Phase G (ein Plugin-Typ, ein Manifest); hier die Token-/Branding-Achse und die Kaskade inkl. `locked` (§10).

* [ ] Capability beziehungsweise Plugin-Typ `surface.theme` definieren.
* [ ] Theme-Manifest festlegen.
* [ ] Design Tokens und CSS Custom Properties standardisieren.
* [ ] Theme-Vererbung modellieren.
* [ ] Konfigurationsvererbung definieren.
* [ ] Theme-Zuweisung auf Workspace- und Surface-Ebene implementieren.
* [ ] Theme-Assets und Brandingauflösung implementieren.
* [ ] Template- und Theme-Kompatibilitätsprüfung definieren.
* [ ] Theme-Preview in der Administration vorbereiten.

## Phase I — Template Resolver und Compiler

* [ ] `ISurfaceTemplateRegistry` definieren.
* [ ] `ISurfaceTemplateAssignmentStore` definieren.
* [ ] `ISurfaceTemplateResolver` definieren.
* [ ] `ISurfaceViewLoader` definieren.
* [ ] `ISurfaceTemplateCompiler` definieren.
* [ ] `ISurfaceAssetCompiler` definieren.
* [ ] `ISurfaceTemplateCache` definieren.
* [ ] effektive Template-Vererbungskette berechnen.
* [ ] effektive View-, Style-, Script- und Asset-Ketten berechnen.
* [ ] Cache-Key mindestens aus Tenant, Workspace, Surface, Template, Plugins, Versionen, Konfiguration und Locale bilden.
* [ ] Cache bei Pluginaktivierung, -deaktivierung und -update invalidieren.
* [ ] Templatefehler als sichtbaren Runtime-State behandeln.
* [ ] Diagnoseansicht für die effektive Template-Komposition implementieren.

## Phase J — Frontend und offizielle Referenztemplates

* [ ] entscheiden, welche Technologie die offizielle Administration verwendet.
* [ ] öffentliche Administrations-Extension-API von internen Frameworkkomponenten trennen.
* [ ] offizielles Website-Template als Plugin erstellen.
* [ ] offizielles Application-/Dialer-Template als Plugin erstellen.
* [ ] offizielles Portal-Template als Plugin erstellen.
* [ ] mindestens ein Template ohne Vue-Abhängigkeit als Referenz erstellen.
* [ ] mindestens ein SPA-Template als Referenz erstellen.
* [ ] Beispiel für ein Kundentemplate mit Vererbung erstellen.
* [ ] Beispiel für ein Feature-Plugin erstellen, das einen öffentlichen Block erweitert.
* [ ] Beispiel für unterschiedliche Templates innerhalb desselben Workspaces erstellen.

## Phase K — Tests und Governance

* [ ] Architekturtests für Administration-, Workspace- und Surface-Grenzen ergänzen.
* [ ] sicherstellen, dass Core keine konkreten Template-Plugins referenziert.
* [ ] sicherstellen, dass SurfaceShell keine fachlichen Pluginverträge enthält.
* [ ] Public-API-Baseline für Surface- und Template-Verträge einführen.
* [ ] Contract-Testpaket für Template-Plugins entwickeln.
* [ ] Contract-Testpaket für Surface-Type-Plugins entwickeln.
* [ ] Cross-Tenant-, Cross-Workspace- und Cross-Surface-Sicherheitstests ergänzen.
* [ ] Login- und Session-Isolation zwischen Administration und Surfaces testen.
* [ ] Template-Vererbungs- und Prioritätstests ergänzen.
* [ ] Update- und Rollbacktests für Templates und Themes ergänzen.
* [ ] Performance- und Cachetests für große Plugin-Kompositionen ergänzen.
* [ ] Dokumentation für Pluginentwickler erstellen.

---

# 17. Zusammenfassung

Die endgültige Begriffs- und Verantwortungsverteilung lautet:

```text
Administration
= zentrale Control Plane mit gemeinsamem Admin-Login

Workspace
= tenantgebundene fachliche und technische Umgebung

Surface
= konkrete Nutzungs- und Zugangsoberfläche eines Workspaces

Identity / Principal
= Person, Membership, Rolle und fachliches Profil

SurfaceShell
= einziges minimales Root-Template

Surface-Bundle (Template + Theme)
= ein Plugin-Typ, zwei Achsen: Struktur (Blocks) + Tokens (Branding)

Feature-Plugin
= fachliche Funktion und Block-Erweiterung
```

Ein Admin-Login bedient SuperAdmins, Tenant Admins und Workspace Admins. Sichtbare Funktionen und erlaubte Operationen werden aus Rollen, Permissions und Scopes ermittelt.

Ein Workspace kann mehrere Surfaces besitzen. Jede Surface kann eigene Domains, Templates, Themes, Audiences und Loginverfahren verwenden.

Eine Surface ist kein Customer-, Employee- oder Agent-Datensatz. Sie ist das Frontend, über das sich entsprechende Principals anmelden und arbeiten.

Die SurfaceShell bleibt minimal. Website-, Dialer-, Portal- und andere Oberflächen werden als Template-Plugins umgesetzt, die Shopware-artig über Blocks, `extend` und `parent()` erweitert werden können.

---

# 18. Anhang — Modellierungs-Beispiele (Tenant / Workspace / Surface)

Die drei Ebenen sind **orthogonale Achsen**, keine feste Tiefe. Jedes Deployment
nutzt nur so viele, wie es braucht; ungenutzte kollabieren transparent (Default-
Tenant, Default-Workspace). Die Achsen beantworten je eine Frage:

| Ebene | Frage |
| --- | --- |
| **Tenant** | Wer zahlt / Isolationsgrenze (der Kunde, ein Vertrag) |
| **Workspace** | *Welches System* — der Daten- und Plugin-Container (aktivierte Plugins + gemeinsame Daten + Mitglieder) |
| **Surface** | *Welcher Zugang* — konkrete Ausgabe-/Zugangsfläche auf die Workspace-Daten |

**Verhältnis zu Shopware:** `Surface ≈ SalesChannel`. Shopware fehlt bewusst die
Workspace-Ebene, weil es ein Domänen-Produkt mit *einem* Datenmodell pro Merchant
ist (SalesChannels sind Sichten darauf). Callora ist eine Plattform, auf der ein
Tenant **mehrere getrennte Systeme** betreiben kann — dafür existiert der Workspace
als Container/Isolationsgrenze.

## 18.1 Beispiel A — ein System, viele Zugänge (geteilte Daten)

Kunde will **CRM + Dialer + ContactCenter**. Das ist *ein* zusammenhängendes System:
CRM, Dialer, ContactCenter sind **Plugins**, die im selben Workspace aktiv sind und
auf **dieselben** Kontakte/Kampagnen/Agenten arbeiten.

```text
Tenant „Muster GmbH“
└── Workspace „Contact-Center“   (Plugins: CRM, Dialer, ContactCenter — gemeinsame Daten)
    ├── Surface: Agent-Desktop        (Agenten arbeiten Kampagnen ab)
    ├── Surface: Supervisor-Dashboard (Teamleiter überwachen)
    └── Surface: Kundenportal          (Endkunden)
```

→ **1 Tenant / 1 Workspace / N Surfaces.** Der Workspace ist der *Daten-Container*;
die Surfaces sind verschiedene Zugänge auf dieselben Daten.

## 18.2 Beispiel B — mehrere getrennte Systeme (isolierte Daten)

Kunde will **Website und CRM getrennt** betreiben (keine gemeinsamen Daten).

```text
Tenant „Muster GmbH“
├── Workspace „Website“   (Plugin: CMS — eigene Seiten, Redakteure)
│   └── Surface: öffentliche Website
└── Workspace „CRM“       (Plugin: CRM — eigene Kontakte, Vertrieb)
    └── Surface: internes CRM-Frontend
```

→ **1 Tenant / mehrere Workspaces / je eigene Surfaces.** Die Workspaces sind hart
datenisoliert (Workspace-Query-Filter + Write-Backstop, PLAT-267). Der TenantAdmin
verwaltet beide; WorkspaceAdmins sind pro Workspace.

## 18.3 Faustregel und Grenzfall

- **Geteilte Daten → ein Workspace, mehrere Surfaces.**
- **Isolierte Systeme → mehrere Workspaces.**
- **Zentral verwaltet/abgerechnet → ein Tenant darüber.**

Sollen zwei getrennte Systeme *doch* Daten austauschen (z. B. Website-Kontaktformular
→ CRM-Lead), ist der richtige Weg **nicht** das Zusammenlegen in einen Workspace,
sondern ein **expliziter Integrationspfad**: ein Business-Event/Webhook aus dem einen
Workspace, den eine Flow-Regel oder ein Plugin im anderen aufnimmt. Die Isolation
bleibt, der Datenfluss ist bewusst und auditierbar. Deshalb ist „ein vs. mehrere
Workspaces" eine bewusste Onboarding-Entscheidung: Zusammenlegen im Nachhinein ist
Datenmigration, Datenaustausch via Events ist billig.
