(function registerTemplateAlphaWorkspaceUiExtensions() {
  window.calloraWorkspaceUi = window.calloraWorkspaceUi || {};
  const bridge = window.calloraWorkspaceUi;

  const banner = {
    id: "template-alpha-workspace-banner",
    pluginId: "template-alpha",
    title: "Template Alpha active",
    description: "Base workspace theme widgets are registered."
  };

  const widget = {
    widgetKey: "workspace.dashboard.hero",
    pluginId: "template-alpha",
    slot: "dashboard.main",
    title: "Workspace Overview",
    description: "Base dashboard widget from Template Alpha.",
    contentHtml: "<p>Base theme widget rendered from Template Alpha.</p>",
    order: 10,
    priority: 100
  };

  if (typeof bridge.registerInfoBanner === "function") {
    bridge.registerInfoBanner(banner);
  } else {
    const queuedInfoBanners = bridge.queuedInfoBanners || [];
    queuedInfoBanners.push(banner);
    bridge.queuedInfoBanners = queuedInfoBanners;
  }

  if (typeof bridge.registerWidget === "function") {
    bridge.registerWidget(widget);
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(widget);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
