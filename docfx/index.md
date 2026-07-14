# Callora Documentation

Callora is an extensible voice, AI, and communication platform. The host is a
pure platform — authentication and RBAC, user and plugin management, and a
business-event bus — while everything domain-specific lives in plugins.

## Sections

- **[API Reference](api/index.md)** — generated from the XML documentation of
  the host platform and the first-party plugins (VoIP, Dialer).

## Architecture at a glance

- **Host platform** (`src/Host`) — RBAC (SuperAdmin global, Admin per
  workspace), plugin runtime on collectible assembly load contexts, the
  business-event bus, and the dynamic plugin routing surface.
- **Contracts** (`src/Contracts`, `src/Host/PluginContracts`) — the ASP.NET-free
  boundary plugins build against.
- **First-party plugins** (`custom/plugins`) — VoIP (the full call stack) and
  Dialer, each with its own EF Core schema.

## Extension points

Plugins extend Callora through several mechanisms: business-event listeners,
service decoration, plugin controllers with dynamic routing, and their own EF
Core entities in an isolated `plugin_<id>` schema.

> This documentation is generated with [DocFX](https://dotnet.github.io/docfx/).
> Build it locally with `dotnet tool restore` followed by
> `dotnet docfx docfx/docfx.json --serve`.
