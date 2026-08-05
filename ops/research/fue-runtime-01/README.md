# FUE-RUNTIME-01: Nachweisakte

- Stand: 2026-07-15
- Status: Arbeits- und Rekonstruktionsentwurf
- Antragsteller im Arbeitsmodell: Einzelunternehmen des Inhabers
- Vorhaben: workspace-spezifisch rekonfigurierbare Plugin-Control-Plane mit deterministischem
  Teardown fuer ASP.NET Core

## Zweck

Diese Akte trennt den fachlichen FuE-Gegenstand, den Arbeitsplan, technische Evidenz und die
persoenliche Stundenaufzeichnung. Sie ist noch kein unterschriftsreifer Nachweis und keine Rechts-
oder Steuerberatung.

Die Rekonstruktion darf erst als persoenlicher Stundennachweis verwendet werden, nachdem der
Inhaber fuer jeden Eintrag bestaetigt hat, dass er die angegebene Zeit selbst und an der konkret
bezeichneten FuE-Taetigkeit gearbeitet hat. Laufzeit autonomer Werkzeuge, Builds oder KI-Agenten
ist keine Eigenleistung des Inhabers.

## Dokumente

- [Projektabgrenzung](project-boundary.md)
- [FuE-Arbeitsplan](work-plan.md)
- [Technischer Nachweisindex](evidence-index.md)
- [Rekonstruktionsentwurf 2026](hours/2026-reconstruction-draft.md)
- [Stundennachweis-Vorlage](hours/fue-timesheet-template.csv)
- [April-Entwurf](hours/2026-04-reconstruction-draft.csv)
- [Juli-Entwurf](hours/2026-07-reconstruction-draft.csv)
- [Gesamte Vorpruefung](../2026-07-15-forschungszulage-vorpruefung.md)

## Verbindliche Trennung

In den steuerlichen Stundennachweis gehoeren nur persoenlich geleistete, unmittelbar dem
FuE-Vorhaben dienende Taetigkeiten. Insbesondere bleiben Routineentwicklung, Produktfunktionen,
UI, Betrieb, allgemeine Dokumentation, Security-Baseline, Kundenarbeit und Pflegezeiten ausserhalb
des FuE-Stundennachweises.

Kalenderdaten koennen intern zur Plausibilisierung verwendet werden. Der eigentliche Nachweis ist
die projekt- und tagesbezogene Stundenaufzeichnung. Belege werden nach den amtlichen Hinweisen
grundsaetzlich vorgehalten und nur auf Anforderung vorgelegt.

## Naechster Freigabeschritt

1. Jede rekonstruierte Zeile gegen eigene Erinnerung und vorhandene Quellen pruefen.
2. Unzutreffende Stunden streichen oder reduzieren; niemals zur Zielsummenerreichung verteilen.
3. Den tatsaechlichen sachlichen Projektbeginn bestaetigen.
4. Danach den Status der bestaetigten Zeilen von `ENTWURF` auf `BESTAETIGT` setzen.
5. Ab sofort neue FuE-Zeit zeitnah in der Vorlage erfassen.

