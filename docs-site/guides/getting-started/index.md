# Getting Started

Callora is a plugin platform for .NET: a host application that discovers, installs, and
runs plugins as first-class extensions — with hot install and activation, no host restart,
and a compiler-enforced contract between host and plugin. This section takes you from zero
to a running plugin and gives you the operator and project knowledge to keep going.

## What you'll learn

- The recommended path through these getting-started guides
- Where the hands-on tutorial fits and what to read next
- Where to find deeper conceptual and reference material

## Start here: build a plugin

The fastest way to understand Callora is to build a plugin end to end. The tutorial
scaffolds a plugin, adds an HTTP endpoint, validates its contract, then installs and
activates it in a running host — hot, with no restart.

- [Build your first plugin](/guides/getting-started/your-first-plugin) — the hands-on
  tutorial. Begin here.

## Then: the operator and author essentials

Once you've seen a plugin run, these three guides fill in the model behind it:

- [Install and activate plugins](/guides/getting-started/install-activate) — the plugin
  lifecycle from an operator's view: the `Installed → Active → Inactive` state model
  (with the terminal `Uninstalled`), and the operator API that drives it.
- [Plugin project layout](/guides/getting-started/project-layout) — the recommended
  project structure (Domain / Application / Infrastructure), where `registry.json`, the
  `.csproj`, and UI resources live, and the contract rules your project must follow.
- [The `callora` CLI](/guides/getting-started/plugin-cli) — scaffold plugins
  (`plugin new`), validate them against the host contract (`plugin test-contract`), and
  sign them for distribution (`plugin sign`).

## Next steps

- **Fundamentals** — how the pieces fit together: the
  [plugin entry contract](/guides/fundamentals/plugin-entry) and
  [backend extensions](/guides/backend-extensions).
- **Concepts** — the [platform architecture](/concepts/architecture).
- **Reference** — the [REST API](/reference/rest-api) and the
  [.NET API reference](/api/).
