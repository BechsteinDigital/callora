import * as Vue from "vue";
import PluginPageHost from "~/components/PluginPageHost.vue";
import type {
  WorkspaceBlockExtension,
  WorkspaceBridgeContext,
  WorkspaceFragment,
  WorkspaceInfoBanner,
  WorkspacePluginPage,
  WorkspaceUiBridge,
  WorkspaceWidget
} from "~/types/workspace-plugin-extensions";

declare global {
  interface Window {
    calloraWorkspaceUi?: WorkspaceUiBridge;
    __CALLORA_WORKSPACE_CONTEXT__?: WorkspaceBridgeContext;
    CalloraVue?: typeof Vue;
  }
}

export default defineNuxtPlugin(() => {
  // Plugin bundles compile with vue as external and resolve it from this
  // global, so their components run inside the shell's Vue instance.
  window.CalloraVue = Vue;

  const config = useRuntimeConfig();
  const route = useRoute();
  const { registerInfoBanner } = useWorkspaceInfoBanners();
  const { registerWidget } = useWorkspaceWidgets();
  const { registerBlockExtension, listKnownBlocks } = useShellBlocks();
  const { registerSnippets, translate } = useShellSnippets();
  const { registerFragment, mountFragment } = useShellFragments();
  const { registerPage, listPages } = useShellPages();
  const router = useRouter();
  const { ensureWorkspacePluginAssetsLoaded } = useWorkspacePluginAssets();
  const { applyWorkspaceThemeVariables } = useWorkspaceThemeVariables();
  const bootstrapContext = window.__CALLORA_WORKSPACE_CONTEXT__;
  const initialContext: WorkspaceBridgeContext = {
    workspace: {
      key: bootstrapContext?.workspace?.key || "default",
      name: bootstrapContext?.workspace?.name || config.public.workspaceDisplayName,
      type: bootstrapContext?.workspace?.type || config.public.workspaceType
    },
    route: {
      publicBaseUrl: bootstrapContext?.route?.publicBaseUrl || config.public.calloraApiBase || "",
      publicPathPrefix: bootstrapContext?.route?.publicPathPrefix || route.path || "/"
    }
  };
  const context = useState<WorkspaceBridgeContext>("workspace-bridge-context", () => initialContext);

  const bridge = window.calloraWorkspaceUi ?? {};
  bridge.context = context.value;
  bridge.getContext = () => context.value;
  bridge.registerInfoBanner = (banner: WorkspaceInfoBanner) => {
    registerInfoBanner(banner);
  };
  bridge.registerWidget = (widget: WorkspaceWidget) => {
    registerWidget(widget);
  };
  bridge.registerBlockExtension = (extension: WorkspaceBlockExtension) => {
    registerBlockExtension(extension);
  };
  bridge.registerFragment = (fragment: WorkspaceFragment) => {
    registerFragment(fragment);
  };
  bridge.registerPage = (page: WorkspacePluginPage) => {
    registerPluginPage(page);
  };

  function registerPluginPage(page: WorkspacePluginPage): void {
    const entry = registerPage(page);
    if (!entry) {
      return;
    }

    // The page body is a regular block extension (replace, lowest priority):
    // templates override it with higher-priority replaces and can embed it
    // via the data-shell-parent marker.
    registerBlockExtension({
      blockName: `workspace.${entry.pageId}.content`,
      pluginId: entry.pluginId,
      mode: "replace",
      priority: 0,
      mount: entry.mount,
      component: entry.component
    });
  }
  bridge.registerSnippets = (locale: string, values: Record<string, string>) => {
    registerSnippets(locale, values);
  };
  bridge.translate = translate;
  bridge.mountFragment = mountFragment;
  bridge.listBlocks = listKnownBlocks;

  if (Array.isArray(bridge.queuedInfoBanners)) {
    bridge.queuedInfoBanners.forEach((banner) => {
      registerInfoBanner(banner);
    });
    bridge.queuedInfoBanners = [];
  }
  if (Array.isArray(bridge.queuedWidgets)) {
    bridge.queuedWidgets.forEach((widget) => {
      registerWidget(widget);
    });
    bridge.queuedWidgets = [];
  }
  if (Array.isArray(bridge.queuedBlockExtensions)) {
    bridge.queuedBlockExtensions.forEach((extension) => {
      registerBlockExtension(extension);
    });
    bridge.queuedBlockExtensions = [];
  }
  drainQueuedFragmentsAndSnippets(bridge);

  window.calloraWorkspaceUi = bridge;

  watch(
    () => route.path,
    () => {
      if (!window.calloraWorkspaceUi) {
        return;
      }

      window.calloraWorkspaceUi.context = context.value;
      window.calloraWorkspaceUi.getContext = () => context.value;
    }
  );

  void applyWorkspaceThemeVariables();

  void ensureWorkspacePluginAssetsLoaded().then(() => {
    const afterLoadBridge = window.calloraWorkspaceUi;
    if (!afterLoadBridge) {
      return;
    }

    if (Array.isArray(afterLoadBridge.queuedInfoBanners)) {
      afterLoadBridge.queuedInfoBanners.forEach((banner) => {
        registerInfoBanner(banner);
      });
      afterLoadBridge.queuedInfoBanners = [];
    }

    if (Array.isArray(afterLoadBridge.queuedWidgets)) {
      afterLoadBridge.queuedWidgets.forEach((widget) => {
        registerWidget(widget);
      });
      afterLoadBridge.queuedWidgets = [];
    }

    if (Array.isArray(afterLoadBridge.queuedBlockExtensions)) {
      afterLoadBridge.queuedBlockExtensions.forEach((extension) => {
        registerBlockExtension(extension);
      });
      afterLoadBridge.queuedBlockExtensions = [];
    }

    drainQueuedFragmentsAndSnippets(afterLoadBridge);
    registerPluginPageRoutes();
  });

  function registerPluginPageRoutes(): void {
    const prefix = context.value.route.publicPathPrefix?.trim() || "/";
    const basePath = prefix === "/" ? "" : prefix.replace(/\/+$/, "");

    for (const page of listPages()) {
      const routeName = `plugin-page:${page.pageId}`;
      if (!router.hasRoute(routeName)) {
        router.addRoute({
          path: page.path,
          name: routeName,
          component: PluginPageHost,
          props: { pagePath: page.path }
        });
      }

      const prefixedName = `ws-prefixed:${routeName}`;
      if (basePath && !router.hasRoute(prefixedName)) {
        router.addRoute({
          path: `${basePath}${page.path}`,
          name: prefixedName,
          component: PluginPageHost,
          props: { pagePath: page.path }
        });
      }
    }

    // Re-resolve when the user landed on a plugin page before its route existed.
    const current = router.currentRoute.value;
    const resolved = router.resolve(current.fullPath);
    if (resolved.name !== current.name) {
      void navigateTo(current.fullPath, { replace: true });
    }
  }

  function drainQueuedFragmentsAndSnippets(target: WorkspaceUiBridge): void {
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

    if (Array.isArray(target.queuedPages)) {
      target.queuedPages.forEach((page) => {
        registerPluginPage(page);
      });
      target.queuedPages = [];
    }
  }
});
