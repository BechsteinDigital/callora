import CommunicationAdminPage from './CommunicationAdminPage.vue'

// The host admin shell exposes its plugin API on window.CalloraAdmin (set before any
// plugin bundle loads). We register this plugin's admin page against the slot the
// shell renders at /extensions/communication. Registration is synchronous at
// top-level so the loader attributes it to this plugin.
interface CalloraAdminGlobal {
  registerExtension(slot: string, component: unknown, order?: number): void
}

const admin = (window as unknown as { CalloraAdmin?: CalloraAdminGlobal }).CalloraAdmin
admin?.registerExtension('extension.page.communication', CommunicationAdminPage)
