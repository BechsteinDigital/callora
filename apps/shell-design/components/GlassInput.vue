<template>
  <label class="glass-input">
    <span v-if="label" class="glass-input__label">{{ label }}</span>
    <input
      v-model="model"
      class="glass-input__field"
      :type="type"
      :placeholder="placeholder"
      :autocomplete="autocomplete"
    />
    <span v-if="hint" class="glass-input__hint">{{ hint }}</span>
  </label>
</template>

<script lang="ts" src="../scripts/GlassInput.ts"></script>

<style lang="scss" src="./GlassInput.scss" scoped></style>
