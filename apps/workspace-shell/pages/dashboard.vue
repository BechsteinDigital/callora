<template>
  <div class="dashboard">
    <ShellBlock name="workspace.dashboard.before" :context="blockContext" />

    <GlassCard
      :title="`Willkommen bei ${workspaceName}`"
      description="Hier siehst du auf einen Blick, ob alles läuft."
    >
      <template #actions>
        <GlassButton variant="ghost" :loading="loading" @click="refreshStatus">
          Aktualisieren
        </GlassButton>
      </template>

      <div class="dashboard__status">
        <GlassBadge v-if="apiReachable === true" tone="success">System erreichbar</GlassBadge>
        <GlassBadge v-else-if="apiReachable === false" tone="danger">System nicht erreichbar</GlassBadge>
        <GlassBadge v-else tone="neutral">Status wird geprüft …</GlassBadge>
      </div>
    </GlassCard>

    <GlassCard v-if="banners.length > 0" title="Hinweise deiner Erweiterungen">
      <ul class="dashboard__banners">
        <li v-for="banner in banners" :key="banner.id">
          <p class="dashboard__banner-title">{{ banner.title }}</p>
          <p v-if="banner.description" class="dashboard__banner-text">{{ banner.description }}</p>
        </li>
      </ul>
    </GlassCard>

    <div v-if="dashboardWidgets.length > 0" class="dashboard__widgets">
      <GlassCard
        v-for="widget in dashboardWidgets"
        :key="`${widget.pluginId}:${widget.widgetKey}`"
        :title="widget.title"
        :description="widget.description"
      >
        <!-- Plugin widget content is trusted shell-extension code by design. -->
        <div v-if="widget.contentHtml" v-html="widget.contentHtml" />
      </GlassCard>
    </div>

    <ShellBlock name="workspace.dashboard.after" :context="blockContext" />
  </div>
</template>

<script lang="ts" src="../scripts/pages/dashboard.ts"></script>

<style lang="scss" src="./dashboard.scss" scoped></style>
