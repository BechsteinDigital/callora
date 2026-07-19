# Testing & Publishing

This page covers how Callora tests itself (and how you test a plugin), the PublicAPI
baseline workflow that keeps the public surface honest, and how to publish a signed plugin.

## Testing

Callora tests with **xUnit** on .NET 10. The Engineering Rules
require tests to *assert behavior*, not merely touch code paths, and every functional change
to ship with tests (TDD-oriented). There are three test projects, split by what they need:

| Project | Purpose |
| --- | --- |
| `tests/Callora.Core.Tests` | Host + plugin behavior, including slow DB integration tests |
| `tests/Callora.Analyzers.Tests` | The `CAL0001`–`CAL0004` analyzers, against in-memory compilations |
| `tests/TestPlugins/ExportingPlugin` | A real loadable plugin used as a runtime fixture |

Run the suite:

```bash
dotnet test
```

### Fast unit tests vs. slow integration tests

Most tests are in-memory and fast. Integration tests that need a real Postgres use
**`Testcontainers.PostgreSql`** and are marked so they can be filtered and skipped:

```csharp
[Trait("Category", "Slow")]
public sealed class BackgroundJobFencingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();
    private bool _started;

    public async Task InitializeAsync()
    {
        try { await _postgres.StartAsync(); _started = true; }
        catch (Exception) { _started = false; }   // no Docker → skip, don't fail
    }

    [SkippableFact]
    public async Task Reclaimed_lease_rejects_the_stale_worker_write()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        // …
    }
}
```

Two conventions make this robust:

- **`[Trait("Category", "Slow")]`** tags the slow tests so CI (or you) can run only the fast
  set with `dotnet test --filter "Category!=Slow"`.
- **`[SkippableFact]` + `Skip.IfNot(...)`** (from `Xunit.SkippableFact`) turns "no Docker
  available" into a *skipped* test, not a failure — the container spins up in
  `InitializeAsync` inside a try/catch that records whether it started.

Apply the same pattern to a plugin whose tests need its `plugin_<id>` schema: start a
Postgres container, run your `MigrateAsync`, exercise the entities, and gate on
`Skip.IfNot`.

### The analyzer test project

`Callora.Analyzers.Tests` does **not** wire the analyzer as a build analyzer. It references
`Callora.Analyzers` as a normal library and drives it against in-memory C# compilations
through `AnalyzerTestHarness`, which builds a `CSharpCompilation` and toggles the
`CalloraFrameworkAssembly` MSBuild property to test both the framework-exempt and the
plugin-enforced sides of `CAL0001`–`CAL0004`. If you ship a governance analyzer of your own,
this is the pattern to copy.

## The PublicAPI baseline workflow

Every framework assembly runs `Microsoft.CodeAnalysis.PublicApiAnalyzers` against a tracked
baseline, so the public surface cannot drift silently
([Architecture](../concepts/architecture.md#the-publicapi-baseline)). Two files sit next to each
project:

- **`PublicAPI.Shipped.txt`** — the surface released in a shipped version. Pre-1.0 this is
  essentially empty.
- **`PublicAPI.Unshipped.txt`** — everything public added since the last release.

Two diagnostics enforce it, and because `TreatWarningsAsErrors` is on they **fail the
build**:

| ID | Fires when |
| --- | --- |
| **RS0016** | A public symbol exists that is not recorded in either baseline file |
| **RS0017** | A baseline file lists a symbol that no longer exists |

### The loop when you change a public signature

1. Add or change a `public` type/member.
2. Build — `RS0016` (or `RS0017`) fails with the exact declaration line it expects.
3. Add that line to **`PublicAPI.Unshipped.txt`** (the analyzer offers a code fix, "Add to
   public API", that does this for you).
4. Build passes. At release, the shipped surface moves from `Unshipped` to `Shipped`.

For a plugin, the same discipline applies to any **abstraction package** you publish for
other plugins to build against (e.g. `Callora.Plugin.Communication.Abstractions`): give it
its own `PublicAPI.*.txt` baseline so its consumers get a stable, tracked contract. Combined
with the `CAL0003` XML-documentation requirement on the contract surface, this is the .NET
equivalent of Shopware's BC-checker plus enforced API docs.

## Publishing a plugin

Publishing is: build, sign, place, and let the host trust and load it.

### 1. Build

Build your plugin assembly and, if it has a surface UI, the IIFE bundles (via the
[`calloraSurfacePlugin`](admin-extensions.md#a-minimal-surface-plugin) Vite preset — only
`Resources/public/<surface>` is published). Ensure `registry.json` is complete: identity,
`contractVersion`, capabilities, dependencies, and the
[compliance metadata](plugin-development.md#compliance-metadata).

### 2. Sign — the content manifest

Callora is **trusted-in-process by provenance**
(ADR-013); a plugin is trusted
because of **who signed it**. Produce a signed content manifest with the CLI
([details](plugin-development.md#signing--the-content-manifest)):

```bash
callora plugin sign \
  --plugin ./custom/plugins/MyPlugin \
  --key    ./keys/publisher-private.pem
```

This writes `plugin.signature.json` — a SHA-256 hash of every packaged file, plus the
signer's public-key fingerprint, over an ECDSA-P256/SHA-256 signature. Keep the private key
**outside** the plugin directory.

### 3. Trust the signer on the host

The host trusts a plugin when the signer's public-key **fingerprint** is in its trust store
(`ConfiguredPluginSignatureTrustStore`), configured under `backendHost` in `appsettings.json`:

```json
{
  "backendHost": {
    "trustedSigners": [
      {
        "publisherId": "my-company",
        "displayName": "My Company",
        "publicKey": "-----BEGIN PUBLIC KEY-----\n…\n-----END PUBLIC KEY-----"
      }
    ],
    "allowUnsignedPlugins": false,
    "revokedSignerFingerprints": [],
    "revokedContentHashes": []
  }
}
```

On install, `ManifestSignaturePluginPackageVerifier` recomputes every file hash, rejects any
un-manifested (injected) file, checks revocation, resolves the signer's public key by
fingerprint, and verifies the ECDSA signature. An unsigned plugin is rejected unless
`allowUnsignedPlugins` is explicitly true. You can re-verify all installed plugins at
`GET /api/plugins/signature-report`.

### 4. Install and activate

Place the plugin under a discovered root (`custom/plugins` or `custom/static-plugins`) — or
install it live through the operator API — and activate it. Both happen **without a host
restart** ([Plugin Development](plugin-development.md#hot-loading)):

```http
POST /api/plugins/install            # PluginCreate
POST /api/plugins/{pluginId}/activate    # PluginExecute
```

Once active, its listeners, decorators, routes, and views take effect on the next event,
call, request, and render respectively.

> **Marketplace / paid distribution:** the curated marketplace and paid-publisher gating are
> designed but not built. Today's distribution is curated/self-hosted with the signing and
> trust-store model above.
>
> **Status:** planned — marketplace publishing pipeline and community-signed consent flow.
