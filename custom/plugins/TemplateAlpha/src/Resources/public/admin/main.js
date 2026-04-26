(function registerTemplateAlphaAdminUiExtensions() {
  window.calloraAdminUi = window.calloraAdminUi || {};
  const bridge = window.calloraAdminUi;

  const dashboardWidget = {
    widgetKey: "admin.dashboard.core",
    pluginId: "template-alpha",
    slot: "dashboard.main",
    title: "Template Alpha Admin",
    description: "Base admin dashboard widget provided by Template Alpha.",
    contentHtml: "<p>Alpha base widget is active in admin dashboard.</p>",
    order: 20,
    priority: 100
  };

  const sidebarWidget = {
    widgetKey: "admin.sidebar.template-alpha",
    pluginId: "template-alpha",
    slot: "sidebar.main",
    title: "Template Alpha",
    description: "Base admin extension link.",
    order: 20,
    priority: 100
  };

  const widgets = [dashboardWidget, sidebarWidget];
  if (typeof bridge.registerWidget === "function") {
    widgets.forEach((widget) => bridge.registerWidget(widget));
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(...widgets);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
