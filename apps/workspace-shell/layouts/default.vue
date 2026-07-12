<template>
  <div class="shell">
    <aside class="shell__sidebar">
      <ShellBlock name="workspace.layout.branding" :context="blockContext">
        <div class="shell__brand">
          <span class="shell__brand-mark">{{ workspaceName.slice(0, 2).toUpperCase() }}</span>
          <div>
            <p class="shell__brand-name">{{ workspaceName }}</p>
            <p class="shell__brand-type">{{ workspaceType }}</p>
          </div>
        </div>
      </ShellBlock>

      <ShellBlock name="workspace.layout.nav" :context="blockContext">
        <nav class="shell__nav">
          <NuxtLink
            v-for="link in navigationLinks"
            :key="link.to"
            :to="link.to"
            class="shell__nav-link"
            active-class="shell__nav-link--active"
          >
            <span aria-hidden="true">{{ link.icon }}</span>
            {{ link.label }}
          </NuxtLink>
        </nav>
      </ShellBlock>

      <div class="shell__spacer" />

      <ShellBlock name="workspace.layout.sidebar.footer" :context="blockContext">
        <div class="shell__user">
          <p class="shell__user-name">{{ userLabel }}</p>
          <GlassButton variant="ghost" block @click="logout">Abmelden</GlassButton>
        </div>
      </ShellBlock>
    </aside>

    <main class="shell__main">
      <slot />
    </main>
  </div>
</template>

<script lang="ts" src="../scripts/layouts/default.ts"></script>

<style lang="scss" src="./default.scss" scoped></style>
