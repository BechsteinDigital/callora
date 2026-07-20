# CLI Reference

The `callora` command-line interface scaffolds, validates, and signs plugins for
the Callora platform. This page catalogues every command, its flags, defaults,
behaviour, exit codes, and failure codes — extracted from the CLI source in
`src/Host/Cli/`.

The entry facade is `CalloraCliApplication.RunAsync`; each command is parsed by a
dedicated parser and executed by a dedicated worker (`PluginScaffolder`,
`PluginContractTester`, `PluginSigner`).

## Invocation

There is no packaged global tool yet. Inside this repository, invoke the CLI
through the project:

```bash
dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj -- <command> [options]
```

Everything after `--` is passed to the CLI as its argument vector. In the
examples below, the leading `callora` stands in for that invocation.

> **Status:** No published `dotnet tool` / global `callora` executable was found
> in the repository. The `dotnet run --project …` form above is the verified way
> to run the CLI in-repo.

Running with no arguments, or with `--help`, `-h`, or `help` (exactly one
argument), prints usage and exits `0`.

```
Usage:
  callora plugin new [name] [--name <display-name>] [--id <plugin-id>] [--output <directory>] [--force]
  callora plugin test-contract --assembly <path-to-dll> [--registry <path-to-registry.json>] [--entry-type <full-type-name>]
  callora plugin sign --plugin <plugin-directory> --key <private-key.pem> [--out <plugin.signature.json>]
```

## Commands at a glance

| Command | Purpose | Required input | Exit `0` | Exit `1` |
| --- | --- | --- | --- | --- |
| `plugin new` | Scaffold a new plugin project (csproj, entry class, `registry.json`). | A plugin name (positional or `--name`). | Scaffold created. | Parse error or scaffold failure. |
| `plugin test-contract` | Validate a built plugin assembly + `registry.json` against the v1 plugin contract. | `--assembly`. | All contract checks passed. | Any validation issue (each printed with its code). |
| `plugin sign` | Produce a signed content manifest (`plugin.signature.json`) over the whole plugin directory. | `--plugin` and `--key`. | Signature written. | Parse error or signing failure. |

Every command exits `0` on success and `1` on any parse or execution failure.
Failures are written to stderr; on a parse failure the usage block is re-printed.

---

## `plugin new`

Scaffolds a fresh, host-managed plugin project into a target directory:
a `.csproj`, an `Application/<Name>Plugin.cs` entry class implementing
`IHostManagedPlugin`, and a `registry.json` manifest.

### Flags

| Flag | Alias / form | Required | Default | Meaning |
| --- | --- | --- | --- | --- |
| `[name]` | positional | One of positional or `--name` | — | Display name of the plugin. Only one positional is allowed. |
| `--name <display-name>` | option | One of positional or `--name` | — | Display name. Takes precedence over the positional when both are given. |
| `--id <plugin-id>` | option | No | Derived from the name (lowercased, non-alphanumeric runs joined by `-`). | Stable plugin id written to `registry.json` and the entry class. |
| `--output <directory>` | option | No | `<cwd>/custom/plugins/<SafeNameSegment>`. | Target directory for the scaffold. |
| `--force` | flag | No | `false` | Allow scaffolding into a non-empty directory (otherwise it is refused). |

### Behaviour

- **Name resolution.** The effective name is `--name` if present, otherwise the
  positional. If neither is given the command fails with `Plugin name is required.`
- **Plugin id.** If `--id` is omitted, the id is derived from the name via
  `PluginScaffoldNaming.ToPluginId` (split on non-alphanumeric characters,
  lowercased, joined with `-`; e.g. `Acme Voice` → `acme-voice`). The id must
  match `[a-zA-Z0-9._-]`, be ≤ 128 characters, and must not start or end with
  `-`, `.`, or `_`; otherwise scaffolding fails with
  `Invalid plugin id. Allowed: a-z, A-Z, 0-9, '.', '-', '_'.`
- **Output directory.** Default output is `custom/plugins/<segment>` under the
  current directory, where `<segment>` strips the name down to letters, digits,
  `-`, and `_`. Without `--force`, scaffolding refuses a directory that exists
  and is non-empty (`Output directory is not empty: …`).
- **Contract reference.** When run inside the repository (a `Callora.Host.sln` is
  found by walking up from the current directory), the generated `.csproj`
  references `src/Core/Callora.Core.csproj` as a `ProjectReference` with
  `Private="false" ExcludeAssets="runtime"`. Outside the repository it emits a
  `PackageReference` to `Callora.Core` version `0.1.0` with
  `ExcludeAssets="runtime"`. In both cases Core is compiled against but **not
  shipped** with the plugin — the host provides it and the plugin's assembly
  load context shares its type identity (REV2 §10.1A).

### Generated files

Given a name `Acme Voice`, the scaffold produces (all names derived via
PascalCase):

| Path | Contents |
| --- | --- |
| `Callora.Plugins.AcmeVoice.csproj` | `net10.0` SDK project, `ImplicitUsings`/`Nullable` enabled, `GenerateDocumentationFile=true` (with `NoWarn=$(NoWarn);1591`), the Core reference above, and `registry.json` copied to output (`PreserveNewest`). |
| `Application/AcmeVoicePlugin.cs` | `public sealed class AcmeVoicePlugin : IHostManagedPlugin` with `PluginId`, `DisplayName`, and no-op `StartAsync`/`StopAsync`. |
| `registry.json` | `contractVersion: v1`, `schemaVersion: 1.0`, `name`, `pluginId`, `version: 0.1.0`, `assemblyFileName`, `entryTypeName`, `capabilities: ["workspace.navigation"]`, one `extensions` entry (`extensionPointId: workspace.navigation.main`, `surface: workspace`), and `dependencies: { "Callora.Core": ">=0.1.0" }`. |

### Example

```bash
callora plugin new "Acme Voice" --id acme-voice --output custom/plugins/acme-voice
# → Plugin scaffold created: /abs/path/custom/plugins/acme-voice
```

See the how-to: [Plugin CLI](/guides/getting-started/plugin-cli) and
[Your first plugin](/guides/getting-started/your-first-plugin).

---

## `plugin test-contract`

Validates a built plugin against the **v1** plugin contract: it reads the
`registry.json` manifest, checks the required fields, then loads the assembly in
an isolated, collectible load context (`PluginInspectionLoadContext`) and
verifies the contract reference and the plugin lifecycle entrypoint. Every issue
is printed to stderr as `[CODE] <message> Fix: <remediation>`; any issue fails
the command with exit `1`.

### Flags

| Flag | Required | Default | Meaning |
| --- | --- | --- | --- |
| `--assembly <path-to-dll>` | **Yes** | — | Path to the built plugin DLL to inspect. |
| `--registry <path-to-registry.json>` | No | `registry.json` next to the assembly. | Explicit path to the manifest. |
| `--entry-type <full-type-name>` | No | Manifest `entryTypeName`, else auto-detected. | Overrides which type is treated as the plugin entrypoint. |

Any unknown option, or a missing value for a known option, fails parsing.
`--assembly` is mandatory (`Option --assembly is required.`).

### Validations performed

**Manifest resolution and shape**

| Failure code | Fires when |
| --- | --- |
| `ASSEMBLY_NOT_FOUND` | The `--assembly` file does not exist. |
| `MANIFEST_NOT_FOUND` | No `registry.json` at the resolved/explicit path. |
| `MANIFEST_PARSE_ERROR` | `registry.json` is empty or is not valid JSON. |

**Required manifest fields** (each checked independently, so several may report
at once)

| Failure code | Fires when |
| --- | --- |
| `MANIFEST_CONTRACT_VERSION_MISSING` | `contractVersion` is absent/blank. |
| `MANIFEST_CONTRACT_VERSION_UNSUPPORTED` | `contractVersion` is present but not `v1` (case-insensitive). |
| `MANIFEST_SCHEMA_VERSION_MISSING` | `schemaVersion` is absent/blank. |
| `MANIFEST_NAME_MISSING` | `name` is absent/blank. |
| `MANIFEST_PLUGIN_ID_MISSING` | `pluginId` is absent/blank. |
| `MANIFEST_VERSION_MISSING` | `version` is absent/blank. |
| `MANIFEST_ASSEMBLY_FILE_NAME_MISSING` | `assemblyFileName` is absent/blank. |
| `MANIFEST_ASSEMBLY_FILE_NAME_MISMATCH` | `assemblyFileName` does not equal the actual `--assembly` file name (case-insensitive). |
| `MANIFEST_ENTRY_TYPE_NAME_MISSING` | `entryTypeName` is absent/blank. |

**Contract compatibility** (assembly loaded and inspected)

| Failure code | Fires when |
| --- | --- |
| `COMPATIBILITY_CONTRACTS_REFERENCE_MISSING` | The assembly does not reference `Callora.Core`. |
| `COMPATIBILITY_CONTRACTS_MAJOR_MISMATCH` | The referenced `Callora.Core` **major** version differs from the host's `IHostManagedPlugin` assembly major. |

**Lifecycle entrypoint** (resolved from `--entry-type`, else manifest
`entryTypeName`, else the first concrete type implementing `IHostManagedPlugin`)

| Failure code | Fires when |
| --- | --- |
| `LIFECYCLE_ENTRYPOINT_NOT_FOUND` | No entrypoint type could be located. |
| `LIFECYCLE_ENTRYPOINT_INVALID` | The resolved type is abstract/an interface or does not implement `IHostManagedPlugin`. |
| `LIFECYCLE_ENTRYPOINT_INSTANTIATION_FAILED` | No public parameterless constructor, or the constructor throws when invoked. |
| `LIFECYCLE_PLUGIN_ID_MISSING` | The instantiated entrypoint's `PluginId` property is empty. |
| `LIFECYCLE_DISPLAY_NAME_MISSING` | The instantiated entrypoint's `DisplayName` property is empty. |

On success, the CLI prints `All contract checks passed.` and exits `0`.

### Example

```bash
callora plugin test-contract \
  --assembly custom/plugins/acme-voice/bin/Release/net10.0/Callora.Plugins.AcmeVoice.dll
# On failure, e.g.:
# [MANIFEST_ASSEMBLY_FILE_NAME_MISMATCH] registry.json assemblyFileName '…' does not match assembly '…'. Fix: Set assemblyFileName to the actual built DLL file name.
```

The manifest fields validated here are documented in
[Registry manifest](/guides/fundamentals/registry-manifest) and
[Extension manifests](/reference/extension-manifests). The `[CalloraInternal]`
contract boundary these checks enforce is described under
[.NET contracts](/reference/dotnet-contracts) and
[Architecture](/concepts/architecture).

---

## `plugin sign`

Produces a signed content manifest (`plugin.signature.json`) over an entire
plugin directory. Every file in the directory (except the signature file itself)
is hashed; the hashes, the signer's public-key fingerprint, and a detached
signature over the canonical serialization form the manifest. Because
`registry.json` is among the hashed files, plugin metadata — capabilities, entry
type — is tamper-evident too, not just the assembly.

### Flags

| Flag | Required | Default | Meaning |
| --- | --- | --- | --- |
| `--plugin <plugin-directory>` | **Yes** | — | Directory of the built plugin (must contain `registry.json`). |
| `--key <private-key.pem>` | **Yes** | — | ECDSA P-256 private key in PEM. Must **not** live inside the plugin directory. |
| `--out <plugin.signature.json>` | No | `<plugin-directory>/plugin.signature.json`. | Output path for the signature manifest. |

Relative paths are resolved against the current directory. Any unknown option, or
a missing value, fails parsing. Both `--plugin` and `--key` are mandatory.

### Cryptography

| Property | Value |
| --- | --- |
| Algorithm id | `ECDSA-P256-SHA256` (`PluginSignatureAlgorithms.EcdsaP256Sha256`) |
| Signature | ECDSA over SHA-256 of the canonical manifest bytes, Base64-encoded |
| Per-file hash | SHA-256 (`PluginContentHasher`) |
| Signer fingerprint | Uppercase hex SHA-256 of the key's `SubjectPublicKeyInfo` — the trust unit stored in the host trust store |
| Schema | `PluginSignatureManifest`: `schemaVersion` (`1.0`), `pluginId`, `version`, `algorithm`, `signerFingerprint`, `files[]` (relativePath + hash), `signature` |

Authenticode is **not** used (it is broken on Linux); this cross-platform ECDSA
manifest is the signing path.

### Behaviour and failure messages

The command fails (exit `1`, message to stderr) when:

- The plugin directory is missing — `Plugin directory not found: …`
- No `registry.json` in the directory — `registry.json was not found in the plugin directory.`
- The key file is missing — `Signing key not found: …`
- `registry.json` cannot be parsed — `registry.json could not be parsed: …`
- `registry.json` lacks `pluginId` or `assemblyFileName` — `registry.json is missing pluginId or assemblyFileName.`
- The declared `assemblyFileName` is not present among the package files — `Declared assembly '…' was not found in the plugin directory.`
- The PEM key cannot be loaded — `Could not load the signing key: …`

On success it writes the manifest and prints
`Plugin signature written: <path>`.

### Example

```bash
callora plugin sign \
  --plugin custom/plugins/acme-voice/bin/Release/net10.0 \
  --key ./keys/publisher.pem
# → Plugin signature written: …/bin/Release/net10.0/plugin.signature.json
```

The resulting `plugin.signature.json` structure is catalogued in
[Extension manifests](/reference/extension-manifests). The host verifies it at
install time against configured trusted signers (see the plugin-security routes
in the [REST API](/reference/rest-api)).
