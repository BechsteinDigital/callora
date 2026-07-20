# Secrets

Credentials don't belong in code or in `appsettings.json`, and they must never be committed
to a repository. When your plugin needs an SMTP password, a VoIP provider token, or any other
externally-supplied secret, you read it at runtime from the host's secret store — and when you
*store* sensitive values yourself, you encrypt them at rest.

This page distinguishes the two: `ISecretStore` for reading operator-provided secrets, and
`IPluginDataProtector` for protecting values you persist. It grounds both in real code.

## What you'll learn

- How to read a named secret at runtime with `ISecretStore`
- Where the host resolves secrets from (environment, configuration) and the naming convention
- The difference between reading a secret and encrypting one you store
- A worked example: an SMTP password / VoIP provider token

::: tip Prerequisites

- A running plugin with `context.Services` access — see [Plugin entry](/guides/fundamentals/plugin-entry).
- Deployment access to set an environment variable or host configuration value for the secret.
:::

## Two mechanisms, two jobs

| You want to… | Use | Where the value comes from |
| --- | --- | --- |
| **Read** an operator-provided credential at runtime | `ISecretStore` | Environment variable / host configuration |
| **Store** a sensitive value your plugin holds | `IPluginDataProtector` | Encrypted by the host, kept in your storage |

Don't confuse them. If an operator configures your SMTP password in the environment, *read*
it with `ISecretStore`. If a user types a SIP secret into your admin UI and you persist it,
*protect* it with `IPluginDataProtector` before it hits your schema.

## Reading a secret — `ISecretStore`

`ISecretStore` (`src/Core/Application/Secrets/Contracts/`) is deliberately tiny:

```csharp
public interface ISecretStore
{
    // Returns the secret value, or null when it is not configured.
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
```

Resolve it from `context.Services` and read your named secret:

```csharp
var secrets = (ISecretStore)context.Services.GetService(typeof(ISecretStore))!;

var smtpPassword = await secrets.GetSecretAsync("SmtpPassword", ct);
if (smtpPassword is null)
{
    // Not configured — fail fast or degrade; never fall back to a hard-coded value.
    throw new InvalidOperationException("Secret 'SmtpPassword' is not configured.");
}
```

**Expected behavior:** `GetSecretAsync` returns the configured value, or `null` when the
secret isn't set anywhere. Treat `null` as "not configured" and fail loudly — never
substitute a default credential.

### Where secrets come from

The host wires `ISecretStore` as a `ChainedSecretStore` over an ordered chain
(`src/Core/Infrastructure/DependencyInjection/CalloraHostCompositionExtensions.cs`); the first
provider that returns a non-null value wins. Out of the box the chain includes:

- **`EnvironmentSecretStore`** — reads `CALLORA_SECRET_<NAME>` environment variables. The name
  is uppercased and non-alphanumerics become underscores, so `GetSecretAsync("SmtpPassword")`
  reads `CALLORA_SECRET_SMTPPASSWORD`.
- **`ConfigurationSecretStore`** — reads from host `IConfiguration`.

```bash
# Provide the SMTP password to the host process — never in the repo.
export CALLORA_SECRET_SMTPPASSWORD='s3cr3t-app-password'
```

::: info Extensible to a vault
`ChainedSecretStore` resolves an ordered provider chain, so a vault-backed provider can be
added ahead of environment/configuration without any plugin change. Your plugin just calls
`GetSecretAsync` and doesn't care where the value came from.
:::

::: warning `ISecretStore` is host infrastructure, not a plugin write API
`ISecretStore` is marked `[CalloraInternal]` — it is a host enforcement point for **reading**
operator-provisioned secrets, not a contract for plugins to *write* their own secrets into.
To persist a secret your plugin owns, protect it and store it yourself (below). Secrets never
live in the repository.
:::

## Storing a secret — `IPluginDataProtector`

When your plugin *holds* a sensitive value — a SIP account secret a user entered, an OAuth
refresh token — encrypt it at rest with `IPluginDataProtector`
(`src/Core/Application/Secrets/Contracts/`):

```csharp
public interface IPluginDataProtector
{
    string Protect(string pluginId, string plaintext);
    bool TryUnprotect(string pluginId, string protectedValue, out string plaintext);
}
```

Protected payloads are **isolated per plugin** — a value protected for one plugin can't be
unprotected for another. From `EfSipAccountStore` (the VoIP provider secret), encrypted before
it is written to the column and decrypted when read back:

```csharp
// Store: encrypt before persisting.
var row = new SipAccount
{
    WorkspaceKey = key,
    SipAccountId = id,
    ProtectedSecret = dataProtector.Protect(CommunicationPlugin.Id, request.Secret),
    // …
};
await db.SaveChangesAsync(cancellationToken);

// Read: decrypt, tolerating legacy plaintext.
private string UnprotectSecret(string storedSecret) =>
    dataProtector.TryUnprotect(CommunicationPlugin.Id, storedSecret, out var plaintext)
        ? plaintext
        : storedSecret;
```

**Expected behavior:** the column stores ciphertext, never the raw secret. `TryUnprotect`
returns `false` for a value that wasn't protected for your plugin (e.g. a legacy plaintext
value) — keep it readable and re-encrypt on the next write. See
[Data store — protecting sensitive values](./data-store#protecting-sensitive-values).

## Worked example — a VoIP provider token end to end

1. **The operator provisions a service credential** your plugin needs to reach an external
   API — set it in the environment:

   ```bash
   export CALLORA_SECRET_VOIPPROVIDERTOKEN='provider-api-token'
   ```

2. **Your plugin reads it at runtime** — never hard-coded, never logged:

   ```csharp
   var token = await secrets.GetSecretAsync("VoipProviderToken", ct)
       ?? throw new InvalidOperationException("VoipProviderToken is not configured.");
   ```

3. **A per-account secret the user enters** (a SIP password) is a *different* secret — your
   plugin owns it, so you `Protect` it and store the ciphertext in your schema, as above.

::: warning Never do these

- Never commit a secret to the repository or bake it into `registry.json` / `appsettings.json`.
- Never log a secret value, even at debug level.
- Never store a user-entered secret as a plain column — protect it first.
:::

## Next steps

- Encrypting values you store, in context: **[Data store](./data-store#protecting-sensitive-values)**
- Storing protected secrets as typed columns: **[Entities & schemas](./entities-and-schemas)**
- Redacting personal fields from outbound webhooks (`sensitiveFields`): **[Registry manifest](/guides/fundamentals/registry-manifest)** · **[Compliance metadata](/guides/fundamentals/compliance-metadata)**
- Plugin configuration (non-secret settings): **[Plugin configuration](/guides/fundamentals/plugin-configuration)**
- Contract signatures: **[.NET API](/api/)**
