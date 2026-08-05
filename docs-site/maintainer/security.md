# Security

## Trust model — trusted-in-process by provenance (ADR-013)

Callora's security model is grounded in a hard technical fact: **.NET has no
supported in-process sandbox.** Code Access Security and AppDomain sandboxing were
removed with .NET Core. `AssemblyLoadContext` isolates *types and versions*, not
*capabilities* — an in-process plugin can use reflection, the filesystem,
P/Invoke, threads, and the network. `internal` / `[CalloraInternal]` are bypassable
by reflection.

Therefore (ADR-013, Accepted 2026-07-14):

- **All plugins run in-process and are trusted.** "Untrusted in-process" is not a
  supported concept.
- **Trust is established by provenance:** package signature + publisher trust +
  (marketplace) vetting + **explicit operator consent at install**. This is the
  Shopware model.
- **Trust tiers**, all in-process/trusted: *System/Foundation* (bundled, e.g.
  Communication), *Verified/Commercial* (signed by Bechstein Digital),
  *Community-signed* (later; install demands explicit "full access" consent).
- `internal`/`[CalloraInternal]` + curated DI are the **defined, safe extension
  surface** (a footgun guard and the contract) — **not** a security boundary
  against malicious in-process code. The guarantee against malicious plugins is
  **governance** (signature/vetting/consent), not the runtime.
- The distribution is **curated/self-hosted**. An open, unvetted third-party
  marketplace is deliberately **not** the goal; an out-of-process sidecar (real
  process sandbox) remains a documented future exit, not built now.

**Operator takeaway:** installing a plugin is a **fully privileged act** — the
plugin runs as host code and its admin bundle as privileged admin-frontend code.
Only install plugins whose author you trust.

## Plugin signing and fingerprint trust

The install gate verifies a **detached signature manifest**
(`plugin.signature.json`, **ECDSA-P256**) against a trusted signer's **public key**
and checks the covered file hashes (assembly + `registry.json`), so a plugin's
capabilities and entry type are tamper-evident. Trust = the **public-key
fingerprint** of a signer you configured.

- **Unsigned plugins are rejected** unless `BackendHost__AllowUnsignedPlugins=true`.
  Dev sets this `true`; **production keeps it `false`**, so every deployed plugin —
  including the bundled system-tier plugins under `custom/static-plugins/` — must
  be signed and its signer trusted, or it will not load.
- Signature standing is shown per plugin in **Admin → Plugins**.

Sign a plugin for production:

```bash
# 1) Generate an ECDSA P-256 keypair (keep the private key secret; publish the public key)
openssl ecparam -name prime256v1 -genkey -noout -out callora-signing.key.pem
openssl ec -in callora-signing.key.pem -pubout -out callora-signing.pub.pem

# 2) Build the plugin, then sign its directory (writes plugin.signature.json next to registry.json)
dotnet build custom/static-plugins/Communication/Callora.Plugin.Communication.csproj
callora plugin sign --plugin <plugin-directory> --key callora-signing.key.pem
```

Trust the signer in host config (the fingerprint is derived from the key):

```jsonc
"BackendHost": {
  "TrustedSigners": [
    { "publisherId": "callora", "displayName": "Callora", "publicKey": "-----BEGIN PUBLIC KEY-----\n…\n-----END PUBLIC KEY-----" }
  ]
}
```

**Revocation.** Revoke a compromised signer via
`BackendHost__RevokedSignerFingerprints` or a specific bad build via
`BackendHost__RevokedContentHashes`. Both are enforced at install **and**, through
runtime rehydration, at load — an already-installed revoked build will not reload.

> **Status:** `callora plugin sign` and the manifest verifier + fingerprint trust
> store are implemented. Signing Callora's own system plugins inside the release
> pipeline is an open operations task, so today production signing is a manual
> step for bundled plugins.

## The hardened template sandbox

Public surfaces are server-side rendered by `Callora.Surface.Rendering`: **Nunjucks
executed on the Jint JS interpreter in a hardened sandbox** (ADR-015). The sandbox
exists specifically to run **untrusted surface templates** safely: no CLR interop,
plus timeout / memory / recursion limits. Jint is pinned in
`Directory.Packages.props` (CVE-audited; keep it current). This is the one place
untrusted content executes — plugin *code* is trusted by provenance, but template
*content* is confined by the sandbox.

## RBAC

- **`SuperAdmin`** — global operator, wildcard permission `*`. Seeded as a system
  role at startup (see [Migration & Rollback](migration-and-rollback.md)).
- **`Admin`** — scoped **per workspace**, not a global operator. The historical
  `admin` role was migrated to `SuperAdmin`.
- **Global identity is operator-only**: changing credentials, deleting an account
  and exporting data-subject data act on the global user across every workspace,
  so they require platform scope. Workspace admins manage `membership.*` in their
  own workspace instead — see [Permissions](../reference/permissions.md).
- Permission keys follow `<function>.<action>` (`create/read/update/delete/execute`);
  effective rights are the union of a principal's roles.

## Local accounts

One `BackendPasswordPolicy` governs every local credential — the bootstrap
operator seed, the demo admin, operator-created accounts and later password
changes. A password below **12 characters** is refused everywhere; a seed that
violates it is skipped with a warning instead of creating a weak super-admin.

- **Lockout**: 10 consecutive failures lock the account for 15 minutes. A success
  clears the counter. The lock is account-scoped and time-bounded; per-IP
  throttling stays with the rate limiter.
- **Deactivation instead of deletion**: `PUT /api/users/{id}/activation` disables
  an account. It keeps its data, memberships and audit trail, authenticates
  nowhere, and its live sessions stop working immediately. Both directions are
  written to the audit trail.
- **MFA**: Callora issues no second factor of its own. Set
  `BackendHost__RequireExternalIdentityForOperators=true` (together with
  `OidcAuthority`) to refuse the local password login for **platform operators**,
  so privileged sign-in happens at an identity provider that does enforce MFA.
  Workspace logins keep working locally.

## Session revocation

Every session this host issues carries a `jti` and the account's **security
stamp**. On each request the stamp is compared against the stored one, so a
change to the account invalidates outstanding tokens instead of waiting out their
one-hour lifetime:

| Event | Effect |
|---|---|
| Password change | All sessions of the account |
| Deactivate / re-enable | All sessions of the account |
| Account deletion | All sessions of the account |
| RBAC role assigned/removed | All sessions of that account |
| Role grants changed/deleted | All sessions of every member of that role |
| Logout | Exactly the session that was used |

Logout records the `jti` in a durable revocation list (an in-memory list would
resurrect logged-out tokens on restart); an hourly job purges entries whose
tokens have expired. Account state is cached for 15 seconds on the request path
and dropped the moment a stamp rotates, so revocation is immediate, not eventual.

Tokens without a security stamp — external OIDC sessions and named integration
credentials — are governed by their own issuer, not by this mechanism.

## CSRF, rate limiting, and API auth

- **Control-plane API auth**: an API key in the `X-Callora-Api-Key` header.
  Every presented key is matched against a known credential — a named
  integration (hashed lookup, own RBAC role) or a configured bootstrap key.
  Unknown keys always return `401`, in every configuration permutation.

### Bootstrap credential lifecycle

The bootstrap key is a **break-glass super-admin credential for first-run setup
only**. Retire it as soon as named integrations exist:

1. **Install** — set `BackendHost__EnableBootstrapApiKeys=true` and put one
   generated key in `BackendHost__ApiKeys__0`. Never ship
   `callora-local-dev-key-change-me`.
2. **Bound it** — set `BackendHost__BootstrapApiKeysExpireAtUtc` to a few hours
   out. The key stops authenticating at that instant even if nobody remembers to
   remove it.
3. **Onboard** — create named integrations via `/api/security/integrations`;
   each gets its own key, RBAC role and scope, never super-admin.
4. **Retire** — clear `BackendHost__ApiKeys__*` **or** set
   `BackendHost__EnableBootstrapApiKeys=false`. Both revoke the credential
   immediately; nothing else needs to change.

`RequireApiKeyAuthentication` is a startup policy switch (it refuses to boot
with bootstrap keys enabled but unconfigured). It has no effect on whether a
presented credential is accepted.
- **CSRF guard** and **rate limiting** protect the operator/admin surface. Rate
  limits partition on the connection's remote address; `X-Forwarded-For` is
  honoured only from a configured trusted proxy
  (`BackendHost__ForwardedHeaders__KnownNetworks__0`) — see
  [Forwarded headers](../reference/configuration.md#forwarded-headers).
- Errors use RFC 9457 ProblemDetails (`application/problem+json`) — no anonymous
  error objects.

> **Status:** A CSRF guard and rate limiting are in place on the admin/operator
> surface; two low-priority hardening follow-ups (login-CSRF, `AllowedHosts`
> tightening) remain open per the security feedback rounds.

## Secrets and config hygiene

- Secret config and webhook secrets are **encrypted in the database**; the
  data-protection keyring lives in the DB (imported from any legacy filesystem
  keys at first startup) and is redacted in API responses.
- **PII** (phone, email, display names) is **masked in logs**.
- The default `JwtSigningKey` **throws outside Development** — supply a real key.
- Keep `.env` out of version control (it is gitignored); use `.env.example` as the
  template. See [Deployment](deployment.md#configuration-hygiene).

## Compliance baseline — DSGVO + EU AI Act

The platform-wide compliance baseline is
`docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md` (technical baseline, not
legal advice). Key operator-facing points:

- **Privacy by design/default, data minimization, purpose binding** documented per
  data flow and per plugin.
- **Data-subject rights are mandatory**: export API (Art. 15/20) and delete paths
  (Art. 17); retention via technical TTL/retention policies per data class.
- **EU data residency** for cloud; for **self-hosted deployments, residency is a
  declared operator responsibility** — you own where the data lives.
- **EU AI Act**: each AI plugin declares a risk classification, purpose, and model
  source; human oversight must be able to intervene/override; AI involvement is
  marked in UI/API; model/prompt/policy/plugin versions are logged revision-safe;
  no release without documented guardrails.
- **Plugin compliance gates**: a plugin manifest declares processed data
  categories, purpose, AI-use + risk class, and required scopes. **Activation only
  proceeds with valid entitlements, a compatible host version, and passed
  compliance checks.** Deactivation stops data flow immediately.
- **Retention sweep**: a background job (`host.retention.cleanup`) runs periodically
  (6h) and prunes per the `Retention` config block (e.g. background jobs after 14
  days, notifications after 90 days).

Audit trails are tamper-resistant; every install-gate decision and lifecycle
change is auditable.
