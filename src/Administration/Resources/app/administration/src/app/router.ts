import { createRouter, createWebHistory } from 'vue-router'
import { authGuard } from '@/app/routeGuard'
import { setUnauthorizedHandler } from '@/core/http'

export const router = createRouter({
  history: createWebHistory('/admin/'),
  routes: [
    {
      path: '/login',
      component: () => import('@/modules/auth/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('@/app/AppShell.vue'),
      children: [
        { path: '', component: () => import('@/modules/dashboard/DashboardView.vue') },
        { path: 'users', component: () => import('@/modules/users/UsersListView.vue') },
        { path: 'users/new', component: () => import('@/modules/users/UserDetailView.vue') },
        { path: 'users/:userId', component: () => import('@/modules/users/UserDetailView.vue') },
        { path: 'roles', component: () => import('@/modules/roles/RolesListView.vue') },
        { path: 'roles/new', component: () => import('@/modules/roles/RoleDetailView.vue') },
        { path: 'roles/:role', component: () => import('@/modules/roles/RoleDetailView.vue') },
        { path: 'plugins', component: () => import('@/modules/plugins/PluginsListView.vue') },
        { path: 'entitlements', component: () => import('@/modules/entitlements/EntitlementsListView.vue') },
        { path: 'workspaces', component: () => import('@/modules/workspaces/WorkspacesListView.vue') },
        { path: 'workspaces/new', component: () => import('@/modules/workspaces/WorkspaceDetailView.vue') },
        { path: 'workspaces/:workspaceKey', component: () => import('@/modules/workspaces/WorkspaceDetailView.vue') },
        { path: 'tenants', component: () => import('@/modules/tenants/TenantsListView.vue') },
        { path: 'flows', component: () => import('@/modules/flows/FlowsListView.vue') },
        { path: 'themes', component: () => import('@/modules/themes/ThemesView.vue') },
        { path: 'jobs', component: () => import('@/modules/jobs/JobsListView.vue') },
        { path: 'webhooks', component: () => import('@/modules/webhooks/WebhooksListView.vue') },
        { path: 'config', component: () => import('@/modules/config/SystemConfigView.vue') },
        { path: 'media', component: () => import('@/modules/media/MediaLibraryView.vue') },
      ],
    },
  ],
})

router.beforeEach((to) => authGuard(to))

// A 401 from any API call ends the session — send the user back to login.
setUnauthorizedHandler(() => {
  void router.push('/login')
})
