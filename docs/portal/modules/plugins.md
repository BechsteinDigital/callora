# Runtime Plugins

Callora bietet jetzt ein echtes Runtime-Plugin-Modell mit Lifecycle ohne Prozessneustart:

- `install` (Assembly registrieren, persistent speichern)
- `activate` (Plugin starten + Exports live schalten)
- `deactivate` (Plugin stoppen, installiert lassen)
- `uninstall` (Registry-Eintrag entfernen)

Die Runtime-Aufloesung bleibt ueber die bestehenden Modul-Facades stabil:

- `client.ConferenceManager`
- `client.PlaybackManager`
- `client.RecordingManager`
- `client.RealtimeManager`
- `client.WebSocketManager`

Diese Facades lesen dynamisch aus dem Plugin-Katalog. Wird ein Plugin aktiviert/deaktiviert, ist die Aenderung sofort wirksam.

Der Plugin-Katalog unterstuetzt mehrere Exporte je Vertragstyp.
Damit koennen mehrere Plugins parallel dieselbe Extension-Surface bedienen
(z. B. mehrere UI- oder Integrationsprovider).

## Beispiel

```csharp
var install = await client.ModuleManager.InstallAsync(
    "/opt/voipsdk/plugins/Acme.PlaybackPlugin.dll",
    entryTypeName: "Acme.Playback.PluginEntry");

if (!install.IsSuccess || install.Plugin is null)
    throw new InvalidOperationException(install.Message);

var activate = await client.ModuleManager.ActivateAsync(install.Plugin.PluginId);
if (!activate.IsSuccess)
    throw new InvalidOperationException(activate.Message);

// Plugin ist jetzt live.
// ...

await client.ModuleManager.DeactivateAsync(install.Plugin.PluginId);
await client.ModuleManager.UninstallAsync(install.Plugin.PluginId);
```

## Persistenz

Aktuell gibt es zwei Schichten:

- Runtime-Registry der Engine (`Callora.Hosting`) via Datei (`registry.json`)
- Host-Entity-Registry (Backend) via SQLite (`plugin_installations` + `plugin_audit_logs`)

Engine-Pfad und Verhalten werden ueber `CalloraHostingOptions` gesteuert:

- `PluginDirectory`
- `PluginRegistryFilePath`
- `AutoLoadPlugins`
- `AutoActivateInstalledPlugins`

Host-Pfad wird ueber `BackendHost:DatabasePath` gesteuert.

## Paket-Metadaten (`registry.json`)

Jedes Plugin soll eine `registry.json` neben der Plugin-Assembly ausliefern
(vergleichbar mit `composer.json` als Paket-Metadatenquelle).

Mindestens:

- `schemaVersion`
- `name`
- `pluginId`
- `version`
- `assemblyFileName`
- `entryTypeName`

Optional:

- `capabilities`
- `dependencies`

Beim Host-Install liest die Backend-API diese Datei automatisch und nutzt
`entryTypeName`, falls der Request keinen `entryTypeName` mitliefert.

## Zwei Installpfade (Shopware-analog)

1. `Folder/Assembly`: direkte Installation ueber DLL-Pfad (`/api/plugins/install`)
2. `NuGet-Package`: Installation ueber Paketkoordinaten (`/api/plugins/install/nuget`)

Der NuGet-Pfad resolved aktuell aus dem lokalen NuGet-Cache und geht danach in
denselben Runtime-Lifecycle (install/activate/deactivate/uninstall).

## UI-Erweiterungen

Plugins koennen Admin- und Workspace-Erweiterungen liefern:

- `Admin UI`: Betreiberfunktionen (z. B. Mandanten-/Betriebssichten)
- `Workspace UI`: Agenten-/Nutzersichten (z. B. Dialer, Kampagnen, Contact-Center-Funktionen)

Der Begriff `Storefront` wird im Callora-Kontext nicht verwendet.

## Grenze zur Zielarchitektur

Im Sinne von `Callora_Targetstruktur_fuer_KI.md` ist das Runtime-Plugin-Lifecycle-Fundament jetzt vorhanden.
Fuer produktives SaaS-/Marketplace-Betriebsmodell fehlt weiterhin die Backend/API-Schicht:

- signierte Plugin-Pakete und Vertrauenskette
- zentrale Plugin-Katalog-API (Versionen, Kompatibilitaet, Rollout)
- Entitlement-Backend fuer tenant-spezifische Aktivierungen
- Telemetrie/Audit fuer install/activate/deactivate/uninstall
