import { registerBlock, registerBlockCategory } from '@callora/surface'
import ActiveCallBlock from './ActiveCallBlock.vue'
import CallLogBlock from './CallLogBlock.vue'
import IncomingCallBlock from './IncomingCallBlock.vue'
import { ACTIVE_CALL_KEY, INCOMING_CALL_KEY } from './context-keys'
import './blocks.css'

/**
 * The telephone blocks an operator can place on a surface.
 *
 * `requires` is not documentation: the server only sends a context key when a visible block on
 * that surface asked for it, so a key nobody declared never leaves the host. Getting it wrong
 * costs a panel that never updates, and nothing that says why.
 */

const CATEGORY = 'communication'

registerBlockCategory({
  id: CATEGORY,
  label: 'Telefonie',
  order: 100,
})

registerBlock({
  id: 'communication.incoming-call',
  label: 'Eingehender Anruf',
  description: 'Zeigt einen klingelnden Anruf und lässt ihn annehmen oder ablehnen.',
  category: CATEGORY,
  requires: [INCOMING_CALL_KEY],
  controls: {
    title: {
      type: 'text',
      label: 'Überschrift',
      default: 'Eingehender Anruf',
      group: 'Inhalt',
    },
    allowReject: {
      type: 'toggle',
      label: 'Ablehnen anbieten',
      description: 'Aus, wo ein Anruf angenommen werden muss statt abgewiesen zu werden.',
      default: true,
      group: 'Verhalten',
    },
  },
  component: IncomingCallBlock,
  order: 10,
})

registerBlock({
  id: 'communication.phone',
  label: 'Telefon',
  description: 'Das laufende Gespräch mit Auflegen und Ziffernblock — und ein Wählfeld, wenn keines läuft.',
  category: CATEGORY,
  requires: [ACTIVE_CALL_KEY],
  controls: {
    title: {
      type: 'text',
      label: 'Überschrift',
      default: 'Telefon',
      group: 'Inhalt',
    },
    showKeypad: {
      type: 'toggle',
      label: 'Ziffernblock im Gespräch',
      description: 'Für Menüs am anderen Ende.',
      default: true,
      group: 'Verhalten',
    },
    allowDialing: {
      type: 'toggle',
      label: 'Wählen erlauben',
      description: 'Aus, wo eine Fläche nur zeigen und nicht anrufen soll.',
      default: true,
      group: 'Verhalten',
    },
  },
  component: ActiveCallBlock,
  order: 20,
})

registerBlock({
  id: 'communication.call-log',
  label: 'Anrufliste',
  description: 'Die letzten Anrufe des Workspaces, mit erreichter Nummer und Ergebnis.',
  category: CATEGORY,
  // Kein `requires`: Vergangenes ist eine Abfrage, kein Kontext. Der Block hört trotzdem auf das
  // Ende eines Gesprächs — aber über einen Schlüssel, den ohnehin das Telefon deklariert.
  controls: {
    title: {
      type: 'text',
      label: 'Überschrift',
      default: 'Letzte Anrufe',
      group: 'Inhalt',
    },
    limit: {
      type: 'number',
      label: 'Anzahl Zeilen',
      description: 'Eine Liste, die scrollt, liest niemand.',
      default: 10,
      min: 1,
      max: 100,
      group: 'Inhalt',
    },
  },
  component: CallLogBlock,
  order: 30,
})
