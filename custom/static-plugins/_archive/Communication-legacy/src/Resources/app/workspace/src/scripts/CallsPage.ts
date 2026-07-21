import { computed, defineComponent, onBeforeUnmount, onMounted, ref } from "vue";

type ActiveCall = {
  callId: string;
  channelId: string;
  direction: string;
  state: string;
  targetValue: string;
  targetDisplayName?: string | null;
};

type CallEvent = { type: string; call: ActiveCall };

type VoiceChannel = { channelId: string; displayName: string };

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T | null> {
  const response = await fetch(url, { credentials: "include", ...options });
  if (!response.ok) {
    throw new Error(`Voice request failed with status ${response.status}`);
  }
  return response.status === 204 ? null : (response.json() as Promise<T>);
}

export default defineComponent({
  name: "VoipCallsPage",
  props: {
    context: { type: Object, default: () => ({}) }
  },
  setup(props) {
    const workspaceKey = computed(() =>
      String((props.context as { workspaceKey?: string })?.workspaceKey || "default"));
    const query = computed(() => `workspaceKey=${encodeURIComponent(workspaceKey.value)}`);

    const calls = ref<ActiveCall[]>([]);
    const channels = ref<VoiceChannel[]>([]);
    const streamConnected = ref(false);
    const statusMessage = ref("");
    const dialTarget = ref("");
    const dialChannelId = ref("");
    const dtmfInputs = ref<Record<string, string>>({});
    let eventSource: EventSource | null = null;

    const ringingCalls = computed(() =>
      calls.value.filter((call) => call.state === "Ringing" && call.direction === "Inbound"));
    const activeCalls = computed(() =>
      calls.value.filter((call) => !(call.state === "Ringing" && call.direction === "Inbound")));

    function describeCall(call: ActiveCall): string {
      return call.targetDisplayName || call.targetValue;
    }

    function stateLabel(state: string): string {
      if (state === "Connecting") return "Verbindet …";
      if (state === "Ringing") return "Klingelt";
      if (state === "Connected") return "Im Gespräch";
      return state;
    }

    function stateClass(state: string): string {
      if (state === "Connected") return "voip-badge--success";
      if (state === "Ringing") return "voip-badge--warning";
      return "voip-badge--neutral";
    }

    function applyEvent(event: CallEvent): void {
      if (event.type === "call.ended") {
        calls.value = calls.value.filter((call) => call.callId !== event.call.callId);
        return;
      }
      const index = calls.value.findIndex((call) => call.callId === event.call.callId);
      if (index >= 0) {
        calls.value.splice(index, 1, event.call);
      } else {
        calls.value.push(event.call);
      }
    }

    async function refreshCalls(): Promise<void> {
      try {
        const [callsResult, channelsResult] = await Promise.all([
          fetchJson<ActiveCall[]>(`/api/calls?${query.value}`),
          fetchJson<VoiceChannel[]>(`/api/calls/channels?${query.value}`)
        ]);
        calls.value = callsResult ?? [];
        channels.value = channelsResult ?? [];
        if (channels.value.length === 0) {
          statusMessage.value = "Für diesen Workspace ist noch keine Telefonleitung eingerichtet.";
        }
      } catch {
        statusMessage.value = "Anrufdaten konnten nicht geladen werden.";
      }
    }

    async function submitDial(): Promise<void> {
      const target = dialTarget.value.trim();
      if (!target) return;
      statusMessage.value = "Anruf wird aufgebaut …";
      try {
        await fetchJson(`/api/calls?${query.value}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ target, channelId: dialChannelId.value || null })
        });
        dialTarget.value = "";
        statusMessage.value = "";
      } catch {
        statusMessage.value = "Der Anruf konnte nicht gestartet werden. Bitte Verbindung und Nummer prüfen.";
      }
    }

    async function runCallAction(callId: string, action: "accept" | "reject" | "hangup"): Promise<void> {
      try {
        await fetchJson(`/api/calls/${encodeURIComponent(callId)}/${action}?${query.value}`, { method: "POST" });
      } catch {
        statusMessage.value = "Die Aktion war nicht möglich — vermutlich ist der Anruf bereits beendet.";
        await refreshCalls();
      }
    }

    async function submitDtmf(callId: string): Promise<void> {
      const tones = (dtmfInputs.value[callId] || "").trim();
      if (!tones) return;
      try {
        for (const tone of tones) {
          await fetchJson(`/api/calls/${encodeURIComponent(callId)}/dtmf?${query.value}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ tone })
          });
        }
        dtmfInputs.value[callId] = "";
      } catch {
        statusMessage.value = "Tastentöne konnten nicht gesendet werden.";
      }
    }

    onMounted(() => {
      void refreshCalls();
      eventSource = new EventSource(`/api/calls/events?${query.value}`, { withCredentials: true });
      eventSource.onopen = () => {
        streamConnected.value = true;
      };
      eventSource.onerror = () => {
        streamConnected.value = false;
      };
      eventSource.onmessage = (message) => {
        try {
          applyEvent(JSON.parse(message.data) as CallEvent);
        } catch {
          // Fehlerhafte Stream-Nachrichten überspringen; refresh gleicht ab.
        }
      };
    });

    onBeforeUnmount(() => {
      eventSource?.close();
    });

    return {
      calls,
      channels,
      streamConnected,
      statusMessage,
      dialTarget,
      dialChannelId,
      dtmfInputs,
      ringingCalls,
      activeCalls,
      describeCall,
      stateLabel,
      stateClass,
      refreshCalls,
      submitDial,
      runCallAction,
      submitDtmf
    };
  }
});
