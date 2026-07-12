<template>
  <GlassCard
    title="Seite nicht gefunden"
    :description="`Für '${route.path}' ist keine Ansicht registriert.`"
  />
</template>

<script lang="ts" src="../scripts/pages/fallback.ts"></script>
