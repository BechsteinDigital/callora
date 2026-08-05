# Plugin-Signing: signiertes Content-Manifest (curated / self-hosted)

Status: Design abgestimmt (2026-07-19). Umsetzung Baustein für Baustein mit Review.
Ziel-Trust-Modell (User-Entscheidung): **curated / self-hosted härten** — Callora
signiert die eigenen System-Plugins; der Betreiber installiert Callora-Plugins +
ggf. eigene. **Kein** öffentlicher Dritt-Store nah dran (SBOM / Publisher-Onboarding
sind ausdrücklich Nicht-Ziel).

## Understanding-Summary

- Callora hat bereits ein **deny-by-default Signatur-Gate** beim Install
  (`PluginInstaller` → `IPluginPackageSignatureVerifier.VerifyAsync`): unsigniert
  wird abgelehnt (`AllowUnsignedPlugins`=false per Default), und der Signer muss im
  **Trust-Store** stehen (`IPluginSignatureTrustStore.IsTrusted`), sonst
  `UntrustedSigner`.
- **Kritischer Konstruktionsfehler:** Verifiziert wird über
  `X509Certificate.CreateFromSignedFile` = **Authenticode auf der DLL** — eine
  deprecatete (`SYSLIB0057`), PE/Windows-zentrische API. Der Code fängt bereits
  `PlatformNotSupportedException`. Auf dem **Linux/Docker-Deployment** ist das Gate
  damit realistisch nicht funktionsfähig (blockiert entweder alles oder zwingt zu
  `AllowUnsignedPlugins=true` → Signing aus).
- **Wie Plugins real abgelegt sind** (Communication system-tier, Dialer dynamic):
  ein **Ordner mit `registry.json` + gebauter `.dll`** (`assemblyFileName`) +
  `src/` + `Resources/`. `.nupkg` ist nur ein *Transportweg* (`install/nuget`);
  ausgepackt liegt immer wieder registry.json + DLL auf der Platte. **Die DLL ist
  die geladene Einheit** (ALC), registry.json die Metadaten (pluginId, tier,
  entryTypeName, capabilities).
- Keine unabhängige Content-Integrität: Chain-Build läuft mit
  `RevocationMode.NoCheck`, kein Content-Hash-Pin (das war H7, für Phase 3 vertagt).
- Trust-Status wird im Admin **nicht** angezeigt; der Micro-Frontend-Loader prüft
  Plugin-Assets nicht.

## Assumptions

- Deployment ist **Linux/Docker** (Production-Setup). Alles muss cross-platform sein.
- Der primäre Vertrauensnachweis ist auf **DLL + registry.json** zu verankern (das,
  was real geladen wird), nicht auf dem `.nupkg`-Transport.
- Callora hält einen **Publisher-Signaturschlüssel** (privat; signiert eigene
  System-Plugins im Release/CI). Betreiber tragen erlaubte **Public-Key-Fingerprints**
  in den Trust-Store, um selbst-signierte eigene Plugins zu betreiben. Lokale
  Entwicklung nutzt `AllowUnsignedPlugins` + laute Warnung.
- `sensitiveFields`/`capabilities` in registry.json sind sicherheitsrelevant und
  müssen mitsigniert sein (sonst könnte ein Angreifer sie umschreiben und „signiert"
  bleiben).

## Non-Goals

- Öffentlicher Plugin-Store, Publisher-Identitäts-Verifikation, SBOM, Provenance-Kette,
  Review-Gate, Permission-Consent-Flow — allesamt store-gekoppelt, hier nicht.
- Keyless/Transparency-Log (Sigstore/cosign) — Overkill für curated.
- Erzwungene Passwortänderung / Login-Flow-Themen (anderes Arbeitspaket).

## Decision-Log

1. **Trust-Modell = curated/self-hosted.** (User.) Bestimmt den Umfang: Gate härten,
   nicht Store-Supply-Chain bauen.
2. **Format = signiertes Content-Manifest** (detached Signatur über ein Manifest, das
   die SHA-256 von DLL + registry.json [+ optional publizierte UI-Assets] listet).
   - *Nicht* NuGet-Paketsignatur: deckt nur den nuget-Transportweg, nicht static/local
     und nicht die tatsächlich geladene DLL. Schlechter uniformer Fit.
   - *Nicht* Authenticode: Windows-only, deprecated, auf Linux gebrochen.
   - Content-Manifest arbeitet auf dem, was real auf der Platte liegt → **uniform**
     über static/local/nuget, **cross-platform** (reine .NET-Crypto), **deckt die
     registry.json mit**, und der **Content-Hash-Pin (H7) fällt direkt ab**.
3. **Algorithmus = ECDSA P-256 + SHA-256.** Kompakt, modern, `System.Security.Cryptography.ECDsa`
   cross-platform. Signatur über die kanonischen Bytes des Manifests ohne das
   `signature`-Feld.
4. **Trust-Einheit = Public-Key-Fingerprint** (SHA-256 des SPKI/`ExportSubjectPublicKeyInfo`,
   hex/uppercase) statt Cert-Thumbprint. `IPluginSignatureTrustStore.IsTrusted(fingerprint)`
   behält seine Form; Config-Semantik (`TrustedSigners[].Thumbprint`) wird auf
   Fingerprints umgestellt (Migration: Feld-Bedeutung dokumentiert, alte Thumbprints
   ohne passenden Signaturtyp matchen einfach nicht mehr → deny, fail-closed).
5. **Content-Hash-Pin (H7-Teil 1):** verifizierten DLL-(+Manifest-)Hash beim Install
   in die Installations-Zeile persistieren; bei Load/Rehydration re-checken
   (Tamper-Evidenz nach Install).
6. **Revocation (H7-Teil 2):** konfigurierte widerrufene Key-Fingerprints + widerrufene
   Content-Hashes; geprüft bei Install **und** Load.
7. **Per-Tier-Policy:** `tier=system` = immer strikt (deny-unsigned/untrusted,
   `AllowUnsignedPlugins` greift NICHT). `tier` fehlt/dynamic = deny-by-default mit
   `AllowUnsignedPlugins`-Escape (Dev) + laute Warnung + Admin-Sichtbarkeit.
8. **Signier-Werkzeug = CLI** `callora plugin sign` (im bestehenden `Callora.Host.Cli`
   neben `plugin test-contract`). Signiert einen Plugin-Ordner mit einem Key (PEM).
9. **Admin-Surfacing:** Plugin-Management zeigt je Plugin Signer-Fingerprint +
   Status (trusted / unsigned / untrusted / revoked / hash-mismatch). Install-Metadaten
   tragen `signatureSignerThumbprint` bereits.

## Final Design

### Signaturdatei (neben registry.json): `plugin.signature.json`
```json
{
  "schemaVersion": "1.0",
  "pluginId": "communication",
  "version": "0.2.0",
  "algorithm": "ECDSA-P256-SHA256",
  "signerFingerprint": "<SHA-256(SPKI) hex, uppercase>",
  "files": [
    { "path": "Callora.Plugin.Communication.dll", "sha256": "<hex>" },
    { "path": "registry.json", "sha256": "<hex>" }
  ],
  "signature": "<base64 ECDSA über die kanonischen Bytes des Manifests OHNE 'signature'>"
}
```
- `path` relativ zum Plugin-Root; nur Whitelist (DLL + registry.json [+ definierte
  UI-Asset-Pfade]); `..`/absolute Pfade werden abgelehnt (analog
  `ResolveContainedTargetDirectory`).
- Kanonisierung: deterministische JSON-Serialisierung (sortierte Keys, kein
  Whitespace) des Manifests ohne `signature`.

### Gemeinsames Modell (Core, `Application/Plugins/Signing/`)
- `PluginSignatureManifest` (record), `PluginSignatureManifestSerializer`
  (kanonische Bytes), `PluginContentHasher` (SHA-256 je Datei),
  `PluginSignatureAlgorithm` (ECDSA-P256-SHA256). Rein, unit-testbar; von CLI
  (sign) UND Verifier (verify) geteilt.

### Verifier: `ManifestSignaturePluginPackageVerifier : IPluginPackageSignatureVerifier`
Ersetzt `AuthenticodePluginPackageSignatureVerifier` in der DI
(`CalloraHostCompositionExtensions` Zeile 147). `VerifyAsync(assemblyPath)`:
1. `plugin.signature.json` neben dem Assembly suchen. Fehlt → `UnsignedPackage`
   (bzw. IsValid=true nur wenn `AllowUnsignedPlugins` UND kein system-tier).
2. Manifest lesen, Datei-Hashes neu berechnen, gegen `files[]` prüfen → Mismatch =
   neuer Code `ContentHashMismatch`.
3. Kanonische Bytes bilden, ECDSA-Signatur gegen den Public-Key verifizieren, dessen
   Fingerprint == `signerFingerprint`. Ungültig → `InvalidSignature`.
4. `signerFingerprint` ∈ Trust-Store? sonst `UntrustedSigner`. ∈ Revocation? →
   neuer Code `Revoked`.
5. `IsValid=true`, `SignerThumbprint=signerFingerprint`.

Der Public-Key kommt aus dem Trust-Store-Eintrag (Fingerprint → Key) ODER ist im
Manifest eingebettet und wird nur akzeptiert, wenn sein Fingerprint vertraut ist
(kein Vertrauensgewinn durch Einbettung; Fingerprint-Abgleich bleibt maßgeblich).

### Content-Hash-Pin + Revocation
- Installations-Zeile bekommt `ContentHash` (der Manifest-Root-Hash bzw. DLL-Hash);
  gesetzt beim erfolgreichen Install.
- `PluginRuntimeRehydrationHostedService` / Load-Pfad re-checkt den Hash gegen die
  Platte → Mismatch = Load-Reject + `UnloadFailed`-analoge Sichtbarkeit.
- `BackendHostOptions`: `RevokedSignerFingerprints[]`, `RevokedContentHashes[]`
  (Config). Geprüft bei Install + Load.

### CLI: `callora plugin sign`
`--plugin <dir>` `--key <private.pem>` `[--out plugin.signature.json]`. Liest
registry.json, hasht die Whitelist-Dateien, baut + signiert das Manifest, schreibt
`plugin.signature.json`. Key-Erzeugung: `callora plugin keygen` (optional) oder
Doku für `openssl`/`dotnet`-Snippet.

### Admin-Surfacing
`/api/plugins/installed` (bzw. eine Diagnose) liefert je Plugin Signatur-Status +
Signer-Fingerprint; `PluginsListView` zeigt ein Badge (signiert-vertraut / unsigniert
/ nicht vertraut / widerrufen / Hash-Mismatch). `trusted-signers`-Endpoint existiert.

### Tier-Policy (Zusammenfassung)
| tier | unsigniert | signiert, untrusted | signiert, trusted | revoked/mismatch |
|------|-----------|---------------------|-------------------|------------------|
| system | **deny** (immer) | deny | allow | deny |
| dynamic | deny (allow nur bei `AllowUnsignedPlugins` + Warnung) | deny | allow | deny |

## Bausteine (siehe ops/plans/2026-07-19-plugin-signing-content-manifest.md)
1. Manifest-Modell + kanonische Serialisierung + Hasher (Core, rein) + `callora plugin sign` (CLI).
2. `ManifestSignaturePluginPackageVerifier` + Trust-Store auf Public-Key-Fingerprint + neue Error-Codes; DI-Swap.
3. Content-Hash-Pin (Install persistiert, Load/Rehydration re-checkt) + Revocation-Config.
4. Per-Tier-Policy scharf + Admin-Surfacing (Backend-Status + `PluginsListView`-Badge).

Jeder Baustein: role-dev → role-reviewer → Findings fixen → volle Suite → ff-merge.
Nach Baustein 2 sind Calloras eigene System-Plugins (Communication) im Release zu
signieren (Ops/CI, separat notiert).
