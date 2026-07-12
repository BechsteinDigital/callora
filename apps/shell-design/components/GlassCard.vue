<template>
  <section class="glass-card" :class="{ 'glass-card--strong': strong }">
    <header v-if="title || $slots.actions" class="glass-card__header">
      <div>
        <h2 v-if="title" class="glass-card__title">{{ title }}</h2>
        <p v-if="description" class="glass-card__description">{{ description }}</p>
      </div>
      <div v-if="$slots.actions" class="glass-card__actions">
        <slot name="actions" />
      </div>
    </header>
    <slot />
  </section>
</template>

<script lang="ts" src="../scripts/GlassCard.ts"></script>

<style lang="scss" src="./GlassCard.scss" scoped></style>
