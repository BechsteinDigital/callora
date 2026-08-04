<template>
  <DropdownMenuRoot>
    <DropdownMenuTrigger class="user-trigger" :aria-label="`Konto: ${label}`">
      <span class="user-trigger__avatar" aria-hidden="true">{{ initials }}</span>
      <span class="user-trigger__text">
        <span class="user-trigger__name">{{ label }}</span>
        <span v-if="subtitle" class="user-trigger__subtitle">{{ subtitle }}</span>
      </span>
      <CalIcon class="user-trigger__chevron" :icon="ChevronDown" size="sm" />
    </DropdownMenuTrigger>
    <DropdownMenuPortal>
      <DropdownMenuContent class="user-menu" :side-offset="6" align="end">
        <div class="user-menu__head">
          <p class="user-menu__name">{{ label }}</p>
          <p v-if="subtitle" class="user-menu__subtitle">{{ subtitle }}</p>
        </div>
        <DropdownMenuSeparator class="user-menu__separator" />
        <DropdownMenuItem class="user-item" @select="onLogout">
          <CalIcon :icon="LogOut" size="sm" />
          Abmelden
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenuPortal>
  </DropdownMenuRoot>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuPortal,
  DropdownMenuRoot,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from 'radix-vue'
import { ChevronDown, LogOut } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import CalIcon from './CalIcon.vue'
import { useAuthStore } from '@/core/auth/authStore'

const props = defineProps<{
  label: string
  /** Says which kind of session this is — "Operator" or the bound workspace. */
  subtitle?: string
}>()

const router = useRouter()

// Up to two initials from the display name; an id like "svc-import" still yields
// something readable rather than a blank circle.
const initials = computed(() =>
  props.label
    .split(/[\s._-]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join(''),
)

async function onLogout() {
  await useAuthStore().logout()
  router.push('/login')
}
</script>

<style scoped lang="scss">
.user-trigger {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  height: 32px;
  padding: 0 var(--cal-space-2);
  border: 1px solid transparent;
  border-radius: var(--cal-radius-sm);
  background: transparent;
  color: var(--cal-text);
  cursor: pointer;
  transition:
    background var(--cal-duration-fast) var(--cal-ease),
    border-color var(--cal-duration-fast) var(--cal-ease);
}

.user-trigger:hover,
.user-trigger[data-state='open'] {
  background: var(--cal-surface-hover);
  border-color: var(--cal-border);
}

.user-trigger__avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  flex: none;
  border-radius: var(--cal-radius-full);
  background: var(--cal-accent-subtle);
  color: var(--cal-accent);
  font-size: var(--cal-text-xs);
  font-weight: var(--cal-weight-semibold);
}

.user-trigger__text {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  line-height: 1.15;
  min-width: 0;
}

.user-trigger__name {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  max-width: 140px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-trigger__subtitle {
  font-size: var(--cal-text-xs);
  color: var(--cal-text-muted);
}

.user-trigger__chevron {
  color: var(--cal-text-muted);
}

.user-menu {
  z-index: var(--cal-z-dropdown);
  min-width: 200px;
  padding: var(--cal-space-1);
  background: var(--cal-surface-raised);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-md);
  box-shadow: var(--cal-shadow-lg);
}

.user-menu__head {
  padding: var(--cal-space-2) var(--cal-space-3);
}

.user-menu__name {
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
}

.user-menu__subtitle {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.user-menu__separator {
  height: 1px;
  margin: var(--cal-space-1) 0;
  background: var(--cal-border-subtle);
}

.user-item {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  padding: var(--cal-space-2) var(--cal-space-3);
  border-radius: var(--cal-radius-sm);
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
  cursor: pointer;
  outline: none;
}

.user-item[data-highlighted] {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

@media (width <= 900px) {
  .user-trigger__text {
    display: none;
  }
}
</style>
