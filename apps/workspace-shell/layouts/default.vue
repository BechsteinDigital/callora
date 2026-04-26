<script setup lang="ts">
import type { DropdownMenuItem, NavigationMenuItem } from "@nuxt/ui";
import type { WorkspaceInfoBanner } from "~/types/workspace-plugin-extensions";

const auth = useWorkspaceAuth();
const { banners } = useWorkspaceInfoBanners();
const { listResolvedWidgets } = useWorkspaceWidgets();
const { workspaceKey, workspaceName, workspaceType, publicPathPrefix } = useWorkspaceContext();

const open = ref(false);

function normalizePath(value: string): string {
  if (!value) {
    return "/";
  }

  let path = value.trim();
  if (!path.startsWith("/")) {
    path = `/${path}`;
  }

  while (path.length > 1 && path.endsWith("/")) {
    path = path.slice(0, -1);
  }

  return path;
}

const workspaceBasePath = computed(() => {
  return normalizePath(publicPathPrefix.value || "/");
});

function toWorkspacePath(relativePath: string): string {
  const normalizedRelative = normalizePath(relativePath);
  const basePath = workspaceBasePath.value;

  if (basePath === "/") {
    return normalizedRelative;
  }

  if (normalizedRelative === "/") {
    return basePath;
  }

  return `${basePath}${normalizedRelative}`;
}

const mainLinks = computed<NavigationMenuItem[][]>(() => [[{
  label: "Overview",
  icon: "i-lucide-house",
  to: toWorkspacePath("/"),
  onSelect: () => {
    open.value = false;
  }
}, {
  label: "Dashboard",
  icon: "i-lucide-layout-grid",
  to: toWorkspacePath("/dashboard"),
  onSelect: () => {
    open.value = false;
  }
}, {
  label: "Content",
  icon: "i-lucide-layout-template",
  to: toWorkspacePath("/content"),
  onSelect: () => {
    open.value = false;
  }
}]]);

const pluginBannerLinks = computed<NavigationMenuItem[][]>(() => {
  const items = banners.value
    .slice()
    .map((banner: WorkspaceInfoBanner) => ({
      label: banner.title,
      description: banner.pluginId || "workspace",
      icon: "i-lucide-plug-zap",
      to: toWorkspacePath("/content"),
      onSelect: () => {
        open.value = false;
      }
    }));

  return items.length > 0 ? [items] : [];
});

const sidebarWidgets = listResolvedWidgets("sidebar.main");

const sidebarWidgetLinks = computed<NavigationMenuItem[][]>(() => {
  const items = sidebarWidgets.value.map((widget) => ({
    label: widget.title,
    description: widget.pluginId,
    icon: "i-lucide-panel-right",
    to: toWorkspacePath("/content"),
    onSelect: () => {
      open.value = false;
    }
  }));

  return items.length > 0 ? [items] : [];
});

const supportLinks = computed<NavigationMenuItem[][]>(() => [[{
  label: "Admin",
  icon: "i-lucide-shield",
  to: "/admin/",
  target: "_blank"
}]]);

const userLabel = computed(() =>
  auth.session.value?.displayName ||
  auth.session.value?.email ||
  auth.session.value?.userId ||
  "Workspace User"
);

const userMenuItems = computed<DropdownMenuItem[][]>(() => [[{
  label: userLabel.value,
  icon: "i-lucide-user"
}], [{
  label: "Logout",
  icon: "i-lucide-log-out",
  onSelect: async () => {
    await auth.logout();
    await navigateTo({ name: "login" });
  }
}]]);

</script>

<template>
  <UDashboardGroup unit="rem" storage="local">
    <UDashboardSidebar
      id="workspace"
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
          :label="collapsed ? undefined : workspaceName"
          trailing-icon="i-lucide-chevrons-up-down"
        >
          <template #leading>
            <UAvatar alt="Workspace" text="WS" size="xs" />
          </template>
        </UButton>
      </template>

      <template #default="{ collapsed }">
        <UDashboardSearchButton :collapsed="collapsed" class="bg-transparent ring-default" />

        <UNavigationMenu
          :collapsed="collapsed"
          :items="mainLinks"
          orientation="vertical"
          tooltip
          popover
        />

        <UNavigationMenu
          v-if="pluginBannerLinks.length > 0"
          :collapsed="collapsed"
          :items="pluginBannerLinks"
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
        <div class="px-2 pb-2 text-xs text-muted" v-if="!collapsed">
          <div>Type: {{ workspaceType }}</div>
          <div>Key: {{ workspaceKey }}</div>
        </div>

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
              <UAvatar :alt="userLabel" :text="userLabel.slice(0, 2).toUpperCase()" size="xs" />
            </template>
          </UButton>
        </UDropdownMenu>
      </template>
    </UDashboardSidebar>

    <slot />
  </UDashboardGroup>
</template>
