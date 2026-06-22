import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'Login', component: () => import('@/views/Login.vue') },
    { path: '/dashboard', name: 'Home', component: () => import('@/views/Home.vue'), meta: { requiresAuth: true } },
    { path: '/devices', name: 'Devices', component: () => import('@/views/Devices.vue'), meta: { requiresAuth: true } },
    { path: '/devices/:id/points', name: 'Points', component: () => import('@/views/Points.vue'), meta: { requiresAuth: true } },
    { path: '/monitor', name: 'Monitor', component: () => import('@/views/Monitor.vue'), meta: { requiresAuth: true } },
    { path: '/history', name: 'History', component: () => import('@/views/History.vue'), meta: { requiresAuth: true } },
    { path: '/config/storage', name: 'StorageConfig', component: () => import('@/views/config/StorageConfig.vue'), meta: { requiresAuth: true } },
    { path: '/config/forward', name: 'ForwardConfig', component: () => import('@/views/config/ForwardConfig.vue'), meta: { requiresAuth: true } },
    { path: '/config/system', name: 'SystemConfig', component: () => import('@/views/config/SystemConfig.vue'), meta: { requiresAuth: true } },
    { path: '/logs', name: 'Logs', component: () => import('@/views/Logs.vue'), meta: { requiresAuth: true } },
  ],
})

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    const user = localStorage.getItem('plc_user')
    if (!user) return '/'
  }
})

export default router
