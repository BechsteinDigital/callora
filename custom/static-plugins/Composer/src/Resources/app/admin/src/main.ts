import { registerPage } from '@callora/admin/extensions'
import ComposerAdminPage from './ComposerAdminPage.vue'

// Synchron auf oberster Ebene, damit der Loader die Registrierung diesem Plugin zuordnet.
registerPage('composer', ComposerAdminPage)
