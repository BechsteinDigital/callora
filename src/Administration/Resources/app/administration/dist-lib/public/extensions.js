function t() {
  return globalThis.CalloraAdmin;
}
function o(e) {
  console.warn(`[callora-admin] admin shell not initialised; ${e} was not registered.`);
}
function s(e, n, r) {
  const i = t();
  if (!i) {
    o(`slot "${e}"`);
    return;
  }
  i.registerExtension(e, n, r);
}
function a(e, n, r) {
  const i = t();
  if (!i) {
    o(`hook "${e}"`);
    return;
  }
  i.registerHook(e, n, r);
}
function c(e, n, r) {
  const i = t();
  if (!i) {
    o(`service "${e}"`);
    return;
  }
  i.registerService(e, n, r);
}
function g(e, n, r) {
  const i = t();
  if (!i) {
    o(`page for "${e}"`);
    return;
  }
  i.registerExtension(`extension.page.${e}`, n, r);
}
export {
  s as registerExtension,
  a as registerHook,
  g as registerPage,
  c as registerService,
  t as resolveAdminApi
};
