# Developer Guide

Callora is a domain-neutral plugin platform for .NET 10 — "our own Shopware/Symfony,
for .NET". The **host** is a pure platform: authentication and RBAC, user and plugin
management, a business-event bus, and the dynamic plugin-routing surface. Everything
domain-specific — voice, dialing, contact-center flows, custom UI — lives in **plugins**.

This guide is for the developer building those plugins.

## What you build

A Callora plugin is a normal .NET assembly (plus, optionally, colocated Vue front-end
bundles) that ships with a `registry.json` manifest. It loads into the running host on a
collectible [`AssemblyLoadContext`](../concepts/architecture.md#the-alc-based-plugin-runtime), can be
installed and activated **without a host restart**, and extends the platform through a
small set of sanctioned mechanisms.

## The extension surface at a glance

| Surface | Mechanism | Marker / contract | Guide |
| --- | --- | --- | --- |
| **React to platform activity** | Business-event listeners (mutable / cancelable) | `IBusinessEventListener` | [Events & Jobs](../guides/events-and-jobs.md) |
| **Change platform behavior** | Service decoration (per-call proxy) | `IServiceDecorator<TService>`, `[CalloraExtensible(Decoratable)]` | [Backend Extensions](../guides/backend-extensions.md#service-decoration) |
| **Expose HTTP APIs** | Plugin controllers on dynamic routes | `AdminApiController` / `WorkspaceApiController`, `[CalloraRoute]` | [Backend Extensions](../guides/backend-extensions.md#plugin-controllers-and-dynamic-routing) |
| **Own data** | Custom EF Core entities in an isolated `plugin_<id>` schema | `IPluginDbContextFactory<TContext>` | [Backend Extensions](../guides/backend-extensions.md#custom-ef-entities-and-per-plugin-schemas) |
| **Run background work** | Leased jobs with idempotency and fencing | `IBackgroundJobHandler` | [Events & Jobs](../guides/events-and-jobs.md#the-job-queue) |
| **Gate features** | Capabilities and entitlements (provenance-sourced) | `registry.json` capabilities, `PluginEntitlement` | [Capabilities & Entitlements](../guides/capabilities.md) |
| **Extend the admin shell** | Slots, hooks, service overrides | `window.CalloraAdmin` | [Admin Extensions](../guides/admin/) |
| **Extend tenant-facing surfaces** | Vue views into SSR output | `@callora/surface-sdk` | [Surface Extensions](../guides/surface/) |

For the **complete, always-current catalog** of every sanctioned extension point — grouped by mode
(contribute / decorate / replace) — see the [Extension Points Reference](./extension-points.md). It is
verified against the platform source by a build test, so it never falls behind the code.

## Trust and governance in one sentence

Plugins run **in-process and fully trusted** — .NET has no supported in-process sandbox
(ADR-013) — so safety comes
from **provenance** (signing + curation) and from **compile-time governance** (the
`CAL0001`–`CAL0004` Roslyn analyzers plus a `PublicAPI` baseline), not from a runtime cage.
Read [Architecture](../concepts/architecture.md) before writing your first plugin.

## Where to start

1. [Build your first plugin](../guides/getting-started/your-first-plugin.md) — the end-to-end walkthrough: scaffold, add an endpoint, install, activate, call it.
2. [Architecture](../concepts/architecture.md) — the platform model, the ALC runtime, the governance boundary.
3. [Plugin Fundamentals](../guides/fundamentals/) — the entry class, `registry.json`, the curated context, exporting extensions, configuration, dependencies.
4. [Backend Extensions](../guides/backend-extensions.md) — events, decoration, controllers, data.
5. [Surface Extensions](../guides/surface/) and [Admin Extensions](../guides/admin/) — the two front-end runtimes.
6. [Capabilities & Entitlements](../guides/capabilities.md) and [Events & Jobs](../guides/events-and-jobs.md).
7. [Testing & Publishing](../guides/testing-and-publishing.md) — the test stack, the PublicAPI workflow, publishing a signed plugin.

The generated type reference for the host and first-party plugins lives under
[API Reference](/api/).

> **License model:** Callora is open-core — an AGPL-3.0 core plus an Apache-2.0 SDK.
> Plugins you build against the SDK are yours.
