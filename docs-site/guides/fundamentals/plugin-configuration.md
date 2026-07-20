# Plugin Configuration

Plugins rarely hard-code their settings. A voice plugin needs a preferred codec; a mail
integration needs an SMTP host. Callora gives you a single, scoped configuration reader —
`IPluginConfigReader` — that resolves the *effective* value for your plugin at three levels
of specificity, and a matching set of operator endpoints to set those values.

This page covers how config is scoped, how a plugin reads **its own** config in code, and
how operators set values through the REST API.

## What you'll learn

- The three config scopes and their precedence: **distribution → tenant → workspace**
- How to read your own config with `IPluginConfigReader` (typed getters, fallbacks)
- How operators define and set config through the `/api/config` endpoints
- A worked example resolving config inside a service

## How config is scoped and resolved

Every config value lives at one **scope**. Resolution walks from the least specific to the
most specific, so a more specific value wins (`SystemConfigResolver`,
`src/Core/Application/Configuration/SystemConfigResolver.cs`):

| Scope | Applies to | Precedence |
| --- | --- | --- |
| `global` | The whole distribution (all tenants) | Lowest |
| `tenant` | One tenant | Middle |
| `workspace` | One workspace | **Highest** |
| *(definition default)* | Fallback when nothing is set | Used only if no value exists |

The resolver builds the scope chain and applies values least-specific first, so the most
specific scope overrides the rest:

```csharp
// SystemConfigResolver.BuildScopeChain — global first, then tenant, then workspace.
var chain = new List<(string, string)> { (SystemConfigScopes.Global, string.Empty) };
if (!string.IsNullOrWhiteSpace(tenantKey))    chain.Add((SystemConfigScopes.Tenant, tenantKey.Trim()));
if (!string.IsNullOrWhiteSpace(workspaceKey)) chain.Add((SystemConfigScopes.Workspace, workspaceKey.Trim()));
```

So for a key `codec.preferred` on plugin `voip`: a workspace value beats a tenant value,
which beats a global value, which beats the definition's declared default. This is verified
directly in `SystemConfigResolverTests`:

```csharp
// Workspace value wins when set.
Assert.Equal("\"opus\"",  await resolver.ResolveValueAsync("voip", "codec.preferred", "tenant-a", "workspace-a"));
// Tenant value used when workspace is unset.
Assert.Equal("\"G722\"", await resolver.ResolveValueAsync("voip", "codec.preferred", "tenant-a"));
// Global value used when both are unset.
Assert.Equal("\"PCMU\"", await resolver.ResolveValueAsync("voip", "codec.preferred"));
```

::: info
Values are stored as JSON text (`"opus"`, `587`, `true`). The typed getters below unwrap
that for you, so you don't parse JSON by hand.
:::

## Reading your own config

Resolve `IPluginConfigReader` from `context.Services` (or inject it into your services) and
call one of its getters (`src/Core/Application/Configuration/Contracts/IPluginConfigReader.cs`):

```csharp
public interface IPluginConfigReader
{
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        string pluginId, string? workspaceKey = null, CancellationToken cancellationToken = default);

    Task<string?> GetStringAsync(
        string pluginId, string configKey, string? workspaceKey = null, CancellationToken cancellationToken = default);

    Task<bool> GetBoolAsync(
        string pluginId, string configKey, bool fallback = false, string? workspaceKey = null, CancellationToken cancellationToken = default);

    Task<int> GetIntAsync(
        string pluginId, string configKey, int fallback = 0, string? workspaceKey = null, CancellationToken cancellationToken = default);
}
```

The parameters:

- **`pluginId`** — whose config to read. Pass **your own** `PluginId`. (The platform core
  uses the reserved id `"host"` for its own settings, e.g. SMTP.)
- **`configKey`** — the key, conventionally `"<area>.<name>"` (e.g. `mail.smtp.host`,
  `codec.preferred`).
- **`workspaceKey`** — optional. Pass the current workspace to get its scoped value; omit
  it (or pass `null`) to resolve against the global/tenant chain only.

::: tip
Prefer `GetBoolAsync`/`GetIntAsync` with a `fallback` over `GetStringAsync` + manual
parsing. If the key is unset **or** the stored value isn't a valid JSON bool/number, the
fallback is returned — no exceptions, no null handling.
:::

## Worked example: resolving config in a service

The platform's own SMTP sender is the clearest model (`SmtpMailSender`,
`src/Core/Infrastructure/Mail/SmtpMailSender.cs`). It reads several typed keys under the
reserved `"host"` plugin id — the exact same API your plugin uses with its own id:

```csharp
public sealed class SmtpMailSender(IPluginConfigReader configReader, ILogger<SmtpMailSender> logger) : IMailSender
{
    public const string ConfigPluginId = "host";

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var host = await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.host", cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation("No SMTP host configured; mail not sent.");
            return; // Degrade gracefully when unconfigured.
        }

        var port   = await configReader.GetIntAsync(ConfigPluginId, "mail.smtp.port", fallback: 587, cancellationToken: cancellationToken);
        var useSsl = await configReader.GetBoolAsync(ConfigPluginId, "mail.smtp.ssl", fallback: true, cancellationToken: cancellationToken);
        var user   = await configReader.GetStringAsync(ConfigPluginId, "mail.smtp.username", cancellationToken: cancellationToken);
        // ... build and send ...
    }
}
```

For your own plugin, substitute your `PluginId` and (when the operation is scoped to a
workspace) pass the `workspaceKey`:

```csharp
var codec = await configReader.GetStringAsync(
    "voip", "codec.preferred", workspaceKey: currentWorkspaceKey, cancellationToken)
    ?? "PCMA"; // your own last-resort default
```

**Expected behavior:** if a workspace override exists, you get it; otherwise the tenant
value, then the global value, then the definition default declared for the key — and only
if none exist does your `?? "PCMA"` fallback apply.

## Defining and setting config (operator endpoints)

Config **definitions** (the fields an operator can edit — key, label, field type, default)
are declared per plugin; **values** are set per scope. Both are managed under `/api/config`
(`src/Administration/Api/SystemConfigEndpoints.cs`):

| Method & path | Purpose | Permission |
| --- | --- | --- |
| `GET /api/config/definitions?pluginId=…` | List the editable field definitions for a plugin | `ConfigRead` |
| `GET /api/config/effective?pluginId=…&workspaceKey=…` | Resolve the effective values (workspace-aware) | `ConfigRead` |
| `PUT /api/config/values` | Upsert values for one scope | `ConfigUpdate` |

The upsert body:

```json
{
  "pluginId": "voip",
  "scope": "workspace",
  "scopeKey": "workspace-a",
  "valuesByKey": { "codec.preferred": "\"opus\"" }
}
```

Rules enforced by the endpoint:

- `scope` must be `global`, `tenant`, or `workspace`; `scopeKey` is required for `tenant`
  and `workspace`.
- Writing at **global/tenant** scope is operator-only; writing at **workspace** scope
  requires access to that workspace.
- A **`null`** value in `valuesByKey` **deletes** the stored entry, so resolution falls
  back to the next scope.

::: warning
Fields declared with the `secret` field type are encrypted at rest and are **masked** as
`"***"` when read through `GET /api/config/effective`. The plaintext is only available
internally to the host — do not expect to read a secret's real value back through the
operator API. For plugin-managed secrets, use [`ISecretStore`](./exporting-extensions#exported-vs-resolved).
:::

## Next steps

- Where you resolve the reader: **[The plugin context & dependency injection](./dependency-injection)**
- Declaring config field definitions in your manifest: **[The registry manifest](./registry-manifest)** · **[Extension manifests reference](/reference/extension-manifests)**
- Secrets vs. config: **[Compliance Metadata](./compliance-metadata)** · **[Best Practices](./best-practices#keep-secrets-in-isecretstore)**
- Endpoint details: **[REST API reference](/reference/rest-api)**
