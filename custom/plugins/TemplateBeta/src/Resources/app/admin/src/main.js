(function registerTemplateBetaAdminUiExtensions() {
  window.calloraAdminUi = window.calloraAdminUi || {};
  const bridge = window.calloraAdminUi;

  const replacementWidget = {
    widgetKey: "admin.dashboard.core.replacement",
    pluginId: "template-beta",
    slot: "dashboard.main",
    title: "Template Beta Admin",
    description: "Overrides the Template Alpha dashboard widget.",
    contentHtml: "<p>Template Beta replaced the base admin dashboard widget.</p>",
    order: 20,
    priority: 200,
    override: {
      targetWidgetKey: "admin.dashboard.core",
      mode: "replace"
    }
  };

  const pluginsWidget = {
    widgetKey: "admin.plugins.hint",
    pluginId: "template-beta",
    slot: "plugins.main",
    title: "Template Beta Hint",
    description: "Plugin management area is extended by Template Beta.",
    contentHtml: "<p>Template Beta contributes plugin-management helper content.</p>",
    order: 10,
    priority: 120
  };

  const widgets = [replacementWidget, pluginsWidget];
  if (typeof bridge.registerWidget === "function") {
    widgets.forEach((widget) => bridge.registerWidget(widget));
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(...widgets);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
