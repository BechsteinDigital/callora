<template>
  <!--
    Nur sichtbar, wenn es tatsächlich etwas zu wählen gibt. Heute ist das der Betreiber der
    Instanz: Er erreicht alle drei Ebenen. Alle anderen SIND ihr Bereich — der steht im Token,
    und dorthin zu wechseln, wo man nicht ist, wäre keine Umschaltung, sondern eine neue Sitzung.
  -->
  <div v-if="areas.length > 1" class="area-switcher">
    <CalIcon class="area-switcher__icon" :icon="Layers3" size="sm" />
    <CalSelect
      :model-value="active ?? ''"
      name="active-area"
      size="sm"
      :aria-label="t('admin.area.switcher', 'Bereich')"
      @update:model-value="onSelect"
    >
      <option v-for="area in areas" :key="area" :value="area">{{ AREA_LABELS[area] }}</option>
    </CalSelect>
  </div>

  <!--
    Sonst der Bereich als Text, nicht als Steuerelement. Ein Auswahlfeld mit genau einem Eintrag
    sieht aus wie eine Wahl und ist keine; die Beschriftung sagt dasselbe, ohne etwas zu
    versprechen.
  -->
  <div v-else-if="active" class="area-switcher area-switcher--fixed">
    <CalIcon class="area-switcher__icon" :icon="Layers3" size="sm" />
    <span class="area-switcher__label">{{ AREA_LABELS[active] }}</span>
    <span v-if="subject" class="area-switcher__subject">{{ subject }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Layers3 } from 'lucide-vue-next'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import { useAuthStore } from '@/core/auth/authStore'
import { t } from '@/core/i18n/i18n'
import { AREA_LABELS, currentAreaSubject, type AreaId } from './area'
import { useAreaContext } from './areaContext'

const auth = useAuthStore()
const { areas, active, setActive } = useAreaContext()

const subject = computed(() => currentAreaSubject(auth.context.value))

function onSelect(value: string): void {
  setActive(value as AreaId)
}
</script>

<style scoped lang="scss">
.area-switcher {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2, 0.5rem);
}

.area-switcher__icon {
  color: var(--cal-color-text-muted, #6b7280);
}

.area-switcher__label {
  font-weight: 500;
}

.area-switcher__subject {
  color: var(--cal-color-text-muted, #6b7280);
}

.area-switcher--fixed {
  font-size: var(--cal-font-size-sm, 0.875rem);
}
</style>
