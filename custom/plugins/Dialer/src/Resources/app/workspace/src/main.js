(function registerDialerWorkspaceUiExtensions() {
  window.calloraWorkspaceUi = window.calloraWorkspaceUi || {};
  const bridge = window.calloraWorkspaceUi;

  function apiBase(workspaceKey) {
    return '/api/ext/admin/plugins/dialer/workspaces/' + encodeURIComponent(workspaceKey);
  }

  async function fetchJson(url, options) {
    const response = await fetch(url, Object.assign({ credentials: 'include' }, options));
    if (!response.ok) {
      throw new Error('Dialer request failed with status ' + response.status);
    }
    return response.status === 204 ? null : response.json();
  }

  function describeRun(run) {
    if (!run) {
      return 'No dial run yet.';
    }
    const attempts = Array.isArray(run.attempts) ? run.attempts.length : 0;
    return 'Latest run: ' + (run.status || 'Unknown') + ' (' + attempts + ' attempts)';
  }

  const dialerBlock = {
    blockName: 'workspace.calls.after',
    pluginId: 'dialer',
    mode: 'append',
    priority: 100,
    mount(container, context) {
      const workspaceKey = (context && context.workspaceKey) || 'default';
      let disposed = false;

      container.innerHTML =
        '<div class="border border-default rounded-lg p-4 space-y-3">' +
        '  <div class="flex items-center justify-between gap-2">' +
        '    <p class="font-semibold">Dialer</p>' +
        '    <span class="text-xs uppercase text-muted">plugin: dialer</span>' +
        '  </div>' +
        '  <p class="text-sm text-muted" data-dialer-status>Loading dial run state…</p>' +
        '  <p class="text-sm text-muted" data-dialer-numbers></p>' +
        '  <div class="flex gap-2">' +
        '    <button type="button" data-dialer-start class="px-3 py-1.5 rounded-md text-sm font-medium bg-primary text-inverted disabled:opacity-50">Start run</button>' +
        '    <button type="button" data-dialer-refresh class="px-3 py-1.5 rounded-md text-sm font-medium border border-default">Refresh</button>' +
        '  </div>' +
        '</div>';

      const statusLine = container.querySelector('[data-dialer-status]');
      const numbersLine = container.querySelector('[data-dialer-numbers]');
      const startButton = container.querySelector('[data-dialer-start]');
      const refreshButton = container.querySelector('[data-dialer-refresh]');

      async function refreshPanel() {
        try {
          const [latestRun, numbers] = await Promise.all([
            fetchJson(apiBase(workspaceKey) + '/runs/latest').catch(function () { return null; }),
            fetchJson(apiBase(workspaceKey) + '/numbers').catch(function () { return null; })
          ]);
          if (disposed) {
            return;
          }
          statusLine.textContent = describeRun(latestRun);
          const count = Array.isArray(numbers) ? numbers.length : (numbers && numbers.items ? numbers.items.length : 0);
          numbersLine.textContent = count + ' number(s) configured. Manage them in the admin extension pages.';
          startButton.disabled = !!(latestRun && latestRun.status === 'Running') || count === 0;
        } catch (error) {
          if (!disposed) {
            statusLine.textContent = 'Dialer state unavailable: ' + error.message;
          }
        }
      }

      startButton.addEventListener('click', async function () {
        startButton.disabled = true;
        try {
          await fetchJson(apiBase(workspaceKey) + '/runs', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ callTimeoutSeconds: 30 })
          });
        } catch (error) {
          if (!disposed) {
            statusLine.textContent = 'Starting run failed: ' + error.message;
          }
        }
        await refreshPanel();
      });
      refreshButton.addEventListener('click', refreshPanel);

      void refreshPanel();
      return function cleanup() {
        disposed = true;
      };
    }
  };

  if (typeof bridge.registerBlockExtension === 'function') {
    bridge.registerBlockExtension(dialerBlock);
  } else {
    const queuedBlockExtensions = bridge.queuedBlockExtensions || [];
    queuedBlockExtensions.push(dialerBlock);
    bridge.queuedBlockExtensions = queuedBlockExtensions;
  }
})();
