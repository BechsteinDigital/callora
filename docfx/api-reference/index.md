# API Reference

Callora exposes three distinct kinds of reference surface. This section covers
all three and links the generated .NET reference.

## The three reference kinds

- **[REST API](rest-api.md)** — the HTTP surface of the host: ASP.NET minimal
  APIs for the operator/admin console, workspace management, surfaces, the public
  workspace routes, plugin assets and manifests, and authentication. Grouped by
  area with method, path, purpose, and authorization.

- **[.NET contracts](dotnet-contracts.md)** — the compiled boundary a plugin
  builds against. This page orients you to the generated .NET API reference and
  explains the contract boundary: the v1 plugin contract, the
  `[CalloraInternal]` marker, the `PublicAPI` baseline files, and the governance
  analyzers (`CAL0001`–`CAL0004`).

- **[Extension manifests](extension-manifests.md)** — the JSON formats a plugin
  author writes or the platform emits: `registry.json`, the published
  `plugin-ui-assets.manifest.json`, the signed content manifest
  (`plugin.signature.json`), and the `theme.json` token structure.

## The generated .NET reference

The [.NET API reference](../api/index.md) is generated from the XML
documentation of the host platform and the first-party plugins. It is the
authoritative catalogue of types, interfaces, and members. The pages in this
section describe *how to use* those surfaces — the REST endpoints that expose
them over HTTP and the manifest formats that drive them — but do not restate the
type-level detail. When a page mentions a type (for example
`PluginSignatureManifest` or `ICalloraPluginCatalog`), look it up in the
generated reference for its full member list.
