<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui';
import type { PluginAdminNavigationItem } from '~/types/admin-plugin-extensions';

type SidebarLink = {
  label: string;
  icon?: string;
  to: string;
  description?: string;
  target?: string;
  onSelect?: () => void;
};

const auth = useAdminAuth();
const { requestSafe } = useAdminApi();
const { ensureAdminPluginAssetsLoaded } = useAdminPluginAssets();
const { listResolvedWidgets } = useAdminWidgets();

const open = ref(false);
const workspaceLinks = ref<SidebarLink[][]>([]);
const pluginMainLinks = ref<SidebarLink[]>([]);
const baseMainLinks: SidebarLink[] = [{
  label: 'Overview',
  icon: 'i-lucide-house',
  to: '/',
  onSelect: () => {
    open.value = false;
  }
}, {
  label: 'Users',
  icon: 'i-lucide-users',
  to: '/users',
  onSelect: () => {
    open.value = false;
  }
}, {
  label: 'Workspaces',
  icon: 'i-lucide-store',
  to: '/workspaces',
  onSelect: () => {
    open.value = false;
  }
}, {
  label: 'RBAC',
  icon: 'i-lucide-shield-check',
  to: '/rbac',
  onSelect: () => {
    open.value = false;
  }
}, {
  label: 'Plugins',
  icon: 'i-lucide-plug-zap',
  to: '/plugins',
  onSelect: () => {
    open.value = false;
  }
}];

const mainLinks = computed(() => {
  const items: SidebarLink[] = [...baseMainLinks, ...pluginMainLinks.value];
  return [items];
});

const sidebarWidgets = listResolvedWidgets('sidebar.main');

const sidebarWidgetLinks = computed<SidebarLink[][]>(() => {
  const items: SidebarLink[] = sidebarWidgets.value.map((widget) => ({
    label: widget.title,
    description: widget.pluginId,
    icon: 'i-lucide-panel-right',
    to: '/plugins',
    onSelect: () => {
      open.value = false;
    }
  }));

  return items.length > 0 ? [items] : [];
});

const supportLinks = [[{
  label: 'Admin API',
  icon: 'i-lucide-terminal-square',
  to: '/swagger/api',
  target: '_blank'
}, {
  label: 'Nuxt UI Docs',
  icon: 'i-lucide-book-open',
  to: 'https://ui.nuxt.com/docs/components',
  target: '_blank'
}]] satisfies SidebarLink[][];

async function loadWorkspaceLinks(): Promise<void> {
  const response = await requestSafe<Array<{ workspaceKey: string; displayName: string }>>('/api/workspaces');
  if (!response.ok || !response.data) {
    workspaceLinks.value = [];
    return;
  }

  const entries = response.data
    .slice(0, 12)
    .map((workspace) => ({
      label: workspace.displayName,
      description: workspace.workspaceKey,
      icon: 'i-lucide-store',
      to: `/workspaces/${encodeURIComponent(workspace.workspaceKey)}`,
      onSelect: () => {
        open.value = false;
      }
    }));

  workspaceLinks.value = entries.length > 0 ? [entries] : [];
}

async function loadPluginNavigationLinks(): Promise<void> {
  const response = await requestSafe<PluginAdminNavigationItem[]>('/api/ext/admin/navigation');
  if (!response.ok || !response.data) {
    pluginMainLinks.value = [];
    return;
  }

  pluginMainLinks.value = response.data
    .slice()
    .sort((left, right) => left.order - right.order)
    .map((entry) => ({
      label: entry.label,
      icon: entry.icon || undefined,
      to: entry.to,
      onSelect: () => {
        open.value = false;
      }
    }));
}

const userLabel = computed(() => auth.session.value?.displayName || auth.session.value?.email || auth.session.value?.userId || 'Admin');

const userMenuItems = computed<DropdownMenuItem[][]>(() => [[{
  label: userLabel.value,
  icon: 'i-lucide-user'
}], [{
  label: 'Logout',
  icon: 'i-lucide-log-out',
  onSelect: async () => {
    await auth.logout();
    await navigateTo({ name: 'login' });
  }
}]]);

await Promise.all([ensureAdminPluginAssetsLoaded(), loadWorkspaceLinks(), loadPluginNavigationLinks()]);
</script>

<template>
  <UDashboardGroup
    unit="rem"
    storage="local"
  >
    <UDashboardSidebar
      id="default"
      v-model:open="open"
      collapsible
      resizable
      class="bg-elevated/25"
      :ui="{ footer: 'lg:border-t lg:border-default' }"
    >
      <template #header="{ collapsed }">
        <UButton
          color="neutral"
          variant="ghost"
          block
          :square="collapsed"
          class="data-[state=open]:bg-elevated"
          :label="collapsed ? undefined : 'Callora Admin'"
          trailing-icon="i-lucide-chevrons-up-down"
        >
          <template #leading>
            <UAvatar
              alt="Callora"
              text="CA"
              size="xs"
            />
          </template>
        </UButton>
      </template>

      <template #default="{ collapsed }">
        <UDashboardSearchButton
          :collapsed="collapsed"
          class="bg-transparent ring-default"
        />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="mainLinks"
          orientation="vertical"
          tooltip
          popover
        />

        <UNavigationMenu
          v-if="workspaceLinks.length > 0"
          :collapsed="collapsed"
          :items="workspaceLinks"
          orientation="vertical"
          tooltip
          :ui="{ linkLeadingIcon: 'text-dimmed' }"
        />

        <UNavigationMenu
          v-if="sidebarWidgetLinks.length > 0"
          :collapsed="collapsed"
          :items="sidebarWidgetLinks"
          orientation="vertical"
          tooltip
          :ui="{ linkLeadingIcon: 'text-dimmed' }"
        />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="supportLinks"
          orientation="vertical"
          tooltip
          class="mt-auto"
        />
      </template>

      <template #footer="{ collapsed }">
        <UDropdownMenu
          :items="userMenuItems"
          :content="{ align: 'center', collisionPadding: 12 }"
          :ui="{ content: collapsed ? 'w-48' : 'w-(--reka-dropdown-menu-trigger-width)' }"
        >
          <UButton
            color="neutral"
            variant="ghost"
            block
            :square="collapsed"
            class="data-[state=open]:bg-elevated"
            :label="collapsed ? undefined : userLabel"
            trailing-icon="i-lucide-chevrons-up-down"
          >
            <template #leading>
              <UAvatar
                :alt="userLabel"
                :text="userLabel.slice(0, 2).toUpperCase()"
                size="xs"
              />
            </template>
          </UButton>
        </UDropdownMenu>
      </template>
    </UDashboardSidebar>

    <slot />
  </UDashboardGroup>
</template>
