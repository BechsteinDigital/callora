(function registerTemplateBetaWorkspaceUiExtensions() {
  window.calloraWorkspaceUi = window.calloraWorkspaceUi || {};
  const bridge = window.calloraWorkspaceUi;

  const banner = {
    id: "template-beta-workspace-banner",
    pluginId: "template-beta",
    title: "Template Beta active",
    description: "Decorates and overrides base workspace widgets."
  };

  const replacementWidget = {
    widgetKey: "workspace.dashboard.hero.replacement",
    pluginId: "template-beta",
    slot: "dashboard.main",
    title: "Workspace Overview (Beta)",
    description: "Theme-inherited replacement from Template Beta.",
    contentHtml: "<p>Template Beta replaces the base dashboard hero widget.</p>",
    order: 10,
    priority: 200,
    override: {
      targetWidgetKey: "workspace.dashboard.hero",
      mode: "replace"
    }
  };

  const sidebarWidget = {
    widgetKey: "workspace.sidebar.quicklinks",
    pluginId: "template-beta",
    slot: "sidebar.main",
    title: "Dialer Quicklink",
    description: "Shortcut to workspace content modules.",
    order: 20,
    priority: 120
  };

  if (typeof bridge.registerInfoBanner === "function") {
    bridge.registerInfoBanner(banner);
  } else {
    const queuedInfoBanners = bridge.queuedInfoBanners || [];
    queuedInfoBanners.push(banner);
    bridge.queuedInfoBanners = queuedInfoBanners;
  }

  const widgets = [replacementWidget, sidebarWidget];
  if (typeof bridge.registerWidget === "function") {
    widgets.forEach((widget) => bridge.registerWidget(widget));
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(...widgets);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
