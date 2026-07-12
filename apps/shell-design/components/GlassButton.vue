<template>
  <button
    class="glass-button"
    :class="[`glass-button--${variant}`, { 'glass-button--block': block }]"
    :type="type"
    :disabled="disabled || loading"
  >
    <span v-if="loading" class="glass-button__spinner" aria-hidden="true" />
    <slot />
  </button>
</template>

<script lang="ts" src="../scripts/GlassButton.ts"></script>

<style lang="scss" src="./GlassButton.scss" scoped></style>
