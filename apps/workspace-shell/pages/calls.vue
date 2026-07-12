<script setup lang="ts">
const {
  calls,
  channels,
  streamConnected,
  refresh,
  connectStream,
  disconnectStream,
  placeCall,
  callAction,
  sendDtmf
} = useWorkspaceCalls();
const toast = useToast();

const loading = ref(true);
const placing = ref(false);
const dialTarget = ref("");
const dialChannelId = ref<string | undefined>(undefined);
const dtmfInputs = ref<Record<string, string>>({});

const ringingCalls = computed(() =>
  calls.value.filter((call) => call.state === "Ringing" && call.direction === "Inbound")
);
const otherCalls = computed(() =>
  calls.value.filter((call) => !(call.state === "Ringing" && call.direction === "Inbound"))
);
const channelOptions = computed(() =>
  channels.value.map((channel) => ({
    label: `${channel.displayName} (${channel.pluginId})`,
    value: channel.channelId
  }))
);

function stateColor(state: string): "success" | "warning" | "neutral" {
  if (state === "Connected") {
    return "success";
  }
  if (state === "Ringing") {
    return "warning";
  }
  return "neutral";
}

async function loadCalls(): Promise<void> {
  loading.value = true;
  try {
    await refresh();
  } finally {
    loading.value = false;
  }
}

async function runCallAction(callId: string, action: "accept" | "reject" | "hangup"): Promise<void> {
  try {
    await callAction(callId, action);
  } catch {
    toast.add({
      title: `Call ${action} failed`,
      description: "The call may already have ended.",
      color: "warning"
    });
    await refresh();
  }
}

async function submitDial(): Promise<void> {
  if (!dialTarget.value.trim()) {
    return;
  }

  placing.value = true;
  try {
    await placeCall(dialTarget.value.trim(), dialChannelId.value);
    dialTarget.value = "";
  } catch {
    toast.add({
      title: "Call could not be placed",
      description: "No voice channel available or the channel rejected the call.",
      color: "error"
    });
  } finally {
    placing.value = false;
  }
}

async function submitDtmf(callId: string): Promise<void> {
  const tones = (dtmfInputs.value[callId] || "").trim();
  if (!tones) {
    return;
  }

  try {
    for (const tone of tones) {
      await sendDtmf(callId, tone);
    }
    dtmfInputs.value[callId] = "";
  } catch {
    toast.add({ title: "DTMF failed", color: "warning" });
  }
}

onMounted(() => {
  connectStream();
});

onBeforeUnmount(() => {
  disconnectStream();
});

await loadCalls();
</script>

<template>
  <UDashboardPanel id="workspace-calls">
    <template #header>
      <UDashboardNavbar title="Calls">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UBadge
            :color="streamConnected ? 'success' : 'neutral'"
            variant="subtle"
            :icon="streamConnected ? 'i-lucide-radio' : 'i-lucide-radio-off'"
          >
            {{ streamConnected ? "Live" : "Offline" }}
          </UBadge>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading"
            @click="loadCalls"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div class="space-y-6">
        <UPageCard title="Place a call">
          <form class="flex flex-col sm:flex-row gap-3" @submit.prevent="submitDial">
            <UInput
              v-model="dialTarget"
              placeholder="+49301234567 or sip:alice@example.org"
              icon="i-lucide-phone-outgoing"
              class="flex-1"
            />
            <USelect
              v-if="channelOptions.length > 1"
              v-model="dialChannelId"
              :items="channelOptions"
              placeholder="Auto channel"
              class="sm:w-64"
            />
            <UButton
              type="submit"
              icon="i-lucide-phone"
              :loading="placing"
              :disabled="!dialTarget.trim() || channelOptions.length === 0"
            >
              Call
            </UButton>
          </form>
          <p v-if="channelOptions.length === 0" class="text-sm text-muted mt-2">
            No voice channel is registered for this workspace yet.
          </p>
        </UPageCard>

        <UPageCard v-if="ringingCalls.length > 0" title="Incoming calls">
          <ul class="space-y-3">
            <li
              v-for="call in ringingCalls"
              :key="call.callId"
              class="border border-warning rounded-lg p-4 flex flex-col sm:flex-row sm:items-center gap-3"
            >
              <div class="flex-1">
                <p class="font-semibold">
                  {{ call.targetDisplayName || call.targetValue }}
                </p>
                <p class="text-sm text-muted">
                  {{ call.targetValue }} • {{ call.channelId }}
                </p>
              </div>
              <div class="flex gap-2">
                <UButton
                  color="success"
                  icon="i-lucide-phone"
                  @click="runCallAction(call.callId, 'accept')"
                >
                  Accept
                </UButton>
                <UButton
                  color="error"
                  variant="soft"
                  icon="i-lucide-phone-off"
                  @click="runCallAction(call.callId, 'reject')"
                >
                  Reject
                </UButton>
              </div>
            </li>
          </ul>
        </UPageCard>

        <UPageCard title="Active calls">
          <UEmpty
            v-if="otherCalls.length === 0"
            icon="i-lucide-phone-missed"
            title="No active calls"
            description="Placed and accepted calls appear here in real time."
          />
          <ul v-else class="space-y-3">
            <li
              v-for="call in otherCalls"
              :key="call.callId"
              class="border border-default rounded-lg p-4 flex flex-col gap-3"
            >
              <div class="flex flex-col sm:flex-row sm:items-center gap-3">
                <div class="flex-1">
                  <p class="font-semibold">
                    {{ call.targetDisplayName || call.targetValue }}
                  </p>
                  <p class="text-sm text-muted">
                    {{ call.direction }} • {{ call.channelId }} •
                    started {{ new Date(call.startedAtUtc).toLocaleTimeString() }}
                  </p>
                </div>
                <UBadge :color="stateColor(call.state)" variant="subtle">
                  {{ call.state }}
                </UBadge>
                <UButton
                  color="error"
                  variant="soft"
                  icon="i-lucide-phone-off"
                  @click="runCallAction(call.callId, 'hangup')"
                >
                  Hang up
                </UButton>
              </div>
              <form
                v-if="call.state === 'Connected'"
                class="flex gap-2"
                @submit.prevent="submitDtmf(call.callId)"
              >
                <UInput
                  v-model="dtmfInputs[call.callId]"
                  placeholder="DTMF tones, e.g. 1234#"
                  icon="i-lucide-grid-3x3"
                  class="w-56"
                />
                <UButton type="submit" color="neutral" variant="soft">
                  Send
                </UButton>
              </form>
            </li>
          </ul>
        </UPageCard>
      </div>
    </template>
  </UDashboardPanel>
</template>
