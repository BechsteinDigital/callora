<script setup lang="ts">
// Was passiert, wenn das Telefon klingelt. Der Block holt sich nichts — er nennt den Schlüssel,
// den er braucht, und der Auflöser entscheidet, woher der Wert kommt.
import { computed, onUnmounted, ref } from 'vue'
import { acceptCall, CallApiError, rejectCall } from './api'
import { INCOMING_CALL_KEY } from './context-keys'
import { formatDuration, secondsSince, useCallContext } from './useCallContext'

const props = withDefaults(
  defineProps<{
    title?: string
    /** Ob der Ablehnen-Knopf angeboten wird. Wer nicht ablehnen darf, soll ihn nicht sehen. */
    allowReject?: boolean
  }>(),
  { title: 'Eingehender Anruf', allowReject: true },
)

const call = useCallContext(INCOMING_CALL_KEY)
const busy = ref(false)
const error = ref<string | null>(null)

// Eine Sekunde reicht: Es geht darum, wie lange schon jemand wartet, nicht um Millisekunden.
const now = ref(Date.now())
const ticker = setInterval(() => (now.value = Date.now()), 1000)
onUnmounted(() => clearInterval(ticker))

const waiting = computed(() => formatDuration(secondsSince(call.value?.since, now.value)))

async function act(action: (callId: string) => Promise<void>): Promise<void> {
  const current = call.value
  if (!current || busy.value) {
    return
  }

  busy.value = true
  error.value = null
  try {
    await action(current.callId)
  } catch (err) {
    // Ein 403 nennt den fehlenden Anspruch. Ihn zu verschlucken hieße, eine Installation
    // rätseln zu lassen, warum ein Knopf nichts tut.
    error.value = err instanceof CallApiError ? err.message : (err as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section class="cal-phone">
    <h3 class="cal-phone__title">{{ props.title }}</h3>

    <template v-if="call">
      <!-- Der Name zuerst, wenn es einen gibt: Eine Ziffernfolge sagt einem Menschen nichts, und
           der Anrufer wartet, während man sie liest. -->
      <div class="cal-phone__party">{{ call.callerName || call.remoteParty }}</div>
      <div v-if="call.callerName" class="cal-phone__meta">{{ call.remoteParty }}</div>

      <div v-if="call.calledNumber" class="cal-phone__meta">
        angerufen: {{ call.calledNumber }}
      </div>
      <!-- Wer weitergeleitet wurde, hat nicht diese Nummer gewählt — das ändert die Begrüßung. -->
      <div v-if="call.divertedFrom" class="cal-phone__meta">
        weitergeleitet von {{ call.divertedFrom }}
      </div>
      <div v-if="call.verified" class="cal-phone__meta">Rufnummer bestätigt</div>

      <div class="cal-phone__meta">wartet seit {{ waiting }}</div>

      <div class="cal-phone__actions">
        <button
          type="button"
          class="cal-phone__button cal-phone__button--primary"
          :disabled="busy"
          @click="act(acceptCall)"
        >
          Annehmen
        </button>
        <button
          v-if="props.allowReject"
          type="button"
          class="cal-phone__button cal-phone__button--danger"
          :disabled="busy"
          @click="act(rejectCall)"
        >
          Ablehnen
        </button>
      </div>
    </template>

    <!-- Sichtbar leer statt unsichtbar: Ein Block, der im Ruhezustand verschwindet, lässt das
         Layout springen, sobald es klingelt — genau im ungünstigsten Moment. -->
    <p v-else class="cal-phone__meta">Zurzeit klingelt nichts.</p>

    <p v-if="error" class="cal-phone__error">{{ error }}</p>
  </section>
</template>
