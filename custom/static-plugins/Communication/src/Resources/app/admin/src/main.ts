import { registerPage } from '@callora/admin/extensions'
import CommunicationAdminPage from './CommunicationAdminPage.vue'

// Contributes this plugin's admin page, which the shell renders at /extensions/communication.
// Registration is synchronous at top level so the loader attributes it to this plugin.
//
// registerPage rather than a raw slot string: the shell's extension points are a generated
// literal union, and a mistyped name is a silent no-op everywhere it is a plain string.
registerPage('communication', CommunicationAdminPage)
