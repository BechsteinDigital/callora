import CallsPage from "./CallsPage.vue";

type WorkspaceBridge = {
  registerPage?: (page: unknown) => void;
  queuedPages?: unknown[];
};

declare global {
  interface Window {
    calloraWorkspaceUi?: WorkspaceBridge;
  }
}

const bridge: WorkspaceBridge = (window.calloraWorkspaceUi = window.calloraWorkspaceUi || {});

const callsPage = {
  path: "/calls",
  pluginId: "voip",
  title: "Anrufe",
  icon: "📞",
  navOrder: 10,
  component: CallsPage
};

if (typeof bridge.registerPage === "function") {
  bridge.registerPage(callsPage);
} else {
  const queuedPages = bridge.queuedPages || [];
  queuedPages.push(callsPage);
  bridge.queuedPages = queuedPages;
}
