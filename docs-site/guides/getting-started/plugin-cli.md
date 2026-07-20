# The `callora` CLI

The `callora` CLI is your authoring companion: it scaffolds new plugins, validates them
against the host contract, and signs them for distribution. This guide is task-oriented —
one section per command, with the exact flags and what each command does.

## What you'll learn

- How to scaffold a new plugin with `plugin new`
- How to validate a built plugin against the host contract with `plugin test-contract`
- How to sign a plugin package with `plugin sign`
- How to invoke the CLI from within this repo

::: tip Prerequisites

- The **.NET 10 SDK**.
- In this repository the CLI runs via `dotnet run`; there is no separate install step:

  ```bash
  dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj -- plugin <command> …
  ```

  The `--` separates `dotnet run` arguments from the CLI's own arguments. If you have the
  CLI installed as a global tool, use `callora plugin …` directly instead. Examples below
  use the short `callora …` form.
:::

Run `callora` with no arguments (or `--help`) to print usage for all three commands.

## `plugin new` {#plugin-new}

Scaffolds a new plugin project.

```bash
callora plugin new [name] [--name <display-name>] [--id <plugin-id>] [--output <directory>] [--force]
```

| Argument | Purpose | Default |
| --- | --- | --- |
| `[name]` / `--name` | Display name; also drives the project name. | required |
| `--id` | Stable plugin id used in `registry.json`. | derived from the name |
| `--output` | Target directory. | `custom/plugins/<safe-name>` under the current directory |
| `--force` | Allow scaffolding into a non-empty directory. | off |

The plugin id must contain only `a-z`, `A-Z`, `0-9`, `.`, `-`, `_`.

### What it generates

For a project named `MyPlugin`, the scaffold writes three files:

```text
<output>/
├─ Callora.Plugins.MyPlugin.csproj
├─ registry.json
└─ Application/
   └─ MyPluginPlugin.cs
```

- **`Callora.Plugins.MyPlugin.csproj`** — targets `net10.0`, references the host contracts
  at compile time only (`Callora.Core` with `ExcludeAssets="runtime"`), and copies
  `registry.json` to the output. When run inside the repo it uses a `ProjectReference` to
  `src/Core/Callora.Core.csproj`; outside the repo it uses a `PackageReference` to
  `Callora.Core`. It does **not** set `CalloraFrameworkAssembly` — so the contract analyzers
  apply. See [Plugin project layout](/guides/getting-started/project-layout).
- **`Application/MyPluginPlugin.cs`** — a minimal `IHostManagedPlugin` implementation with
  `PluginId`, `DisplayName`, and no-op `StartAsync`/`StopAsync`.
- **`registry.json`** — a `v1` manifest pre-filled with `pluginId`, `assemblyFileName`,
  `entryTypeName`, a sample `workspace.navigation` capability, and a `Callora.Core >=0.1.0`
  dependency.

Expected output:

```text
Plugin scaffold created: /abs/path/to/custom/plugins/MyPlugin
```

::: warning Non-empty output directory
If the target directory already contains files, the command fails with
`Output directory is not empty: …`. Pass `--force` to scaffold anyway.
:::

## `plugin test-contract` {#plugin-test-contract}

Validates a **built** plugin against the host contract — manifest fields, contract version,
and the entry type. Run it after `dotnet build`.

```bash
callora plugin test-contract --assembly <path-to-dll> [--registry <path-to-registry.json>] [--entry-type <full-type-name>]
```

| Flag | Purpose |
| --- | --- |
| `--assembly` | Path to the built plugin DLL. Required. |
| `--registry` | Path to `registry.json`. Defaults to `registry.json` next to the DLL. |
| `--entry-type` | Full entry type name to check. Overrides the manifest's `entryTypeName`. |

### What it validates

**Manifest fields** — the following registry fields are required and are checked:
`contractVersion`, `schemaVersion`, `name`, `pluginId`, `version`, `assemblyFileName`, and
`entryTypeName`. Beyond presence:

- `contractVersion` must be the supported value `v1`.
- `assemblyFileName` must match the actual built DLL's file name.

**Contract compatibility** — the assembly must reference `Callora.Core`, and its major
version must match the host's contract major version.

**Entry type (lifecycle)** — the entry type must be a concrete (non-abstract) class that
implements `IHostManagedPlugin`, has a public parameterless constructor, and returns
non-empty `PluginId` and `DisplayName` values when instantiated. If `--entry-type` /
`entryTypeName` is omitted, the tool auto-detects the single implementing type.

Each failure is reported with a code, a message, and a remediation hint. On success:

```text
All contract checks passed.
```

On failure the process exits non-zero and prints one line per issue, e.g.:

```text
[manifest.contractVersion.missing] registry.json field 'contractVersion' is required. Fix: Set 'contractVersion' to 'v1'.
```

## `plugin sign` {#plugin-sign}

Signs a plugin package by producing a signed **content manifest** — Callora's trust model is
based on content signatures, not Authenticode.

```bash
callora plugin sign --plugin <plugin-directory> --key <private-key.pem> [--out <plugin.signature.json>]
```

| Flag | Purpose |
| --- | --- |
| `--plugin` | The plugin directory to sign. Required. |
| `--key` | An **ECDSA P-256** private key in PEM format. Required. |
| `--out` | Output path. Defaults to `plugin.signature.json` inside the plugin directory. |

### What it does

The command hashes **every file** in the plugin directory (except the signature file
itself) — the assembly, dependent assemblies, UI bundles, templates, migrations, and
`registry.json` — builds a signature manifest of those hashes, signs the canonical manifest
with the ECDSA P-256 key (SHA-256), and writes `plugin.signature.json`. Covering the whole
directory makes the entire package tamper-evident: no content lives outside the signed set.
The manifest also records the signer's key fingerprint, which is the basis of trust.

Expected output:

```text
Plugin signature written: /abs/path/to/plugin/plugin.signature.json
```

::: warning Keep the signing key outside the plugin directory
The signing key must not live inside the directory being signed. `registry.json` (with a
`pluginId` and `assemblyFileName`) must exist, and the declared assembly must actually be
present in the directory, or signing fails.
:::

At install time the host verifies this signature. A trusted signature is required unless the
operator has enabled `AllowUnsignedPlugins` for development — see
[Install and activate plugins](/guides/getting-started/install-activate#hot-loading-in-detail).

## Next steps

- [Build your first plugin](/guides/getting-started/your-first-plugin) — `plugin new` and
  `plugin test-contract` in a full workflow.
- [Plugin project layout](/guides/getting-started/project-layout) — the structure `plugin new`
  produces, explained.
- [Install and activate plugins](/guides/getting-started/install-activate) — install the
  plugin you just built and signed.
