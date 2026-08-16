import { useAuthStore } from '@/core/auth/authStore'
import { resetConfirm } from '@/core/feedback/confirm'
import { resetToasts } from '@/core/feedback/toasts'
import { resetHooks } from '@/core/extensions/hooks'
import { resetPluginNavigation } from '@/core/extensions/pluginNavigation'
import { resetPluginUiLoadResults } from '@/core/extensions/loader'
import { resetExtensions } from '@/core/extensions/registry'
import { resetReplacements } from '@/core/extensions/replaceable'
import { resetServices } from '@/core/extensions/services'
import { resetSurfaceTabs } from '@/core/extensions/surfaceTabs'
import { resetSurfaceNavigation } from '@/core/workspace/surfaceNavigation'
import { resetWorkspaceContext } from '@/core/workspace/workspaceContext'

/**
 * Beendet die Sitzung im Browser: ein Ort, und darunter die Liste dessen, was dabei fällt.
 *
 * Vorher setzte das Abmelden nur `context` auf null, und alles andere blieb im Speicher des Tabs
 * stehen — die Workspace-Auswahl samt ihres `localStorage`-Schlüssels, der Flächenbaum, die
 * offenen Toasts und die Registrierungen der geladenen Plugin-Bundles. Sichtbar wurde das nie,
 * weil die Anmeldung danach die Seite hart neu lädt (`LoginView`, wegen der Bundles). Genau das
 * ist der Punkt: Der Schutz war der Nebeneffekt einer Entscheidung, die aus einem anderen Grund
 * getroffen wurde. Fällt der Grund weg, fällt der Schutz mit, und niemand merkt es (#293).
 *
 * Es gab außerdem zwei Wege hinaus — die Shell und das Benutzermenü — und keiner räumte auf. Ein
 * dritter wäre dazugekommen, ohne dass jemand an die Liste gedacht hätte; deshalb prüft ein Gate
 * in `session.test.ts`, dass jede `reset`-Funktion des Kerns hier steht oder unten begründet
 * fehlt.
 *
 * Die geladenen Skripte selbst bleiben im Dokument — entladen lässt sich keines. Was geht, ist
 * ihr Beitrag zur Oberfläche, und der gehört der Sitzung.
 */
export function endSession(): void {
  useAuthStore().reset()

  resetWorkspaceContext()
  resetSurfaceNavigation()

  resetToasts()
  resetConfirm()

  resetExtensions()
  resetHooks()
  resetServices()
  resetReplacements()
  resetSurfaceTabs()
  resetPluginNavigation()
  resetPluginUiLoadResults()
}

/**
 * Was bewusst NICHT am Sitzungsende hängt.
 *
 * `resetTheme` — die Farbwahl gehört dem Gerät, nicht der Sitzung. Wer sich abmeldet, will kein
 * anderes Farbschema; das Umschalten beim Abmelden wäre ein Fehler, kein Aufräumen.
 */
export const SESSION_RESET_EXEMPTIONS: readonly string[] = ['resetTheme']
