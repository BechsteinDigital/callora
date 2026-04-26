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
    return;
  }

  const queuedInfoBanners = bridge.queuedInfoBanners || [];
  queuedInfoBanners.push(banner);
  bridge.queuedInfoBanners = queuedInfoBanners;
})();
