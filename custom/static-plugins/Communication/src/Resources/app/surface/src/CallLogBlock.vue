<script setup lang="ts">
// Was war. Eine Abfrage, kein Ereignis — deshalb holt dieser Block seine Daten selbst, statt
// auf einen Kontext-Schlüssel zu warten, den es für Vergangenes nicht gibt.
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { CallApiError, listCalls, type CallHistoryEntry } from './api'
import { ACTIVE_CALL_KEY } from './context-keys'
import { useCallContext } from './useCallContext'

const props = withDefaults(
  defineProps<{
    title?: string
    /** Wie viele Zeilen. Eine Liste, die scrollt, liest niemand — sie beantwortet „was war eben".  */
    limit?: number
  }>(),
  { title: 'Letzte Anrufe', limit: 10 },
)

const OUTCOMES: Record<string, string> = {
  InProgress: 'läuft',
  Completed: 'beendet',
  Missed: 'nicht angenommen',
  Rejected: 'abgewiesen',
  Failed: 'fehlgeschlagen',
  Interrupted: 'abgebrochen',
}

const calls = ref<CallHistoryEntry[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const activeCall = useCallContext(ACTIVE_CALL_KEY)

async function reload(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    calls.value = await listCalls(props.limit)
  } catch (err) {
    error.value = err instanceof CallApiError ? err.message : (err as Error).message
    calls.value = []
  } finally {
    loading.value = false
  }
}

// Nachladen, wenn ein Gespräch endet: Der eben geführte Anruf ist der, den man in der Liste
// sucht. Ohne das bliebe die Historie stehen, bis jemand die Seite neu lädt.
watch(activeCall, (current, previous) => {
  if (previous && !current) {
    void reload()
  }
})

const timer = setInterval(() => void reload(), 60_000)
onUnmounted(() => clearInterval(timer))
onMounted(() => void reload())

function formatMoment(iso: string): string {
  const at = new Date(iso)
  return Number.isNaN(at.getTime()) ? iso : at.toLocaleString()
}

function outcomeOf(call: CallHistoryEntry): string {
  return OUTCOMES[call.outcome] ?? call.outcome
}
</script>

<template>
  <section class="cal-phone">
    <h3 class="cal-phone__title">{{ props.title }}</h3>

    <p v-if="loading && calls.length === 0" class="cal-phone__meta">Lädt…</p>
    <p v-else-if="calls.length === 0" class="cal-phone__meta">Noch keine Anrufe.</p>

    <table v-else class="cal-phone__table">
      <thead>
        <tr>
          <th scope="col">Zeit</th>
          <th scope="col">Gegenstelle</th>
          <th scope="col">Erreicht</th>
          <th scope="col">Ergebnis</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="call in calls" :key="call.callId">
          <td>{{ formatMoment(call.startedAt) }}</td>
          <td>{{ call.remoteParty }}</td>
          <td>{{ call.localIdentity }}</td>
          <td>{{ outcomeOf(call) }}</td>
        </tr>
      </tbody>
    </table>

    <p v-if="error" class="cal-phone__error">{{ error }}</p>
  </section>
</template>
