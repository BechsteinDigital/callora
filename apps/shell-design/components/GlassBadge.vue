<template>
  <span class="glass-badge" :class="`glass-badge--${tone}`">
    <slot />
  </span>
</template>

<script lang="ts" src="../scripts/GlassBadge.ts"></script>

<style lang="scss" src="./GlassBadge.scss" scoped></style>
