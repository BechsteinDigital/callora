# custom/static-plugins

Mitgelieferte **System/Foundation-Tier**-Plugins (REV2 §3). Sie kommen mit der
Distribution, werden **vor** `custom/plugins` gescannt (eine Foundation lädt also
vor allem, was sie braucht) und sind nicht über den Marketplace installierbar.

Der Tier eines Plugins kommt aus dem `tier`-Feld seines `registry.json`; fehlt es,
gilt hier `system` und in `custom/plugins` `application`.

Hier liegen **Communication** (Voice-Foundation) und **Composer** (der
Flächen-Editor). Beide bekommen eigene, private Repositories und werden als
NuGet-Pakete bezogen — bis dahin liegen sie im Monorepo.
