# Webhooks

A webhook pushes a platform event to an **external system** as a signed HTTP POST. When something
happens — a call ends, an invoice is paid — an operator can subscribe a URL, and Callora delivers
the event there: **durably retried**, **HMAC-signed**, and **data-minimized** by default.

Your plugin's role is small and clean: **publish** the event through `IWebhookEventPublisher`.
The host does the rest — matching subscriptions, minimizing the payload, signing it, and
delivering it on the [job queue](./background-jobs). Operators manage the subscriptions through
`/api/webhooks`.

The worked references are the webhook subsystem
(`src/Core/Application/Webhooks/`), the subscription domain
(`src/Core/Domain/Webhooks/WebhookSubscription.cs`), and the endpoints
(`src/Administration/Api/WebhookEndpoints.cs`).

## What you'll learn

- How to publish an event with `IWebhookEventPublisher`
- The subscription model (`WebhookSubscription`) and how matching works
- The operator endpoints under `/api/webhooks`
- Delivery, signing, and retry semantics
- How `sensitiveFields` masks payloads for data minimization — and how a subscription opts out
- A worked subscription and the resulting signed payload

::: tip Prerequisites

- A plugin that can resolve host services from `context.Services` — see
  [Exporting extensions](/guides/fundamentals/exporting-extensions).
- Familiarity with the [job queue](./background-jobs): every delivery is a `webhook.deliver` job.
:::

## Publish an event

Resolve `IWebhookEventPublisher` (`src/Core/Application/Webhooks/Contracts/`) and call
`PublishAsync`:

```csharp
public interface IWebhookEventPublisher
{
    Task PublishAsync(
        string eventName,        // stable dotted name, e.g. "call.ended"
        string? workspaceKey,    // null = platform-wide event
        object payload,          // any serializable object; becomes the "data" field
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed class CallCompletionNotifier(IWebhookEventPublisher webhooks)
{
    public Task OnCallEndedAsync(CallSummary call, CancellationToken ct) =>
        webhooks.PublishAsync(
            eventName: "call.ended",
            workspaceKey: call.WorkspaceKey,
            payload: new
            {
                callId = call.Id,
                direction = call.Direction,
                calleeNumber = call.Callee,   // a sensitive field — masked by default (see below)
                durationSeconds = call.DurationSeconds
            },
            cancellationToken: ct);
}
```

That single call is your whole responsibility. `PublishAsync` matches the event against the
workspace's active subscriptions and enqueues one durable delivery per match — **payload
minimization and signing happen host-side**, so a plugin can't accidentally leak an unsigned or
un-minimized payload.

::: info You usually don't call this directly
Most events reach webhooks *automatically*: when you publish a `IBusinessEvent` on the
[business-event bus](/guides/events-and-jobs), the host's webhook dispatcher already fans it out
to matching subscriptions. Call `IWebhookEventPublisher` explicitly only for an event you want to
webhook but *not* put on the business-event bus.
:::

::: info `IWebhookEventPublisher` is decoratable
The contract is marked `[CalloraExtensible(ExtensionPointMode.Decoratable, …)]`. A plugin can
decorate it via `IServiceDecorator<IWebhookEventPublisher>` to customize delivery (e.g. add a
routing rule) without replacing the host implementation — see
[Backend extensions](/guides/backend-extensions).
:::

## The subscription model

A `WebhookSubscription` (`src/Core/Domain/Webhooks/`) is one operator-managed delivery target:

```csharp
public sealed class WebhookSubscription
{
    public Guid Id { get; set; }
    public string? WorkspaceKey { get; set; }     // null = subscribes across ALL workspaces (operator)
    public string EventName { get; set; }         // filter; "*" matches every event
    public string TargetUrl { get; set; }         // absolute http(s) URL
    public string Secret { get; set; }            // shared HMAC-SHA256 secret (write-only)
    public bool IsActive { get; set; } = true;
    public bool IncludeSensitiveData { get; set; }// opt-in to unmasked payloads (default false)
    // CreatedAtUtc / UpdatedAtUtc …
}
```

Matching (`WebhookDispatcher`): a published event goes to every **active** subscription whose
`EventName` equals the event name **or** is `"*"`, within the event's workspace scope. Each match
becomes one `webhook.deliver` job.

## Manage subscriptions via the API

Operators create and manage subscriptions through `/api/webhooks`
(`src/Administration/Api/WebhookEndpoints.cs`):

| Method & route | Permission | Purpose |
| --- | --- | --- |
| `GET /api/webhooks?workspaceKey=…` | `webhook.read` | List subscriptions (paged) |
| `POST /api/webhooks` | `webhook.manage` | Create a subscription |
| `PUT /api/webhooks/{id}/activation?isActive=…` | `webhook.manage` | Enable / disable |
| `DELETE /api/webhooks/{id}` | `webhook.manage` | Delete a subscription |

Create takes a `CreateWebhookSubscriptionApiRequest`
(`src/Administration/Api/CreateWebhookSubscriptionApiRequest.cs`):

```csharp
public sealed record CreateWebhookSubscriptionApiRequest(
    string EventName,
    string TargetUrl,
    string Secret,
    string? WorkspaceKey,
    bool IncludeSensitiveData = false);
```

The endpoint validates: `eventName` and `secret` are required; `targetUrl` must be an **absolute
http(s)** URL; and `eventName` must match `^[\w.\-*]{1,120}$` — a strict allowlist that rules out
CR/LF header-injection, because the event name is echoed in an HTTP header on delivery.

Responses use `WebhookSubscriptionApiResponse`
(`src/Administration/Api/WebhookSubscriptionApiResponse.cs`) — and, importantly, **never echo the
secret**:

```csharp
public sealed record WebhookSubscriptionApiResponse(
    Guid Id, string? WorkspaceKey, string EventName, string TargetUrl,
    bool IsActive, bool IncludeSensitiveData, DateTimeOffset CreatedAtUtc);
```

::: warning The secret is write-only
List and create responses omit the secret entirely (`ToPublicShape`). It is supplied once on
create and never returned. Store it on the receiving side at creation time — you cannot read it
back from Callora.
:::

## Delivery, signing & retries

Delivery is a `webhook.deliver` background job (`WebhookDeliveryJobHandler`), so it inherits the
queue's durability. Per delivery:

- The request is an HTTP `POST` with `Content-Type: application/json`.
- **`X-Callora-Event`** carries the event name.
- **`X-Callora-Signature`** carries `sha256=<hmac>` — an HMAC-SHA256 of the exact body using the
  subscription's `Secret` (`WebhookSignature`). Receivers verify origin and integrity by recomputing
  the HMAC over the received body.
- Any **non-2xx** response **throws**, so the job queue **retries with backoff**. Deliveries are
  enqueued with **`MaxAttempts: 5`** (`WebhookDispatcher`), then land as a dead letter in
  [`/api/jobs`](./background-jobs#monitoring).
- If the subscription was deleted or disabled while the job was queued, the handler quietly does
  nothing.

::: tip Verify the signature on your receiver
Recompute `HMAC-SHA256(secret, rawBody)`, hex-encode it lowercased, prefix `sha256=`, and compare
to `X-Callora-Signature` in constant time. Reject on mismatch — that's what proves the POST came
from Callora and wasn't tampered with. Sign over the **raw received bytes**, not a re-serialized body.
:::

## Data minimization: `sensitiveFields`

By default, outbound payloads are **masked** before they leave the platform — data minimization
(PLAT-244), so PII isn't shipped to third parties unless explicitly allowed.

`WebhookPayloadMinimizer` recursively walks the JSON body and masks any property whose name is in
the **effective sensitive-field set**, replacing the value: a value longer than 5 chars becomes
`abc***yz` (first 3 + last 2), anything shorter becomes `***`.

The effective set (`SensitivePayloadFieldRegistry`) is:

- a **core baseline** of generic person-related fields —
  `target`, `targetValue`, `targetDisplayName`, `displayName`, `email`; **plus**
- fields each plugin declares in its `registry.json` **`sensitiveFields`** array.

Your plugin declares the field names *it* produces. The Communication plugin's manifest
(`custom/static-plugins/Communication/registry.json`) declares the voice-specific fields, because
the domain-neutral core doesn't know about phone numbers:

```json
{
  "pluginId": "communication",
  "sensitiveFields": [
    "phoneNumber",
    "callerNumber",
    "calleeNumber"
  ]
}
```

The host registers these on install and clears them on uninstall, so the masking set follows the
installed plugins.

::: warning Opt-out is per subscription and explicit
A subscription with `IncludeSensitiveData = true` receives the **unmasked** payload — the operator
consciously opting the endpoint out of minimization. Default (`false`) always masks. There is no
per-field opt-out: it is all-masked or all-raw for a given subscription. Only grant
`IncludeSensitiveData` to endpoints that genuinely need raw PII and are contractually allowed to
receive it — see [Retention & GDPR](/guides/data/retention-and-gdpr).
:::

## Worked example: subscribe and receive

**1. Operator creates a subscription** for call-ended events in workspace `acme`, masked (default):

```http
POST /api/webhooks
Content-Type: application/json

{
  "eventName": "call.ended",
  "targetUrl": "https://crm.example.com/hooks/callora",
  "secret": "whsec_8f3a…",
  "workspaceKey": "acme",
  "includeSensitiveData": false
}
```

Response `201 Created` (secret omitted):

```json
{
  "id": "b1e0…",
  "workspaceKey": "acme",
  "eventName": "call.ended",
  "targetUrl": "https://crm.example.com/hooks/callora",
  "isActive": true,
  "includeSensitiveData": false,
  "createdAtUtc": "2026-07-20T10:15:00+00:00"
}
```

**2. Your plugin publishes** the `call.ended` event (as shown [above](#publish-an-event)).

**3. The receiver gets** a signed POST. Because `calleeNumber` is a Communication-declared
sensitive field and the subscription didn't opt out, its value is masked:

```http
POST /hooks/callora HTTP/1.1
Content-Type: application/json
X-Callora-Event: call.ended
X-Callora-Signature: sha256=9a1c…f0

{
  "event": "call.ended",
  "workspaceKey": "acme",
  "occurredAtUtc": "2026-07-20T10:16:42+00:00",
  "data": {
    "callId": "call_01H…",
    "direction": "inbound",
    "calleeNumber": "+49***21",
    "durationSeconds": 143
  }
}
```

Had the operator set `includeSensitiveData: true`, `calleeNumber` would carry the full number. A
`2xx` response completes the job; anything else triggers a retry (up to 5 attempts).

## Next steps

- [Background jobs](./background-jobs) — the delivery mechanism and how to read dead letters
- [Flows](./flows) — the ad-hoc `webhook.send` flow action for one-off, subscription-less posts
- [Events & jobs](/guides/events-and-jobs) — business events that automatically fan out to webhooks
- [Retention & GDPR](/guides/data/retention-and-gdpr) — data minimization in the wider compliance picture
- [REST API reference](/reference/rest-api) — `/api/webhooks`
