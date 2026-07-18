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
      ],
    },
  ],
})

router.beforeEach((to) => authGuard(to))

// A 401 from any API call ends the session — send the user back to login.
setUnauthorizedHandler(() => {
  void router.push('/login')
})
