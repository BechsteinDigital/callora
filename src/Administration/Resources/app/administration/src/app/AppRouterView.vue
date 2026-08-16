<template>
  <RouterView v-slot="{ Component, route }">
    <Transition name="shell-view" mode="out-in">
      <component :is="Component" :key="viewKey(route)" />
    </Transition>
  </RouterView>
</template>

<script setup lang="ts">
import { RouterView } from 'vue-router'
import { viewKey } from './viewKey'

// Der Router verwendet eine Komponenteninstanz wieder, solange dieselbe Komponente an
// derselben Stelle steht — auch über zwei Pfade hinweg, die sie sich teilen (users/new und
// users/:userId, roles/new und roles/:role, workspaces/new und workspaces/:workspaceKey).
// `onMounted(load)` läuft dann nicht erneut, und die Detailformulare zeigen den vorigen
// Datensatz. Der teure Fall ist nicht die falsche Anzeige: Auf /users/new steht danach
// externalId des zuvor bearbeiteten Benutzers im Feld, und Speichern legt ihn erneut an.
//
// Ein `watch` auf den Parameter reicht dafür nicht — auf der /new-Route hat `load()` nichts
// zu laden und setzt die Felder folglich auch nicht zurück. Was hilft, ist eine neue
// Instanz, und der Key ist die Stelle, an der man sie nicht vergessen kann: Er gilt für
// jede Route, nicht nur für die, an die beim Schreiben jemand gedacht hat.
//
// Ausnahme ist die Ansicht, die den Parameterwechsel selbst verarbeitet und ihren Zustand
// dabei behalten SOLL — der Flächenbaum, dessen linke Spalte einen Knotenwechsel überlebt.
// Sie trägt `meta.viewKey`, und zwar denselben Wert auf allen Pfaden, die zu ihr führen.
</script>

<style scoped lang="scss">
/* A short cross-fade on route change; long enough to read as continuity,
   short enough never to feel like waiting. Reduced motion disables it. */
.shell-view-enter-active,
.shell-view-leave-active {
  transition:
    opacity var(--cal-duration-fast) var(--cal-ease),
    transform var(--cal-duration-fast) var(--cal-ease);
}

.shell-view-enter-from {
  opacity: 0;
  transform: translateY(4px);
}

.shell-view-leave-to {
  opacity: 0;
}
</style>
