# Build your first Callora plugin

In this tutorial you'll build a real, runnable Callora plugin from nothing — scaffold it
with the `callora` CLI, add an HTTP endpoint, validate its contract, then **install and
activate it in a running host and `curl` it live**. No host restart, no recompile of the
platform. The plugin is minimal on purpose (a single `GET /api/hello` route), but every
step is the same one you'd use for a production plugin.

## What you'll learn

- How to scaffold a plugin with `callora plugin new` and what each generated file is for
- How the `IHostManagedPlugin` entry contract works and how `StartAsync` registers exports
- How to expose an HTTP API from a plugin by deriving from `AdminApiController` and
  annotating an action with `[CalloraRoute]`
- How to validate your plugin against the host contract with `callora plugin test-contract`
- How to **install** and **activate** a local plugin through the operator API — hot, with
  no restart — and see it respond

::: tip Prerequisites
You'll need:

- The **.NET 10 SDK** (`dotnet --version` → `10.*`)
- A **running Callora host in development mode** on `http://localhost:5000`
  (`dotnet run --project src/Host/Dev/Callora.Host.Dev.csproj`, or the dev stack
  via `docker compose up -d`). Check it with
  `curl http://localhost:5000/health`.
- The **`callora` CLI** — in this repo it's `dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj --`.
  This tutorial uses that form; if you have the CLI installed as a global tool, use
  `callora …` directly.
- **Operator credentials.** In dev, the bootstrap API key
  `callora-local-dev-key-change-me` (from `.env.example`) authenticates as a platform
  operator. That's all the install/activate calls below need.

The dev host also sets `BackendHost__AllowUnsignedPlugins=true`, so a plugin you build
locally installs without signing. (Production requires a trusted signature — see
[Plugin signing](#next-steps).)
:::

---

## Step 1 — Scaffold the plugin

**What & why.** `callora plugin new` writes a compilable plugin project so you never start
from a blank file. Scaffold it **inside the host's plugin directory** (`custom/plugins` in
dev) — that's where the local install resolver looks for plugins by id.

```bash
dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj -- \
  plugin new hello \
  --name "Hello" \
  --id hello \
  --output custom/plugins/Hello
```

**Expected result:**

```text
Plugin scaffold created: /…/callora/custom/plugins/Hello
```

You now have three files:

```text
custom/plugins/Hello/
├─ Callora.Plugins.Hello.csproj   # net10.0 project, one reference: Callora.Plugin.Sdk
├─ src/
│  └─ HelloPlugin.cs              # the IHostManagedPlugin entry point
└─ registry.json                  # the manifest the host reads at install time
```

The scaffolded `registry.json` looks like this — it's the plugin's identity card
(`pluginId`, entry type, assembly file, contract version):

```json
{
  "contractVersion": "v2",
  "schemaVersion": "1.0",
  "name": "Hello",
  "pluginId": "hello",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugins.Hello.dll",
  "entryTypeName": "Callora.Plugins.Hello.HelloPlugin",
  "capabilities": ["workspace.navigation"],
  "extensions": [
    { "extensionPointId": "workspace.navigation.main", "surface": "surface" }
  ],
  "dependencies": { "Callora.Core": ">=0.1.0" }
}
```

::: info One reference, and why it is `Callora.Plugin.Sdk`
The generated `.csproj` carries a single `PackageReference` to **`Callora.Plugin.Sdk`**, at the
same version as the CLI that scaffolded it. The SDK brings the contract surface, the
`CAL0001`–`CAL0004` governance analyzers, and the build rule that keeps platform assemblies out
of your output folder.

That last part used to be a hand-written `ExcludeAssets="runtime"` on a `Callora.Core`
reference — one line that a plugin author removes while restructuring without anything
failing. Nothing does fail, until load time: your copy of Core shadows the host's, the two
stop sharing type identity, and the cast that should work throws. The SDK owns that rule now,
so it cannot be edited away by accident.

You compile **against** the host's contracts and ship none of them. Do **not** add
`CalloraFrameworkAssembly` to your `.csproj` — leaving it at its default is what keeps the
analyzers on.

Inside this repository there are no packages to reference, so the scaffolder emits
`ProjectReference`s to the same pieces with `Private="false"` instead. Same result, different
source.
:::

---

## Step 2 — Understand the entry point

**What & why.** Every plugin has one entry type implementing `IHostManagedPlugin`. The host
calls `StartAsync` on activation (register your exports here) and `StopAsync` on
deactivation. The scaffold gives you an empty one:

```csharp
// custom/plugins/Hello/src/HelloPlugin.cs
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.Plugins.Hello;

public sealed class HelloPlugin : IHostManagedPlugin
{
    public string PluginId => "hello";

    public string DisplayName => "Hello";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
```

The contract is small by design:

| Member | Purpose |
| --- | --- |
| `PluginId` | Stable id; must match `registry.json`'s `pluginId`. |
| `DisplayName` | Shown in host tooling and Admin → Plugins. |
| `StartAsync(context, ct)` | Register runtime exports via `context.Export(typeof(IContract), impl)`. `context.Services` is a curated host service provider. |
| `StopAsync(ct)` | Release resources. Exports are dropped automatically on deactivation. |

Right now the plugin does nothing. Next we give it a visible behavior.

---

## Step 3 — Add an HTTP endpoint

**What & why.** The most tangible thing a plugin can do is answer an HTTP request. You
expose one by writing a controller that derives from **`AdminApiController`**
(operator-facing, no workspace scoping — the simplest scope) and annotating an action with
**`[CalloraRoute]`**. On activation the host reflects over your controller and adds the
route to the live ASP.NET Core endpoint table; on deactivation it removes it again.

An action must have the exact signature
`Task<ApiResult> M(ApiRequest request, CancellationToken ct)`.

Create `custom/plugins/Hello/Application/HelloController.cs`:

```csharp
// custom/plugins/Hello/Application/HelloController.cs
using Callora.Core.Application.Http.Contracts;

namespace Callora.Plugins.Hello;

public sealed class HelloController : AdminApiController
{
    // Permission is left empty → the route requires an authenticated caller,
    // but no extra RBAC permission. That keeps this first run simple.
    [CalloraRoute("GET", "/api/hello", Name = "hello.index")]
    public Task<ApiResult> Get(ApiRequest request, CancellationToken cancellationToken)
    {
        var greeting = new { message = "Hello from the Callora hello plugin!" };
        return Task.FromResult(Ok(greeting));
    }
}
```

::: warning Route your API under a namespace you own
`/api/hello` is fine, but a plugin **cannot** register under a reserved host prefix
(`/api/auth`, `/api/plugins`, `/api/workspaces`, `/api/users`, …). Colliding routes are
rejected when the endpoint table is rebuilt, so a plugin can never shadow a platform
endpoint. Pick a prefix you own — e.g. `/api/hello/...`. See
[Backend Extensions → reserved prefixes](/guides/backend-extensions#reserved-route-prefixes)
for the full list.
:::

Now wire the controller up in `StartAsync` by exporting it under the `IApiController`
contract. Edit `src/HelloPlugin.cs`:

```csharp
// custom/plugins/Hello/src/HelloPlugin.cs
using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.Plugins.Hello;

public sealed class HelloPlugin : IHostManagedPlugin
{
    public string PluginId => "hello";

    public string DisplayName => "Hello";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        context.Export(typeof(IApiController), new HelloController());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
```

That single `Export` call is the whole registration. The host indexes exports by contract
type; because they're added on activation and dropped on deactivation, the route is **hot** —
nothing to pin, nothing to restart.

::: info Other things you can export
`IApiController` is one of four backend extension points. From the same `StartAsync` you can
also export an `IBusinessEventListener` (react to platform activity), an
`IServiceDecorator<T>` (wrap a host service), or an `IPluginDbContextFactory<T>` (own data in
an isolated schema). See [Backend Extensions](/guides/backend-extensions) for all four.
:::

---

## Step 4 — Build and validate the contract

**What & why.** Before installing, compile the plugin and run the contract test kit. It
loads your assembly, checks the entry type against `IHostManagedPlugin`, and cross-checks
`registry.json` — catching contract mistakes here rather than at install time.

```bash
# Build
dotnet build custom/plugins/Hello/Callora.Plugins.Hello.csproj

# Validate against the host contract
dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj -- \
  plugin test-contract \
  --assembly custom/plugins/Hello/bin/Debug/net10.0/Callora.Plugins.Hello.dll \
  --registry custom/plugins/Hello/registry.json
```

**Expected result:**

```text
All contract checks passed.
```

If a check fails, the CLI prints one line per issue in the form
`[CODE] <message> Fix: <remediation>` and exits non-zero — the message tells you exactly
what to change.

---

## Step 5 — Authenticate as an operator

**What & why.** Installing and activating a plugin is a privileged operator action — the
plugin runs as host code, so the host requires an authenticated operator with the plugin
permissions. In dev the simplest credential is the bootstrap **API key**, sent as a header
on every call:

```bash
export CALLORA=http://localhost:5000
export APIKEY="callora-local-dev-key-change-me"
# then add:  -H "X-Callora-Api-Key: $APIKEY"   to each request below
```

That key maps to a platform super-admin, which is all the calls in Step 6 need.

::: info Prefer a login token?
You can instead exchange operator credentials for a bearer token:

```bash
curl -s -X POST "$CALLORA/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"login":"<operator-email>","password":"<password>"}'
# → { "accessToken": "…", "tokenType": "Bearer", "expiresInSeconds": 3600, … }
```

Then send `-H "Authorization: Bearer <accessToken>"` instead of the API-key header. The
operator login (`login` = the operator's email) is what a real UI or CI pipeline would use;
the API key is the fast path for local development.
:::

---

## Step 6 — Install and activate

**What & why.** Two operator API calls take the plugin live. First **install**: the
`install/local` endpoint resolves your plugin by id inside the host's plugin directory,
builds it if needed (`buildIfNeeded: true`), and registers it. Then **activate**: the host
loads the assembly, calls your `StartAsync`, and wires the exported route into the live
endpoint table.

```bash
# 1) Install (resolves + builds the local 'hello' plugin)
curl -s -X POST "$CALLORA/api/plugins/install/local" \
  -H "X-Callora-Api-Key: $APIKEY" \
  -H "Content-Type: application/json" \
  -d '{"pluginId":"hello","buildIfNeeded":true,"requestedBy":"tutorial"}'

# 2) Activate (runs StartAsync → registers /api/hello, no restart)
curl -s -X POST "$CALLORA/api/plugins/hello/activate" \
  -H "X-Callora-Api-Key: $APIKEY" \
  -H "Content-Type: application/json" \
  -d '{"requestedBy":"tutorial"}'
```

**Expected result** — each call returns a success envelope:

```json
{ "isSuccess": true, "pluginId": "hello", "message": "…" }
```

::: warning "Unsigned plugin" rejection?
If install fails complaining the plugin is unsigned, your host isn't in the dev posture.
Confirm `BackendHost__AllowUnsignedPlugins=true` is set (it defaults to `true` in
`docker-compose.yml`). In production you'd sign the plugin and trust the signer instead.
:::

---

## Step 7 — Call your plugin

**What & why.** The route is now live on the running host. Call it — the response is your
controller's JSON.

```bash
curl -s "$CALLORA/api/hello" \
  -H "X-Callora-Api-Key: $APIKEY"
```

**Expected result:**

```json
{ "message": "Hello from the Callora hello plugin!" }
```

That's a plugin you built, installed, and activated into a running host, answering a live
HTTP request — with no platform restart.

To take it back down again, deactivate it (the route disappears immediately):

```bash
curl -s -X POST "$CALLORA/api/plugins/hello/deactivate" \
  -H "X-Callora-Api-Key: $APIKEY" \
  -H "Content-Type: application/json" \
  -d '{"requestedBy":"tutorial"}'
```

---

## Next steps

You've shipped a working plugin end to end. From here:

- **[Backend Extensions](/guides/backend-extensions)** — the other three extension points:
  business-event listeners, service decoration, and per-plugin EF Core schemas.
- **[Plugin entry & lifecycle](/guides/fundamentals/plugin-entry)** — install → activate →
  deactivate → uninstall in depth, and how the host rehydrates plugins on startup.
- **Signing for production** — build the plugin, run `callora plugin sign --plugin <dir>
  --key <private-key.pem>`, and trust the signer in host config so it loads with
  `AllowUnsignedPlugins=false`.
- **[.NET API reference](/api/)** — the exact contract types you used: `IHostManagedPlugin`,
  `IApiController`, `AdminApiController`, `CalloraRouteAttribute`, `ApiResult`.
