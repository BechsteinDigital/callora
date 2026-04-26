import type { AdminLoginNoticeExtension } from "~/types/admin-login-extensions";
import type { AdminPluginCrudPageExtension, AdminUiBridge, AdminWidget } from "~/types/admin-plugin-extensions";

declare global {
  interface Window {
    calloraAdminUi?: AdminUiBridge;
  }
}

export default defineNuxtPlugin(() => {
  const { registerNoticeExtension } = useAdminLoginExtensions();
  const { registerCrudPageExtension } = useAdminPageExtensions();
  const { registerWidget } = useAdminWidgets();
  const { ensureAdminPluginAssetsLoaded } = useAdminPluginAssets();
  const existingBridge = window.calloraAdminUi ?? {};

  if (Array.isArray(existingBridge.queuedLoginNoticeExtensions)) {
    existingBridge.queuedLoginNoticeExtensions.forEach((entry) => {
      registerNoticeExtension(entry);
    });
    existingBridge.queuedLoginNoticeExtensions = [];
  }

  if (Array.isArray(existingBridge.queuedPageExtensions)) {
    existingBridge.queuedPageExtensions.forEach((entry) => {
      registerCrudPageExtension(entry);
    });
    existingBridge.queuedPageExtensions = [];
  }
  if (Array.isArray(existingBridge.queuedWidgets)) {
    existingBridge.queuedWidgets.forEach((entry) => {
      registerWidget(entry);
    });
    existingBridge.queuedWidgets = [];
  }

  existingBridge.registerLoginNoticeExtension = registerNoticeExtension;
  existingBridge.registerPageExtension = registerCrudPageExtension;
  existingBridge.registerWidget = (widget: AdminWidget) => {
    registerWidget(widget);
  };
  window.calloraAdminUi = existingBridge;

  void ensureAdminPluginAssetsLoaded().then(() => {
    const bridge = window.calloraAdminUi;
    if (!bridge) {
      return;
    }

    if (Array.isArray(bridge.queuedLoginNoticeExtensions)) {
      bridge.queuedLoginNoticeExtensions.forEach((entry) => {
        registerNoticeExtension(entry);
      });
      bridge.queuedLoginNoticeExtensions = [];
    }

    if (Array.isArray(bridge.queuedPageExtensions)) {
      bridge.queuedPageExtensions.forEach((entry) => {
        registerCrudPageExtension(entry);
      });
      bridge.queuedPageExtensions = [];
    }

    if (Array.isArray(bridge.queuedWidgets)) {
      bridge.queuedWidgets.forEach((entry) => {
        registerWidget(entry);
      });
      bridge.queuedWidgets = [];
    }
  });
});
