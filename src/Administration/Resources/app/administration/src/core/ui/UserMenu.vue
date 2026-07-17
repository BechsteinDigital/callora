<template>
  <DropdownMenuRoot>
    <DropdownMenuTrigger class="user-trigger">{{ label }}</DropdownMenuTrigger>
    <DropdownMenuPortal>
      <DropdownMenuContent class="user-menu" :side-offset="4" align="end">
        <DropdownMenuItem class="user-item" @select="onLogout">Abmelden</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenuPortal>
  </DropdownMenuRoot>
</template>

<script setup lang="ts">
import {
  DropdownMenuRoot,
  DropdownMenuTrigger,
  DropdownMenuPortal,
  DropdownMenuContent,
  DropdownMenuItem,
} from 'radix-vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'

defineProps<{ label: string }>()

const router = useRouter()

async function onLogout() {
  await useAuthStore().logout()
  router.push('/login')
}
</script>

<style scoped lang="scss">
.user-trigger {
  background: transparent;
  border: 1px solid var(--cal-color-muted);
  color: var(--cal-color-text);
  border-radius: var(--cal-radius);
  padding: 6px 12px;
  font: inherit;
  cursor: pointer;
}

.user-menu {
  background: var(--cal-color-surface);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  padding: 4px;
  min-width: 160px;
}

.user-item {
  padding: 8px 12px;
  border-radius: calc(var(--cal-radius) - 2px);
  cursor: pointer;
  outline: none;
}

.user-item[data-highlighted] {
  background: var(--cal-color-accent);
  color: #fff;
}
</style>
