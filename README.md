<div align="center">

# Callora

**The open platform for communication products.**

A domain-neutral .NET core, a real plugin model, and a visual editor that assembles
workspaces and portals instead of programming them.

[Documentation](https://bechsteindigital.github.io/callora/) · [Architecture](docs/adr/) · [Extension points](docs-site/developers/extension-points.md)

🇩🇪 [Diese Seite auf Deutsch](README.de.md)

</div>

---

## What Callora is

A platform, not a product. The core knows nothing about telephony, appointments or
customers — it knows how **plugins** are loaded, isolated, supplied and delivered.
Everything domain-specific arrives as a plugin, including what we ship ourselves.

That is the same bet Shopware and Odoo made, for a different market: whoever needs a
contact centre, a customer portal or an agent desktop should **assemble** it rather
than commission it.

### The three layers underneath

```
Workspace          — the data (one tenant, one body of records)
 └─ Surface        — the way in (domain, sign-in, design)
     └─ Surface    — the structure (pages, nested freely, built by the customer)
         └─ Layout — what the composer makes of it
```

One workspace can have several ways in on the same data — a public website, an agent
desktop, a dialer. Each is a tree of pages, and each page can carry a composed layout
([ADR-019](docs/adr/ADR-019-surfaces-als-baum.md)).

## What makes it different

**Plugins run in-process, not beside it.** Each gets its own `AssemblyLoadContext` and
its own database schema (`plugin_<id>`), while sharing type identity with the host. A
plugin exports contracts that other plugins consume — with no HTTP in between
([ADR-013](docs/adr/ADR-013-trust-model-trusted-in-process.md)).

**The contract surface is guarded by the compiler.** `[CalloraInternal]`, CAL0001–0004
and PublicApiAnalyzers make "public API" something other than an intention: crossing the
boundary fails the build, not the customer.

**The editor renders the real components.** No iframe, no second render path, no preview
that drifts. The canvas loads the same Vue components and the same stylesheet as the live
surface, only scoped. What you see in the editor is what ships.

**Design has guardrails.** A block's configuration panel is generated from its contract,
and appearance controls pick from `--cal-*` roles — no free colour picker, no pixel
fields. A page a customer assembled still looks like the product.

**Context crosses surface boundaries.** A call answered on the agent desktop is the same
call the customer portal sees, over a declared channel with field-level visibility
([ADR-017](docs/adr/ADR-017-surface-identitaet-und-session-transport.md)).

## Quick start

Nothing installed but Docker? This path builds the host, both front-ends and every cloned
plugin into the image:

```bash
git clone https://github.com/BechsteinDigital/callora.git
cd callora

# Plugins you want along — optional, the host runs without them
git clone <communication> custom/static-plugins/Communication
git clone <videoconference> custom/plugins/videoconference

docker compose -f docker-compose.standalone.yml up --build
```

Admin at `http://localhost:5000/admin`. Nothing is enumerated anywhere: what gets built
and loaded is whatever has a `registry.json` under `custom/` — the same search for the
build and for discovery.

### Working on it

```bash
scripts/dev-build.sh                 # host + every cloned plugin
docker compose up -d                 # stack with dotnet watch, Postgres, TURN
```

`dotnet watch` rebuilds the **host**, not the plugins. After changing a plugin, run
`scripts/dev-build.sh --plugins <name>`.

### One target at a time

A full run builds both Vue suites through their MSBuild targets, plus every plugin.
Working on one of them needs only that one:

```bash
scripts/dev-build.sh --only admin        # the Vue shell under /admin
scripts/dev-build.sh --only surface      # surface runtime + SSR
scripts/dev-build.sh --only host         # .NET only, no Node needed
scripts/dev-build.sh --plugins composer  # one plugin (C# + bundles)
```

### Without Docker

```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln        # builds both front-ends too (vue-tsc + vite)
dotnet test Callora.Host.sln
```

Without Node: `dotnet build -p:SkipAdminFrontend=true -p:SkipSurfaceFrontend=true`

The front-end Vitest suites run on their own:

```bash
cd src/Administration/Resources/app/administration && npm ci && npm run test
cd src/Surface.Rendering/Resources/app/surface     && npm ci && npm run test
```

## Building a plugin

```csharp
public sealed class MyPlugin : IHostManagedPlugin
{
    public string PluginId => "my-plugin";
    public string DisplayName => "My Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
    {
        // Export contracts other plugins consume
        context.Export<IMyContract>(new MyImplementation());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}
```

Plus a `registry.json`, optionally its own database schema, an admin UI as an IIFE bundle,
and blocks for the editor. The way there is in
[Build your first Callora plugin](docs-site/guides/getting-started/) and
[Building a surface plugin](docs-site/guides/surface/building-a-surface-plugin.md).

The client half of the contract is on npm:

```bash
npm install @callora/surface @callora/admin
```

## Repository layout

| Path | What |
|---|---|
| `src/Core` | The domain-neutral core (`Callora.Core`) |
| `src/Administration` | Admin module with its colocated Vue 3 shell |
| `src/Workspace` | Workspaces, surfaces, public routing |
| `src/Surface.Rendering` | Surface rendering (Nunjucks SSR) and `@callora/surface` |
| `src/Analyzers` | Roslyn analyzers guarding the contract surface |
| `src/Plugin.Sdk` | `Callora.Plugin.Sdk` — one reference a plugin builds against |
| `src/Host/Cli` | The `callora` CLI |
| `src/Host/Dev` | This repository's runnable composition — not a product |
| `custom/static-plugins/*` | Bundled system plugins (Communication, Composer) |
| `custom/plugins/` | Install target for dynamic plugins — empty in the repository |
| `docs-site/` | The documentation (VitePress) |
| `docs/adr/` | Architecture decisions |

This repository is the **framework** — a set of packable libraries. The runnable process
and the assembly of a distribution live in the separate `callora-production` repository;
the same framework can carry several distributions.

The plugins under `custom/static-plugins` are moving into their own repositories and are
consumed as packages; their **contracts** stay public, so a third party can build against
them without seeing the implementation
([ADR-020](docs/adr/ADR-020-repo-schnitt-und-paketgrenzen.md)).

## Documentation

Published at **[bechsteindigital.github.io/callora](https://bechsteindigital.github.io/callora/)**,
with the .NET API reference under [`/api/`](https://bechsteindigital.github.io/callora/api/).

Locally:

```bash
cd docs-site && npm ci && npm run dev
```

- **[Users](docs-site/users/)** — workspaces, surfaces, administration
- **[Developers](docs-site/developers/)** — contracts, extension points, building plugins
- **[Reference](docs-site/reference/)** — APIs, manifests, analyzer rules, permissions
- **[Operations](docs-site/maintainer/)** — deployment, migrations, security

## Contributing

Bug reports, proposals and pull requests are welcome. What to know first is in
**[CONTRIBUTING.md](CONTRIBUTING.md)** — above all what the repository enforces at build
time: API baselines, governance analyzers and architecture tests strike before a review
would.

Contributions run on the **Developer Certificate of Origin**: one line in the commit
(`git commit -s`), no contract, no assignment of rights. Why that is enough and why there
is no CLA is explained there too.

## Licence

Callora is licensed under the **[Apache License 2.0](LICENSE)**.

That covers everything in this repository, including the `@callora/surface` and
`@callora/admin` packages a plugin compiles against. **A plugin may be licensed however
you like, including proprietary** — Apache-2.0 asks nothing of it.

Apache rather than MIT for the explicit **patent grant**: with codecs, SIP and echo
cancellation, patent law is real, and MIT does not address it.

© 2026 Bechstein.Digital Ecommerce UG (haftungsbeschränkt)
