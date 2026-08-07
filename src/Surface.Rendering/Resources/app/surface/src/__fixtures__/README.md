# Test-Fixtures

## `demo-plugin-bundle.js`

Ein **echtes**, vom Surface-Preset gebautes Plugin-Bundle: eine IIFE, die `CalloraVue`
entgegennimmt und über `window.calloraSurface.registerView` eine View anmeldet.

Es stammt aus dem SurfaceDemo-Referenzplugin, das mit den Beispiel-Plugins aus dem
Repository gezogen ist. Der Test, der es benutzt (`golden-path.spec.ts`), prüft die Kette
Loader → Skript-Injektion → Registrierung → Mount. Dafür muss das Bundle echte
Preset-Ausgabe sein: Ein von Hand geschriebener `registerView`-Aufruf könnte von dem
abweichen, was das Preset tatsächlich erzeugt, und der Test würde die Abweichung nicht
bemerken.

**Grenze, die dabei bewusst in Kauf genommen wird:** Als eingefrorene Datei wächst das
Bundle nicht mit dem Preset mit. Ändert sich die Aufrufform, die das Preset erzeugt, bleibt
dieser Test grün und die echte Fläche bricht. Wer das Preset anfasst, baut ein Plugin-Bundle
neu und ersetzt diese Datei — das ist der Preis dafür, dass kein Referenzplugin mehr im
Repository liegt.
