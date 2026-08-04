<template>
  <DialogRoot :open="open" @update:open="onOpenChange">
    <DialogPortal>
      <DialogOverlay class="palette__overlay" />
      <DialogContent class="palette" aria-label="Befehlspalette" @open-auto-focus.prevent="focusInput">
        <VisuallyHidden>
          <DialogTitle>Suchen und springen</DialogTitle>
          <DialogDescription>Seiten und Aktionen der Administration durchsuchen</DialogDescription>
        </VisuallyHidden>

        <div class="palette__search">
          <CalIcon :icon="Search" size="sm" />
          <input
            ref="inputEl"
            v-model="query"
            class="palette__input"
            type="text"
            placeholder="Seite oder Aktion suchen…"
            aria-label="Suchbegriff"
            @keydown.down.prevent="move(1)"
            @keydown.up.prevent="move(-1)"
            @keydown.enter.prevent="runActive"
          />
          <kbd class="palette__kbd">Esc</kbd>
        </div>

        <div class="palette__results" role="listbox">
          <template v-for="section in sections" :key="section.name">
            <p class="palette__section">{{ section.name }}</p>
            <button
              v-for="item in section.items"
              :key="item.id"
              type="button"
              role="option"
              class="palette__item"
              :class="{ 'is-active': item.id === activeId }"
              :aria-selected="item.id === activeId"
              @click="run(item)"
              @mousemove="activeId = item.id"
            >
              <CalIcon v-if="item.icon" :icon="item.icon" size="sm" />
              <span class="palette__item-label">{{ item.label }}</span>
              <CalIcon v-if="item.to" class="palette__item-go" :icon="CornerDownLeft" size="sm" />
            </button>
          </template>

          <p v-if="!results.length" class="palette__empty">Nichts gefunden für „{{ query }}“.</p>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef, watch } from 'vue'
import {
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
  VisuallyHidden,
} from 'radix-vue'
import { CornerDownLeft, Search } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import CalIcon from '@/core/ui/CalIcon.vue'
import { searchCommands } from './commandSearch'
import type { CommandItem } from './commandItem'

/**
 * Search-and-jump across everything the shell can reach. With a dozen-plus
 * subsystems plus whatever plugins contribute, hunting the sidebar becomes the
 * slow path — this is the fast one, and it is the only navigation surface that
 * scales without redesign as more plugins arrive.
 */
const props = defineProps<{ open: boolean; commands: readonly CommandItem[] }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const router = useRouter()
const query = ref('')
const activeId = ref<string | null>(null)
const inputEl = useTemplateRef<HTMLInputElement>('inputEl')

const results = computed(() => searchCommands(props.commands, query.value))

// Grouped for scanning, but the keyboard walks the flat result order so
// arrow-down never skips across a heading unexpectedly.
const sections = computed(() => {
  const grouped = new Map<string, CommandItem[]>()
  for (const item of results.value) {
    const name = item.section ?? 'Sonstiges'
    const bucket = grouped.get(name)
    if (bucket) {
      bucket.push(item)
    } else {
      grouped.set(name, [item])
    }
  }
  return [...grouped].map(([name, items]) => ({ name, items }))
})

// Typing re-ranks the list, so the highlight moves back to the best match.
watch(results, (list) => {
  activeId.value = list[0]?.id ?? null
})

watch(
  () => props.open,
  (open) => {
    if (open) {
      query.value = ''
      activeId.value = props.commands[0]?.id ?? null
    }
  },
)

function move(delta: number): void {
  const list = results.value
  if (!list.length) {
    return
  }
  const current = list.findIndex((item) => item.id === activeId.value)
  // Wraps around: from the last entry, down lands on the first.
  const next = (current + delta + list.length) % list.length
  activeId.value = list[next].id
}

function run(item: CommandItem): void {
  emit('update:open', false)
  if (item.to) {
    void router.push(item.to)
    return
  }
  item.run?.()
}

function runActive(): void {
  const item = results.value.find((entry) => entry.id === activeId.value)
  if (item) {
    run(item)
  }
}

function onOpenChange(open: boolean): void {
  emit('update:open', open)
}

// Radix would focus the dialog container; the search field is what the operator
// wants under the cursor the moment it opens.
function focusInput(): void {
  void nextTick(() => inputEl.value?.focus())
}
</script>

<style scoped lang="scss">
.palette__overlay {
  position: fixed;
  inset: 0;
  z-index: var(--cal-z-overlay);
  background: var(--cal-overlay-backdrop);
  backdrop-filter: blur(2px);
}

.palette {
  position: fixed;
  top: 14vh;
  left: 50%;
  z-index: var(--cal-z-modal);
  display: flex;
  flex-direction: column;
  width: min(560px, calc(100vw - var(--cal-space-8)));
  max-height: 60vh;
  transform: translateX(-50%);
  background: var(--cal-surface-raised);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
  box-shadow: var(--cal-shadow-xl);
  overflow: hidden;
  animation: palette-in var(--cal-duration-base) var(--cal-ease-out);
}

.palette:focus {
  outline: none;
}

.palette__search {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  padding: 0 var(--cal-space-4);
  height: 48px;
  border-bottom: 1px solid var(--cal-border-subtle);
  color: var(--cal-text-muted);
  flex: none;
}

.palette__input {
  flex: 1;
  min-width: 0;
  border: 0;
  background: transparent;
  color: var(--cal-text);
  font-size: var(--cal-text-base);
  outline: none;
}

.palette__input::placeholder {
  color: var(--cal-text-muted);
}

.palette__kbd {
  padding: 2px var(--cal-space-1);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-xs);
  font-family: var(--cal-font);
  font-size: var(--cal-text-xs);
  color: var(--cal-text-muted);
}

.palette__results {
  flex: 1;
  overflow-y: auto;
  padding: var(--cal-space-2);
}

.palette__section {
  padding: var(--cal-space-2) var(--cal-space-2) var(--cal-space-1);
  font-size: var(--cal-text-xs);
  font-weight: var(--cal-weight-semibold);
  text-transform: uppercase;
  letter-spacing: var(--cal-tracking-wide);
  color: var(--cal-text-muted);
}

.palette__item {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  width: 100%;
  height: 34px;
  padding: 0 var(--cal-space-2);
  border: 0;
  border-radius: var(--cal-radius-sm);
  background: none;
  color: var(--cal-text-secondary);
  font-size: var(--cal-text-md);
  text-align: left;
  cursor: pointer;
}

.palette__item.is-active {
  background: var(--cal-accent-subtle);
  color: var(--cal-accent);
}

.palette__item-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.palette__item-go {
  opacity: 0;
}

.palette__item.is-active .palette__item-go {
  opacity: 0.7;
}

.palette__empty {
  padding: var(--cal-space-6) var(--cal-space-4);
  text-align: center;
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
}

@keyframes palette-in {
  from {
    opacity: 0;
    transform: translate(-50%, -6px) scale(0.98);
  }
}
</style>
