<script setup lang="ts">
// Das Gespräch, das gerade läuft — und, wenn keines läuft, ein Feld zum Wählen. Ein Telefon,
// das im Ruhezustand nichts kann, ist eine Anzeige und kein Telefon.
import { computed, onUnmounted, ref } from 'vue'
import { CallApiError, hangupCall, placeCall, sendDtmf } from './api'
import { ACTIVE_CALL_KEY } from './context-keys'
import { formatDuration, secondsSince, useCallContext } from './useCallContext'

const props = withDefaults(
  defineProps<{
    title?: string
    /** Ziffernblock während des Gesprächs — für Menüs am anderen Ende. */
    showKeypad?: boolean
    /** Wählfeld im Ruhezustand. Aus, wo eine Fläche nur zeigen und nicht anrufen soll. */
    allowDialing?: boolean
  }>(),
  { title: 'Telefon', showKeypad: true, allowDialing: true },
)

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'] as const

const call = useCallContext(ACTIVE_CALL_KEY)
const busy = ref(false)
const error = ref<string | null>(null)
const dialTarget = ref('')

const now = ref(Date.now())
const ticker = setInterval(() => (now.value = Date.now()), 1000)
onUnmounted(() => clearInterval(ticker))

const duration = computed(() => formatDuration(secondsSince(call.value?.since, now.value)))

async function run(action: () => Promise<unknown>): Promise<void> {
  if (busy.value) {
    return
  }

  busy.value = true
  error.value = null
  try {
    await action()
  } catch (err) {
    error.value = err instanceof CallApiError ? err.message : (err as Error).message
  } finally {
    busy.value = false
  }
}

function hangUp(): void {
  const current = call.value
  if (current) {
    void run(() => hangupCall(current.callId))
  }
}

function press(key: string): void {
  const current = call.value
  if (current) {
    void run(() => sendDtmf(current.callId, key))
  }
}

function dial(): void {
  const target = dialTarget.value.trim()
  if (!target) {
    error.value = 'Bitte eine Nummer eingeben.'
    return
  }

  void run(async () => {
    await placeCall(target)
    dialTarget.value = ''
  })
}
</script>

<template>
  <section class="cal-phone">
    <h3 class="cal-phone__title">{{ props.title }}</h3>

    <template v-if="call">
      <div class="cal-phone__party">{{ call.remoteParty }}</div>
      <div class="cal-phone__meta">{{ call.state }} · {{ duration }}</div>

      <div class="cal-phone__actions">
        <button
          type="button"
          class="cal-phone__button cal-phone__button--danger"
          :disabled="busy"
          @click="hangUp"
        >
          Auflegen
        </button>
      </div>

      <div v-if="props.showKeypad" class="cal-phone__keypad">
        <button
          v-for="key in KEYS"
          :key="key"
          type="button"
          class="cal-phone__button"
          :disabled="busy"
          @click="press(key)"
        >
          {{ key }}
        </button>
      </div>
    </template>

    <template v-else-if="props.allowDialing">
      <label class="cal-phone__meta" for="cal-phone-dial">Nummer</label>
      <input
        id="cal-phone-dial"
        v-model="dialTarget"
        class="cal-phone__input"
        type="tel"
        placeholder="+49 30 12345678"
        @keyup.enter="dial"
      />
      <div class="cal-phone__actions">
        <button
          type="button"
          class="cal-phone__button cal-phone__button--primary"
          :disabled="busy"
          @click="dial"
        >
          Anrufen
        </button>
      </div>
    </template>

    <p v-else class="cal-phone__meta">Kein Gespräch.</p>

    <p v-if="error" class="cal-phone__error">{{ error }}</p>
  </section>
</template>
