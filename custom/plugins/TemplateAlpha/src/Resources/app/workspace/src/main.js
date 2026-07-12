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

  // Template inheritance proof: replace the shell branding block and embed
  // the original via the data-shell-parent marker — Twig's parent().
  const brandingOverride = {
    blockName: "workspace.layout.branding",
    pluginId: "template-alpha",
    mode: "replace",
    priority: 100,
    contentHtml:
      '<div class="rounded-lg p-2" style="background: var(--callora-brand-primary, #4c1d95); color: white;">' +
      '  <p class="font-bold text-sm px-1">Template Alpha</p>' +
      '  <div data-shell-parent></div>' +
      "</div>"
  };

  const dashboardNotice = {
    blockName: "workspace.dashboard.before",
    pluginId: "template-alpha",
    mode: "append",
    priority: 100,
    contentHtml:
      '<div class="border border-default rounded-lg p-3 text-sm text-muted">' +
      "Template Alpha steuert dieses Workspace-Layout (Block: workspace.dashboard.before)." +
      "</div>"
  };

  for (const extension of [brandingOverride, dashboardNotice]) {
    if (typeof bridge.registerBlockExtension === "function") {
      bridge.registerBlockExtension(extension);
    } else {
      const queuedBlockExtensions = bridge.queuedBlockExtensions || [];
      queuedBlockExtensions.push(extension);
      bridge.queuedBlockExtensions = queuedBlockExtensions;
    }
  }
})();
