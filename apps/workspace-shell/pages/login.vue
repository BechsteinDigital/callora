<template>
  <div class="login">
    <GlassCard
      class="login__card"
      strong
      :title="`Anmelden bei ${workspaceName}`"
      description="Mit deinem Workspace-Zugang anmelden."
    >
      <form class="login__form" @submit.prevent="submit">
        <GlassInput
          v-model="loginValue"
          label="E-Mail oder Benutzername"
          autocomplete="username"
          placeholder="name@firma.de"
        />
        <GlassInput
          v-model="password"
          label="Passwort"
          type="password"
          autocomplete="current-password"
          placeholder="••••••••"
        />
        <p v-if="errorMessage" class="login__error">{{ errorMessage }}</p>
        <GlassButton type="submit" block :loading="pending">
          Anmelden
        </GlassButton>
      </form>
    </GlassCard>
  </div>
</template>

<script lang="ts" src="../scripts/pages/login.ts"></script>

<style lang="scss" src="./login.scss" scoped></style>
