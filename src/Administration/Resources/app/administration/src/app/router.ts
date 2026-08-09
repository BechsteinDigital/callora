import { createRouter, createWebHistory } from 'vue-router'
import { authGuard } from '@/app/routeGuard'
import { setUnauthorizedHandler } from '@/core/http'

// Breadcrumb parents, declared once so a section and its detail routes cannot
// drift apart. `title` also drives the document title.
const USERS = { label: 'Benutzer', to: '/users' }
const ROLES = { label: 'Rollen', to: '/roles' }
const WORKSPACES = { label: 'Workspaces', to: '/workspaces' }

export const router = createRouter({
  history: createWebHistory('/admin/'),
  routes: [
    {
      path: '/login',
      component: () => import('@/modules/auth/LoginView.vue'),
      meta: { public: true, title: 'Anmelden' },
    },
    {
      path: '/',
      component: () => import('@/app/AppShell.vue'),
      children: [
        { path: '', component: () => import('@/modules/dashboard/DashboardView.vue'), meta: { title: 'Übersicht' } },
        {
          path: 'onboarding',
          component: () => import('@/modules/onboarding/OnboardingView.vue'),
          meta: { title: 'Erste Schritte' },
        },
        { path: 'users', component: () => import('@/modules/users/UsersListView.vue'), meta: { title: 'Benutzer' } },
        {
          path: 'users/new',
          component: () => import('@/modules/users/UserDetailView.vue'),
          meta: { title: 'Neuer Benutzer', parent: USERS },
        },
        {
          path: 'users/:userId',
          component: () => import('@/modules/users/UserDetailView.vue'),
          meta: { title: 'Benutzer bearbeiten', parent: USERS },
        },
        { path: 'roles', component: () => import('@/modules/roles/RolesListView.vue'), meta: { title: 'Rollen' } },
        // Baum links, Detail rechts — der Knoten steht in der URL, damit ein Neuladen (und
        // ein geteilter Link) dieselbe Seite zeigt.
        {
          path: 'surfaces',
          component: () => import('@/modules/surfaces/SurfacesView.vue'),
          meta: { title: 'Flächen' },
        },
        {
          path: 'surfaces/:surfaceKey',
          component: () => import('@/modules/surfaces/SurfacesView.vue'),
          meta: { title: 'Flächen' },
        },
        {
          path: 'roles/new',
          component: () => import('@/modules/roles/RoleDetailView.vue'),
          meta: { title: 'Neue Rolle', parent: ROLES },
        },
        {
          path: 'roles/:role',
          component: () => import('@/modules/roles/RoleDetailView.vue'),
          meta: { title: 'Rolle bearbeiten', parent: ROLES },
        },
        {
          path: 'plugins',
          component: () => import('@/modules/plugins/PluginsListView.vue'),
          meta: { title: 'Plugins' },
        },
        {
          path: 'entitlements',
          component: () => import('@/modules/entitlements/EntitlementsListView.vue'),
          meta: { title: 'Berechtigungen' },
        },
        {
          path: 'workspaces',
          component: () => import('@/modules/workspaces/WorkspacesListView.vue'),
          meta: { title: 'Workspaces' },
        },
        {
          path: 'workspaces/new',
          component: () => import('@/modules/workspaces/WorkspaceDetailView.vue'),
          meta: { title: 'Neuer Workspace', parent: WORKSPACES },
        },
        {
          path: 'workspaces/:workspaceKey',
          component: () => import('@/modules/workspaces/WorkspaceDetailView.vue'),
          meta: { title: 'Workspace bearbeiten', parent: WORKSPACES },
        },
        {
          path: 'tenants',
          component: () => import('@/modules/tenants/TenantsListView.vue'),
          meta: { title: 'Mandanten' },
        },
        { path: 'flows', component: () => import('@/modules/flows/FlowsListView.vue'), meta: { title: 'Flows' } },
        { path: 'themes', component: () => import('@/modules/themes/ThemesView.vue'), meta: { title: 'Themes' } },
        { path: 'jobs', component: () => import('@/modules/jobs/JobsListView.vue'), meta: { title: 'Jobs' } },
        {
          path: 'webhooks',
          component: () => import('@/modules/webhooks/WebhooksListView.vue'),
          meta: { title: 'Webhooks' },
        },
        {
          path: 'config',
          component: () => import('@/modules/config/SystemConfigView.vue'),
          meta: { title: 'Konfiguration' },
        },
        { path: 'media', component: () => import('@/modules/media/MediaLibraryView.vue'), meta: { title: 'Medien' } },
        // Neutral host for plugin-contributed admin pages; the target of plugin
        // navigation entries (e.g. /extensions/communication).
        {
          path: 'extensions/:pluginId',
          component: () => import('@/modules/extensions/ExtensionHostView.vue'),
          meta: { title: 'Erweiterung' },
        },
      ],
    },
  ],
})

router.beforeEach((to) => authGuard(to))

// The browser tab and the history entries name the page, not just the product.
router.afterEach((to) => {
  const title = typeof to.meta.title === 'string' ? to.meta.title : null
  document.title = title ? `${title} · Callora` : 'Callora Administration'
})

// A 401 from any API call ends the session — send the user back to login.
setUnauthorizedHandler(() => {
  void router.push('/login')
})
