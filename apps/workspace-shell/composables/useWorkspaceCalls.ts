export type WorkspaceActiveCall = {
  callId: string;
  workspaceKey: string;
  channelId: string;
  direction: string;
  state: string;
  targetValue: string;
  targetDisplayName?: string | null;
  startedAtUtc: string;
};

export type WorkspaceCallEvent = {
  type: string;
  call: WorkspaceActiveCall;
};

export type WorkspaceVoiceChannel = {
  channelId: string;
  displayName: string;
  pluginId: string;
};

export function useWorkspaceCalls() {
  const { request, requestSafe } = useWorkspaceApi();
  const { workspaceKey } = useWorkspaceContext();
  const runtimeConfig = useRuntimeConfig();

  const calls = ref<WorkspaceActiveCall[]>([]);
  const channels = ref<WorkspaceVoiceChannel[]>([]);
  const streamConnected = ref(false);
  let eventSource: EventSource | null = null;

  function applyEvent(event: WorkspaceCallEvent): void {
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

  async function refresh(): Promise<void> {
    if (!workspaceKey.value) {
      return;
    }

    const query = `workspaceKey=${encodeURIComponent(workspaceKey.value)}`;
    const [callsResult, channelsResult] = await Promise.all([
      requestSafe<WorkspaceActiveCall[]>(`/api/calls?${query}`),
      requestSafe<WorkspaceVoiceChannel[]>(`/api/calls/channels?${query}`)
    ]);

    if (callsResult.ok) {
      calls.value = callsResult.data ?? [];
    }
    if (channelsResult.ok) {
      channels.value = channelsResult.data ?? [];
    }
  }

  function connectStream(): void {
    if (!import.meta.client || eventSource || !workspaceKey.value) {
      return;
    }

    const base = runtimeConfig.public.calloraApiBase || "";
    const url = `${base}/api/calls/events?workspaceKey=${encodeURIComponent(workspaceKey.value)}`;
    eventSource = new EventSource(url, { withCredentials: true });
    eventSource.onopen = () => {
      streamConnected.value = true;
    };
    eventSource.onerror = () => {
      streamConnected.value = false;
    };
    eventSource.onmessage = (message) => {
      try {
        applyEvent(JSON.parse(message.data) as WorkspaceCallEvent);
      } catch {
        // Malformed stream payloads are skipped; the next refresh reconciles state.
      }
    };
  }

  function disconnectStream(): void {
    eventSource?.close();
    eventSource = null;
    streamConnected.value = false;
  }

  async function placeCall(target: string, channelId?: string): Promise<WorkspaceActiveCall> {
    const query = `workspaceKey=${encodeURIComponent(workspaceKey.value)}`;
    return await request<WorkspaceActiveCall>(`/api/calls?${query}`, {
      method: "POST",
      body: {
        target,
        channelId: channelId || null
      }
    });
  }

  async function callAction(callId: string, action: "accept" | "reject" | "hangup"): Promise<void> {
    const query = `workspaceKey=${encodeURIComponent(workspaceKey.value)}`;
    await request(`/api/calls/${encodeURIComponent(callId)}/${action}?${query}`, {
      method: "POST"
    });
  }

  async function sendDtmf(callId: string, tone: string): Promise<void> {
    const query = `workspaceKey=${encodeURIComponent(workspaceKey.value)}`;
    await request(`/api/calls/${encodeURIComponent(callId)}/dtmf?${query}`, {
      method: "POST",
      body: { tone }
    });
  }

  return {
    calls,
    channels,
    streamConnected,
    refresh,
    connectStream,
    disconnectStream,
    placeCall,
    callAction,
    sendDtmf
  };
}
