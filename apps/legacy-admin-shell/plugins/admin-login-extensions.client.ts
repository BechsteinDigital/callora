import type { AdminLoginNoticeExtension } from "~/types/admin-login-extensions";
import type {
  AdminBlockExtension,
  AdminFragment,
  AdminPluginCrudPageExtension,
  AdminUiBridge,
  AdminWidget
} from "~/types/admin-plugin-extensions";

declare global {
  interface Window {
    calloraAdminUi?: AdminUiBridge;
  }
}

export default defineNuxtPlugin(() => {
  const { registerNoticeExtension } = useAdminLoginExtensions();
  const { registerCrudPageExtension } = useAdminPageExtensions();
  const { registerWidget } = useAdminWidgets();
  const { registerBlockExtension, listKnownBlocks } = useShellBlocks();
  const { registerSnippets, translate } = useShellSnippets();
  const { registerFragment, mountFragment } = useShellFragments();
  const { ensureAdminPluginAssetsLoaded } = useAdminPluginAssets();
  const existingBridge = window.calloraAdminUi ?? {};

  function drainQueuedShellExtensions(target: AdminUiBridge): void {
    if (Array.isArray(target.queuedBlockExtensions)) {
      target.queuedBlockExtensions.forEach((extension) => {
        registerBlockExtension(extension);
      });
      target.queuedBlockExtensions = [];
    }

    if (Array.isArray(target.queuedFragments)) {
      target.queuedFragments.forEach((fragment) => {
        registerFragment(fragment);
      });
      target.queuedFragments = [];
    }

    if (Array.isArray(target.queuedSnippets)) {
      target.queuedSnippets.forEach((registration) => {
        registerSnippets(registration.locale, registration.values);
      });
      target.queuedSnippets = [];
    }
  }

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

  drainQueuedShellExtensions(existingBridge);

  existingBridge.registerLoginNoticeExtension = registerNoticeExtension;
  existingBridge.registerPageExtension = registerCrudPageExtension;
  existingBridge.registerWidget = (widget: AdminWidget) => {
    registerWidget(widget);
  };
  existingBridge.registerBlockExtension = (extension: AdminBlockExtension) => {
    registerBlockExtension(extension);
  };
  existingBridge.registerFragment = (fragment: AdminFragment) => {
    registerFragment(fragment);
  };
  existingBridge.registerSnippets = (locale: string, values: Record<string, string>) => {
    registerSnippets(locale, values);
  };
  existingBridge.translate = translate;
  existingBridge.mountFragment = mountFragment;
  existingBridge.listBlocks = listKnownBlocks;
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

    drainQueuedShellExtensions(bridge);
  });
});
