(function registerVoipWorkspaceUiExtensions() {
  window.calloraWorkspaceUi = window.calloraWorkspaceUi || {};
  const bridge = window.calloraWorkspaceUi;

  const banner = {
    id: "voip-workspace-banner",
    pluginId: "voip",
    title: "VoIP Module",
    description: "VoIP workspace bundle loaded."
  };

  if (typeof bridge.registerInfoBanner === "function") {
    bridge.registerInfoBanner(banner);
  } else {
    const queuedInfoBanners = bridge.queuedInfoBanners || [];
    queuedInfoBanners.push(banner);
    bridge.queuedInfoBanners = queuedInfoBanners;
  }

  const voipWidget = {
    widgetKey: "voip.content.status",
    pluginId: "voip",
    slot: "content.main",
    title: "VoIP Runtime",
    description: "Live status area for calls and telephony events.",
    contentHtml: "<p>VoIP plugin workspace surface is active.</p>",
    order: 30,
    priority: 100
  };

  if (typeof bridge.registerWidget === "function") {
    bridge.registerWidget(voipWidget);
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(voipWidget);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
