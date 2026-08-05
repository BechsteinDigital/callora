# Plugin-Signing (Content-Manifest) — Baustein-Plan

Spec: `ops/specs/2026-07-19-plugin-signing-content-manifest-design.md`
Ausführung: ein Baustein pro Branch, je role-dev → role-reviewer → Findings fixen →
volle Suite → ff-merge → push. Reihenfolge ist bewusst: erst signieren können, dann
verifizieren, dann pinnen/widerrufen, dann Policy/UI.

---

## Baustein 1 — Manifest-Modell + kanonische Serialisierung + Signier-CLI

**Ziel:** Ein Plugin-Ordner kann deterministisch signiert werden; das gemeinsame
Modell (von Verifier + CLI geteilt) steht.

**Dateien (Core):** `src/Core/Application/Plugins/Signing/PluginSignatureManifest.cs`,
`PluginSignatureManifestSerializer.cs` (kanonische Bytes, ohne `signature`-Feld),
`PluginContentHasher.cs` (SHA-256 je Whitelist-Datei, Pfad-Containment),
`PluginSignatureAlgorithms.cs` (Konstante `ECDSA-P256-SHA256`).
**Dateien (CLI):** `src/Host/Cli/Application/PluginSigner.cs` + Command-Verdrahtung
`plugin sign --plugin <dir> --key <pem> [--out]`; optional `plugin keygen`.
**Tests:** `tests/Callora.Core.Tests/…/PluginSignatureManifestTests.cs` — Round-trip
Sign→Verify (rein, mit einem im Test erzeugten ECDSA-Key), Kanonisierung stabil
(Key-Reihenfolge/Whitespace irrelevant), Pfad-Containment lehnt `..` ab.

**Akzeptanz:** `callora plugin sign` erzeugt eine `plugin.signature.json`, deren
Signatur mit dem passenden Public-Key verifiziert; Manipulation einer gelisteten
Datei bricht die Hash-Prüfung. Build 0/0, PublicAPI gepflegt.

**Nicht in B1:** kein Verifier im Install-Pfad, kein DI-Swap.

---

## Baustein 2 — Manifest-Verifier + Trust-Store auf Fingerprint + DI-Swap

**Ziel:** Der Install-Gate verifiziert cross-platform gegen das signierte Manifest;
Authenticode ist raus.

**Dateien (Core):**
- NEU `src/Core/Infrastructure/Plugins/ManifestSignaturePluginPackageVerifier.cs`
  (`IPluginPackageSignatureVerifier`): Signaturdatei finden → Hashes prüfen
  (`ContentHashMismatch`) → ECDSA verifizieren (`InvalidSignature`) → Fingerprint
  im Trust-Store (`UntrustedSigner`); `AllowUnsignedPlugins` nur für nicht-system,
  unsignierte Plugins.
- `PluginPackageSignatureErrorCodes`: `+ ContentHashMismatch = "PLUGIN_PACKAGE_CONTENT_HASH_MISMATCH"`.
- `ConfiguredPluginSignatureTrustStore`: Fingerprint-Semantik (SHA-256 SPKI) + Public-Key-Ablage;
  `TrustedPluginSigner`/`BackendTrustedSignerOptions` um den Public-Key erweitern.
- DI: `CalloraHostCompositionExtensions` Zeile 147 → `ManifestSignaturePluginPackageVerifier`.
  Authenticode-Verifier bleibt vorerst als Datei (nicht registriert) ODER wird entfernt
  (im Review entscheiden; wenn entfernt: PublicAPI + Tests anpassen).
**Tests:** valid-signed-trusted → allow; tampered-dll → `ContentHashMismatch`;
untrusted-fingerprint → `UntrustedSigner`; unsigned+system → deny; unsigned+dynamic+
`AllowUnsignedPlugins` → allow(+Warnung). Bevorzugt ohne DB (Verifier ist rein +
Trust-Store aus Options).

**Akzeptanz:** Gate funktioniert auf Linux; kein `PlatformNotSupportedException`-Pfad
mehr; Default deny-by-default bleibt. Build 0/0, volle Suite grün.

**Danach (Ops, separat notiert):** Calloras System-Plugins (Communication) im Release
signieren; Betreiber-Doku für Trusted-Fingerprint + `callora plugin sign`.

---

## Baustein 3 — Content-Hash-Pin (Install→Load) + Revocation

**Ziel:** Manipulation NACH dem Install wird beim Laden erkannt; kompromittierte
Signer/Hashes können widerrufen werden.

**Dateien:**
- Installations-Entität/-Snapshot + Persistence: `ContentHash` (Manifest-Root/DLL-Hash),
  gesetzt beim erfolgreichen Install (EF-Migration wenn nötig).
- Load/Rehydration (`PluginRuntimeRehydrationHostedService` bzw. Activation): Hash gegen
  Platte re-checken → Mismatch = Reject + sichtbarer Fehler (analog `UnloadFailed`).
- `BackendHostOptions`: `RevokedSignerFingerprints[]`, `RevokedContentHashes[]`; Verifier
  + Load prüfen dagegen (`Revoked = "PLUGIN_PACKAGE_REVOKED"`).
**Tests:** Install pinnt Hash; nachträglich veränderte DLL → Load-Reject; widerrufener
Fingerprint → Install- und Load-Reject. (Persistenz-Tests ggf. Testcontainers-Slow.)

**Akzeptanz:** Tamper-nach-Install wird beim Laden gefangen; Revocation greift an
beiden Toren. Build 0/0, Suite grün.

---

## Baustein 4 — Per-Tier-Policy scharf + Admin-Surfacing

**Ziel:** system-tier immer strikt; Betreiber sieht Signatur-/Trust-Status je Plugin.

**Dateien (Backend):** Tier-Policy im Installer/Verifier explizit (system ignoriert
`AllowUnsignedPlugins`); Signatur-Status je Installation im `/api/plugins/installed`
(oder Diagnose-Endpoint): `{ signatureState: signed-trusted|unsigned|untrusted|revoked|hash-mismatch, signerFingerprint }`.
**Dateien (Frontend):** `pluginsApi` um das Status-Feld erweitern; `PluginsListView`
zeigt ein Badge je Plugin + in der Diagnose-Section Signatur-Auffälligkeiten.
**Tests:** Backend — system+unsigned trotz `AllowUnsignedPlugins` → deny; Status-Mapping.
Vitest — Badge-Rendering je Status.

**Akzeptanz:** Betreiber erkennt im Admin, welches Plugin signiert/vertraut/auffällig
ist; system-tier ist nicht per Flag aufweichbar. Build 0/0, Vitest + Suite grün.

---

## Querschnitt
- PublicApiAnalyzers: neue public Symbole in `PublicAPI.Unshipped.txt` in-place ergänzen
  (nie ganze Datei sortieren).
- Jeder Baustein einzeln reviewen; Findings fixen statt nur notieren.
- Nach jedem Baustein Memory aktualisieren (Fortschritt + offene Punkte).
