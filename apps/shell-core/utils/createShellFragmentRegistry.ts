import type { ShellFragment } from "#shell-core/types/shell-extensions";

/**
 * Shared fragment registry — Twig's include for shell extensions. Plugins
 * register named fragments; blocks and other plugins render them into a
 * container without knowing the providing plugin.
 */
export function createShellFragmentRegistry(stateKey: string) {
  const fragments = useState<Record<string, ShellFragment>>(stateKey, () => ({}));

  function registerFragment(fragment: ShellFragment): void {
    const fragmentName = fragment.fragmentName?.trim();
    const pluginId = fragment.pluginId?.trim();
    if (!fragmentName || !pluginId || (!fragment.contentHtml && !fragment.mount)) {
      return;
    }

    fragments.value = {
      ...fragments.value,
      [fragmentName]: { ...fragment, fragmentName, pluginId }
    };
  }

  function mountFragment(fragmentName: string, container: HTMLElement): (() => void) | void {
    const fragment = fragments.value[fragmentName?.trim()];
    if (!fragment || !container) {
      return;
    }

    if (fragment.contentHtml) {
      container.innerHTML = fragment.contentHtml;
    }

    if (fragment.mount) {
      return fragment.mount(container);
    }
  }

  function listFragmentNames(): string[] {
    return Object.keys(fragments.value).sort();
  }

  return {
    registerFragment,
    mountFragment,
    listFragmentNames
  };
}
