# Autoload/Discovery & Grenzen: Symfony vs. Shopware — und die Einordnung für Callora

> Recherche vom 2026-07-16. Quellen: `symfony/symfony` (GitHub, default branch, über gh-tooling),
> Shopware-Plattform-Monorepo (lokales Checkout `shopware/src`). Zweck: das .NET-Äquivalent in
> Callora bewusst und belegt aufbauen — nicht aus dem Gedächtnis, sondern gegen die realen Quellen.

## Kernbefund

Zwei Dinge, die leicht verwechselt werden, sind strikt zu trennen:

- **Autoload** = Klassen physisch laden (Composer PSR-4 ↔ .NET Assembly/ALC).
- **Auto-Discovery** = Implementierungen automatisch als *Rollen* registrieren (Marker-Interface/Attribut → DI-Tag).

Beim **Autoloading** sind Symfony und Shopware fast identisch — und in .NET bekommen wir beides geschenkt
(das Assembly *ist* die Classmap, `Assembly.GetTypes()` *ist* die Enumeration, der per-Plugin
`AssemblyLoadContext` *ist* der Plugin-Autoloader).

Der scharfe Unterschied liegt bei den **Grenzen**:
- **Symfony**: erzwingt Grenzen *fast nicht technisch* — `@internal`/`@final`/`@experimental` sind reine
  phpDoc-Marker ohne durchsetzendes Tool. Hart nur: Deprecations zur Laufzeit + `composer conflict`.
  BC-Promise = Prozess (Doku + Core-Team-Review). **Freiheit über Schutz.**
- **Shopware**: erzwingt Grenzen *hart* — ~40 Custom-PHPStan-Rules + PHPat + Roave-BC-Checker.
  `@internal` ist ein **BC-Vertrags-Ausschluss**, kein Sichtbarkeits-Hinweis. **Schutz über Freiheit.**

**Callora hat sich bereits für Shopwares harte Linie entschieden** (Roslyn-Analyzer CAL0001/CAL0002 +
Marker-Attribute + PublicApiAnalyzers-Baseline).

---

## 1. Was sie autoloaden / auto-discovern

| Ebene | Symfony | Shopware |
|---|---|---|
| **Klassen (PSR-4)** | `composer.json` PSR-4 + `replace:` (~80 Split-Pakete, Monorepo→Komponenten) | `composer.json` `Shopware\`→`src/`; **Plugins zur Laufzeit** per `$classLoader->addPsr4()` (`KernelPluginLoader.php:222`) ohne Neu-Dump |
| **DI-Rollen (Kern)** | `registerForAutoconfiguration(Interface/Attribut → Tag)`, **verteilt** auf `ServicesBundle` (DI-Component), `ConsoleBundle`, `FrameworkExtension` (~60 Rollen gesamt) | **EIN** zentraler `AutoconfigureCompilerPass` (~34 Interfaces/Attribute → Tag, Prio 1000); spezialisierte Passes sammeln per `findTaggedServiceIds` ein |
| **Verzeichnis-Autowiring** | Skeleton-Konvention `resource: '../src/'` + `exclude:` (Loader: `PrototypeConfigurator`, rekursiver Glob-Scan) | **Bewusst kaum genutzt** — Services überwiegend explizites XML; Discovery über Tags/Interfaces, nicht Ordner-Scan |
| **Modul-Konvention** | Bundle + `config/bundles.php` | `Bundle::build()` lädt deterministisch `Resources/config/services.*`, `{routes}*`, `Migration/`, Snippets, Twig — **der Ort einer Datei ist ihre Registrierung** |
| **Domänen-Discovery** | Doctrine `#[Entity]` u.a. | DAL `EntityDefinition → shopware.entity.definition`; Migrationen per Ordner `Migration/V6_*/` + Timestamp-Klassenname; ScheduledTasks/Rules/FlowActions je eigenes Tag |

### Symfony: `registerForAutoconfiguration` — Fundort-Topologie
Verteilt auf drei Orte (bewusst je Komponente, nicht zentral):
- **DI-Component** (`ServicesBundle`): `EventSubscriberInterface`→`kernel.event_subscriber`,
  `ServiceSubscriberInterface`, `EnvVarLoaderInterface`, `EnvVarProcessorInterface`, `ResetInterface`,
  `#[AsEventListener]`; `CompilerPassInterface`/`TestCase`/`\UnitEnum`/`#[\Attribute]`→`container.excluded`.
- **Console-Component** (`ConsoleBundle`): `Command`/`#[AsCommand]`→`console.command`,
  `ValueResolverInterface`→`console.argument_value_resolver`.
- **FrameworkBundle** (`FrameworkExtension`, die Masse): `FormTypeInterface`→`form.type`,
  `NormalizerInterface`/`DenormalizerInterface`→`serializer.normalizer`, `ConstraintValidatorInterface`→
  `validator.constraint_validator`, `DataCollectorInterface`, `CacheWarmerInterface`,
  `AbstractController`→`controller.service_arguments`, `#[AsMessageHandler]`→`messenger.message_handler`,
  `#[AsController]`, `#[Route]`, `#[AsSchedule]`/`#[AsPeriodicTask]`/`#[AsCronTask]`→Scheduler u.v.m.

> **Load-bearing:** Die Basis-Rollen (Command, EventSubscriber, ServiceSubscriber) werden NICHT im
> App-Framework registriert, sondern tief in der jeweiligen Komponente. .NET-Lehre: die Auto-Registrierung
> gehört zur Komponente/zum Modul, das die Rolle definiert — nicht in einen App-weiten Gott-Registrar.

### Symfony: Autoconfiguration-Attribute (DI-Component)
`AsAlias`, `AsDecorator`, `AsTaggedItem`, `Autoconfigure`, `AutoconfigureTag`, `Autowire`,
`AutowireIterator`, `AutowireLocator`, `Exclude` (= Prototype-Scan-Ausschluss), `Lazy`, `Target`,
`When`/`WhenNot` (env-abhängige Registrierung).

### Shopware: der Discovery-Kern (Compiler-Passes)
- **`AutoconfigureCompilerPass`** (Prio 1000) — der Kern: 1 Attribut- + 33 Interface-Autoconfigurations
  (`EntityDefinition`, `EntityExtension`, `ScheduledTask`, `Rule`, `CartProcessorInterface`,
  `AbstractPaymentHandler`, `EntityIndexer`, `AbstractRouteScope`, `FlowStorer`, …).
- `EntityCompilerPass` — sammelt `shopware.entity.definition` → `DefinitionInstanceRegistry` + Repositories.
- `AttributeEntityCompilerPass` — kompiliert `#[Entity]`-Klassen zu Definitions.
- `RouteScopeCompilerPass`, `MessageHandlerCompilerPass`, `BusinessEventRegisterCompilerPass`,
  `FeatureFlagCompilerPass` (entfernt Services mit inaktivem `shopware.feature`-Tag), Twig/Asset-Passes.
- **Bundle-als-Convention-Container**: `Bundle::build()` → `registerContainerFile` (Glob `services.*`),
  `registerMigrationPath` (Tag `shopware.migration_source`), `configureRoutes` (Glob `{routes}*`),
  Snippets, Twig-View-Pfade.

**Gemeinsames Prinzip beider:** *Contract-Implementierung = Registrierung.* Ein Marker-Interface/Attribut
zu erfüllen genügt — kein manuelles Verdrahten. Unterschied ist nur die Topologie (Symfony verstreut,
Shopware bündelt).

---

## 2. Welche Grenzen sie setzen

| Grenze | Symfony | Shopware |
|---|---|---|
| **`@internal`** | phpDoc-Marker, **kein Tool erzwingt es** (eigener PHPStan nur `level 5` + Security-Rules) | **BC-Vertrags-Ausschluss**; `InternalClassRule`/`InternalMethodRule` verlangen den Marker, Roave-BC-Checker respektiert ihn |
| **`@final` / final** | nur Doc (`@final` an nicht-finalen Klassen) | hart `final` erzwungen (`MessageHandlerFinalRule`, `AttributeFinalRule`); `@final` = angekündigte Vorstufe |
| **Modulgrenzen** | — | **`RestrictNamespacesRule`** (PHPat): Core ↛ Administration/Storefront/Elasticsearch; diese nur → Core |
| **Decoration** | — | `DecorationPatternRule` + `AbstractClassUsageRule`: nur über abstrakte Basis, `getDecorated()`, keine Extra-public-Methoden; `PublicServiceDecoratorRule`, `NoRouteOverrideInDecoratorsRule` |
| **Domänen-Zuordnung** | — | `#[Package]` **Pflicht** je Klasse (`PackageAnnotationRule`) |
| **API-Feld-Exposition** | — | **`ApiAware`-Flag, fail-closed**: Felder default NICHT exponiert, nur mit `->addFlags(new ApiAware())` |
| **Domain-Exceptions** | — | `DomainExceptionRule`: kein rohes `\Exception`, nur `{Domain}Exception extends HttpException` |
| **Migrations-Disziplin** | — | `AddColumnRule`/`NoAfterStatementRule`/`NoDropStatementInUpdateRule` (Blue-Green-/INSTANT-tauglich) |
| **Deprecations** | `trigger_deprecation()` → `E_USER_DEPRECATED`, CI-phpunit-bridge → Hard-Fehler | `Feature::triggerDeprecationOrThrow` (~345 Sites) + `DeprecatedMethodsThrowDeprecationRule` erzwingt Call; Feature-Flags steuern warn→throw→removal |
| **BC-Torwächter** | **Prozess**: Doku + Review, kein Code-Gate | **`roave/backward-compatibility-check`** CI-Job — liest Marker, bricht bei unmarkierten Signaturänderungen |
| **Abhängigkeiten** | `composer.json` `conflict:` | dito |

**Der entscheidende Satz:** Bei Shopware ist `@internal` *kein Sichtbarkeits-, sondern ein
BC-Vertrags-Ausschluss* — durchgesetzt durch „**Analyzer erzwingt den Marker**" + „**BC-Tool respektiert
den Marker**". Symfony hat dieselben Marker ohne die erzwingende Maschine.

### Shopwares Custom-PHPStan-Rules (der Governance-Kern, Auszug)
`core-rules.neon` (Core + Plugin-Dev): `Internal/InternalClassRule`, `Internal/InternalMethodRule`,
`Deprecation/DeprecatedMethodsThrowDeprecationRule`, `AbstractClassUsageRule`, `DecorationPatternRule`,
`PackageAnnotationRule`, `DomainExceptionRule`, `ExtensionRule`, `AclValidPermissions*`,
`ShopwareNamespaceStyleRule`, `AttributeFinalRule`, `NameConstantEntityDefinition`,
`Migration/{AddColumn,NoAfterStatement,NoDropStatementInUpdate}Rule`.
`rules.neon` (öffentlich als `shopware/phpstan-extension`): `MessageHandlerFinalRule`,
`RestrictNamespacesRule`, `PublicServiceDecoratorRule`, `NoRouteOverrideInDecoratorsRule`,
`RouteScopeRule`, `UseCLIContextRule`, `UseHasherRule`, `NoUnserializeUsageRule`, DAL-Disziplin-Rules.

---

## 3. Einordnung für Callora

| Mechanismus | Symfony | Shopware | Callora heute | Lücke |
|---|---|---|---|---|
| Klassen-Autoload | PSR-4 | PSR-4 + ALC | Assembly + per-Plugin-ALC ✅ | — (geschenkt) |
| Rollen-Discovery (Plugin) | — | — | `ICalloraPluginCatalog.GetExports<T>` für **6 Rollen** ✅ | — |
| Rollen-Discovery (Host) | `registerForAutoconfiguration` | `AutoconfigureCompilerPass` | Commands-Scan + EF `ApplyConfigurationsFromAssembly` ✅, **Rest manuell** (~98 DI-Zeilen) | **zentraler Registrar fehlt** |
| `@internal`-Härte | Doc | Analyzer + BC-Tool | **CAL0001 + `[CalloraInternal]` + PublicApiAnalyzers-Baseline** ✅ | — |
| Vererbungsguard | Doc | `@final`-Rules | **CAL0002 + `[CalloraExtensible]`** ✅ | — |
| Modulgrenze | — | `RestrictNamespacesRule` | nur Roadmap (Namespace+Analyzer) | **Analyzer fehlt** |
| Decoration-Pattern | — | `DecorationPatternRule` | dynamische Decoration ✅, kein Guard | Analyzer-Kandidat |
| `#[Package]` | — | Pflicht-Attribut | — | Kandidat |
| API-Feld fail-closed | — | `ApiAware` | — | Kandidat (DTO/API-Schicht) |
| Deprecation-Lifecycle | Runtime + CI | Runtime + Feature-Flags | — | Kandidat |

**Fazit:** Bei den *Grenzen* stehen wir bereits auf Shopware-Niveau. Die echte offene Baustelle ist die
*Discovery*: uns fehlt der **eine zentrale Registrar**, der — wie Shopwares `AutoconfigureCompilerPass` —
Marker-Interfaces host-seitig automatisch registriert. Die Plugin-Seite jedes
`.Concat(GetExports<T>())` ist auto-discovered, die Host-Seite (noch) nicht.

### Empfohlene nächste Schritte
1. **Zentraler Discovery-Registrar** (`AutoconfigureCompilerPass`-Zwilling): `AddCalloraContracts(assembly)`
   registriert die 6 Contributor-Rollen host-seitig per Marker-Scan; EF-Inline-Config-Cleanup mitnehmen.
2. **Modulgrenzen-Analyzer** (später): `RestrictNamespacesRule`-Äquivalent (Core ↛ Administration/Workspace).
3. **Weitere Grenz-Kandidaten** (je eigenes, späteres Paket): Decoration-Pattern-Analyzer,
   `[CalloraPackage]`, `[ApiAware]` (fail-closed Feld-Exposition), Deprecation-Lifecycle.
