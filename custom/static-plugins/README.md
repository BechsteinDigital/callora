# custom/static-plugins

Bundled **System/Foundation-tier** plugins (REV2 §3). Plugins here ship with the
distribution, are scanned **before** `custom/plugins` (so a foundation loads
before anything that depends on it), and are not marketplace-installable.

A plugin's tier comes from its `registry.json` `"tier"` field; a plugin in this
directory defaults to `system` when the field is absent. Plugins in
`custom/plugins` default to `application`.

The Communication foundation will live here once extracted from the host
(Phase 1, WP-2/WP-3).
