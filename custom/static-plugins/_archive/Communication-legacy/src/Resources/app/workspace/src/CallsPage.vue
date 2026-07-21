<template>
  <div class="voip-calls">
    <section class="voip-card">
      <div class="voip-card__head">
        <div>
          <h2 class="voip-card__title">Jemanden anrufen</h2>
          <p class="voip-card__sub">Nummer eingeben und lostelefonieren.</p>
        </div>
        <span class="voip-badge" :class="streamConnected ? 'voip-badge--success' : 'voip-badge--neutral'">
          {{ streamConnected ? "Live verbunden" : "Offline" }}
        </span>
      </div>

      <form class="voip-dial" @submit.prevent="submitDial">
        <input v-model="dialTarget" class="voip-input voip-dial__target" placeholder="+49 30 1234567" />
        <select v-if="channels.length > 1" v-model="dialChannelId" class="voip-input">
          <option value="">Leitung automatisch wählen</option>
          <option v-for="channel in channels" :key="channel.channelId" :value="channel.channelId">
            {{ channel.displayName }}
          </option>
        </select>
        <button type="submit" class="voip-button voip-button--primary" :disabled="!dialTarget.trim() || channels.length === 0">
          Anrufen
        </button>
      </form>
      <p v-if="statusMessage" class="voip-note">{{ statusMessage }}</p>
    </section>

    <section v-if="ringingCalls.length > 0" class="voip-card">
      <h2 class="voip-card__title">Eingehender Anruf</h2>
      <ul class="voip-list">
        <li v-for="call in ringingCalls" :key="call.callId" class="voip-item voip-item--ringing">
          <div class="voip-item__info">
            <p class="voip-item__name">{{ describeCall(call) }}</p>
            <p class="voip-item__meta">{{ call.targetValue }}</p>
          </div>
          <div class="voip-item__actions">
            <button type="button" class="voip-button voip-button--success" @click="runCallAction(call.callId, 'accept')">
              Annehmen
            </button>
            <button type="button" class="voip-button voip-button--danger" @click="runCallAction(call.callId, 'reject')">
              Ablehnen
            </button>
          </div>
        </li>
      </ul>
    </section>

    <section class="voip-card">
      <h2 class="voip-card__title">Aktuelle Gespräche</h2>
      <p v-if="activeCalls.length === 0" class="voip-note">
        Gerade laufen keine Gespräche. Eingehende Anrufe erscheinen hier automatisch.
      </p>
      <ul v-else class="voip-list">
        <li v-for="call in activeCalls" :key="call.callId" class="voip-item">
          <div class="voip-item__info">
            <p class="voip-item__name">{{ describeCall(call) }}</p>
            <p class="voip-item__meta">
              {{ call.direction === "Inbound" ? "Eingehend" : "Ausgehend" }} · {{ call.channelId }}
            </p>
          </div>
          <span class="voip-badge" :class="stateClass(call.state)">{{ stateLabel(call.state) }}</span>
          <div class="voip-item__actions">
            <form v-if="call.state === 'Connected'" class="voip-dtmf" @submit.prevent="submitDtmf(call.callId)">
              <input v-model="dtmfInputs[call.callId]" class="voip-input" placeholder="Tasten, z. B. 1234#" />
              <button type="submit" class="voip-button voip-button--ghost">Senden</button>
            </form>
            <button type="button" class="voip-button voip-button--danger" @click="runCallAction(call.callId, 'hangup')">
              Auflegen
            </button>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>

<script lang="ts" src="./scripts/CallsPage.ts"></script>

<style lang="scss" src="./CallsPage.scss"></style>
