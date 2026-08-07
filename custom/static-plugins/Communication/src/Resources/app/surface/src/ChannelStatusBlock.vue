<script setup lang="ts">
// Ob überhaupt etwas klingeln kann. Die Frage, die vor jeder anderen steht, wenn es still bleibt —
// und die sonst als Support-Fall bei jemandem landet, der sie in zwei Sekunden beantworten könnte.
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { CallApiError, listChannels, type ChannelView } from './api'

const props = withDefaults(
  defineProps<{
    title?: string
    /** Nur zeigen, wenn etwas nicht stimmt. Für eine Fläche, auf der der Normalfall kein Thema ist. */
    onlyWhenDegraded?: boolean
  }>(),
  { title: 'Leitungen', onlyWhenDegraded: false },
)

const STATUS: Record<string, string> = {
  Disabled: 'deaktiviert',
  Connecting: 'verbindet',
  Up: 'registriert',
  Degraded: 'eingeschränkt',
  Failed: 'getrennt',
}

const channels = ref<ChannelView[]>([])
const error = ref<string | null>(null)

async function reload(): Promise<void> {
  try {
    channels.value = await listChannels()
    error.value = null
  } catch (err) {
    error.value = err instanceof CallApiError ? err.message : (err as Error).message
    channels.value = []
  }
}

// Alle 30 Sekunden: Ein Registrierungswechsel ist selten, und wer gerade telefoniert, soll davon
// nichts merken.
const timer = setInterval(() => void reload(), 30_000)
onUnmounted(() => clearInterval(timer))
onMounted(() => void reload())

const healthy = (channel: ChannelView): boolean => channel.status === 'Up'

const shown = computed(() =>
  props.onlyWhenDegraded ? channels.value.filter((channel) => !healthy(channel)) : channels.value,
)

function statusOf(channel: ChannelView): string {
  return STATUS[channel.status] ?? channel.status
}

function since(channel: ChannelView): string {
  // „Getrennt" ist beunruhigend, „getrennt seit zwei Minuten" ist handhabbar, und „seit gestern"
  // ist ein anderes Gespräch.
  if (!channel.since) {
    return ''
  }

  const at = new Date(channel.since)
  return Number.isNaN(at.getTime()) ? '' : ` seit ${at.toLocaleString()}`
}
</script>

<template>
  <section v-if="shown.length > 0 || error" class="cal-phone">
    <h3 class="cal-phone__title">{{ props.title }}</h3>

    <div v-for="channel in shown" :key="channel.channelId">
      <div class="cal-phone__party">{{ channel.displayName }}</div>
      <div class="cal-phone__meta">{{ statusOf(channel) }}{{ since(channel) }}</div>
      <!-- Der Grund ist in der Domäne bereits redigiert; eine Provider-Meldung kann die
           Zugangsdaten enthalten, an denen sie gescheitert ist. -->
      <div v-if="channel.error" class="cal-phone__error">{{ channel.error }}</div>
    </div>

    <p v-if="error" class="cal-phone__error">{{ error }}</p>
  </section>
</template>
