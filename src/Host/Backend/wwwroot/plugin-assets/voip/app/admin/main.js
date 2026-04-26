(function registerVoipAdminUiExtensions() {
  window.calloraAdminUi = window.calloraAdminUi || {};
  const bridge = window.calloraAdminUi;

  const loginNotice = {
    id: 'voip-plugin-login-notice',
    position: 'after-form',
    title: 'VoIP Plugin Active',
    description: 'SIP Accounts are available in the admin sidebar when the plugin is active.',
    color: 'info',
    order: 60
  };

  const sipCrudPage = {
    id: 'sip-accounts',
    pluginId: 'voip',
    title: 'SIP Accounts',
    routePath: '/extensions/voip/sip-accounts',
    apiBasePath: '/api/ext/admin/plugins/voip/sip-accounts',
    primaryKey: 'sipAccountId',
    icon: 'i-lucide-phone-call',
    description: 'Manage SIP accounts provided by the VoIP plugin.',
    emptyMessage: 'No SIP accounts configured.',
    columns: [
      { key: 'sipAccountId', label: 'ID' },
      { key: 'displayName', label: 'Display Name' },
      { key: 'username', label: 'Username' },
      { key: 'domain', label: 'Domain' },
      { key: 'isActive', label: 'Status', type: 'boolean-badge', trueLabel: 'active', falseLabel: 'inactive' },
      { key: 'updatedAtUtc', label: 'Updated', type: 'datetime' }
    ],
    formFields: [
      { key: 'displayName', label: 'Display Name', type: 'text', required: true },
      { key: 'username', label: 'Username', type: 'text', required: true },
      { key: 'domain', label: 'Domain', type: 'text', required: true },
      { key: 'secret', label: 'Secret', type: 'password', required: true },
      { key: 'isActive', label: 'Active', type: 'boolean' }
    ]
  };

  if (typeof bridge.registerLoginNoticeExtension === 'function') {
    bridge.registerLoginNoticeExtension(loginNotice);
  } else {
    const queuedLoginNoticeExtensions = bridge.queuedLoginNoticeExtensions || [];
    queuedLoginNoticeExtensions.push(loginNotice);
    bridge.queuedLoginNoticeExtensions = queuedLoginNoticeExtensions;
  }

  if (typeof bridge.registerPageExtension === 'function') {
    bridge.registerPageExtension(sipCrudPage);
  } else {
    const queuedPageExtensions = bridge.queuedPageExtensions || [];
    queuedPageExtensions.push(sipCrudPage);
    bridge.queuedPageExtensions = queuedPageExtensions;
  }
})();
