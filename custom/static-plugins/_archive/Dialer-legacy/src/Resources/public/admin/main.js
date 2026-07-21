(function registerDialerAdminUiExtensions() {
  window.calloraAdminUi = window.calloraAdminUi || {};
  const bridge = window.calloraAdminUi;

  const numbersCrudPage = {
    id: 'numbers',
    pluginId: 'dialer',
    title: 'Dialer Numbers',
    routePath: '/extensions/dialer/numbers',
    apiBasePath: '/api/ext/admin/plugins/dialer/workspaces/{workspaceKey}/numbers',
    primaryKey: 'numberId',
    icon: 'i-lucide-phone-outgoing',
    description: 'Nummernliste des Dialers je Workspace. Läufe werden über POST /api/ext/admin/plugins/dialer/workspaces/{workspaceKey}/runs gestartet.',
    emptyMessage: 'Keine Nummern hinterlegt.',
    columns: [
      { key: 'number', label: 'Nummer' },
      { key: 'displayName', label: 'Anzeigename' },
      { key: 'addedAtUtc', label: 'Hinzugefügt', type: 'datetime' }
    ],
    formFields: [
      { key: 'number', label: 'Nummer', type: 'text', required: true },
      { key: 'displayName', label: 'Anzeigename', type: 'text' }
    ]
  };

  if (typeof bridge.registerPageExtension === 'function') {
    bridge.registerPageExtension(numbersCrudPage);
  } else {
    const queuedPageExtensions = bridge.queuedPageExtensions || [];
    queuedPageExtensions.push(numbersCrudPage);
    bridge.queuedPageExtensions = queuedPageExtensions;
  }

  const pluginsWidget = {
    widgetKey: 'admin.plugins.dialer-status',
    pluginId: 'dialer',
    slot: 'plugins.main',
    title: 'Dialer',
    description: 'Wählt Nummernlisten über die Voice-Verträge der Plattform — ohne eigenes SIP.',
    order: 25,
    priority: 140
  };

  if (typeof bridge.registerWidget === 'function') {
    bridge.registerWidget(pluginsWidget);
  } else {
    const queuedWidgets = bridge.queuedWidgets || [];
    queuedWidgets.push(pluginsWidget);
    bridge.queuedWidgets = queuedWidgets;
  }
})();
