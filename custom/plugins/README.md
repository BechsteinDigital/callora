# custom/plugins

Das Installationsziel für **Application-Tier**-Plugins — die, die zur Laufzeit
installiert, aktiviert und aktualisiert werden. Ein Plugin ohne `tier` im
`registry.json` gilt hier als `application` (in `custom/static-plugins` als
`system`, siehe `PluginTierResolver`).

Das Verzeichnis ist im Repository **absichtlich leer**. Es wird zur Laufzeit
befüllt: von der Distribution beim Deployment oder vom Operator über die
Plugin-API. Nichts, was hier landet, gehört in dieses Repository.

Beispiel- und Referenz-Plugins liegen ebenfalls nicht mehr hier. Sie bekommen ein
eigenes Repository, weil sie genau das nachweisen sollen, was ein fremder
Plugin-Autor durchläuft: gegen die veröffentlichten Pakete bauen, gegen den
Vertrag testen, signieren. Ein Plugin, das im selben Repository neben dem Kern
liegt, kann diesen Nachweis nicht erbringen — es kompiliert per
`ProjectReference` und umgeht damit jede Paketgrenze.
