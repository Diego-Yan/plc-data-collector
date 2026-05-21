import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'Login', component: () => import('@/views/Login.vue') },
    { path: '/dashboard', name: 'Home', component: () => import('@/views/Home.vue') },
    { path: '/devices', name: 'Devices', component: () => import('@/views/Devices.vue') },
    { path: '/devices/:id/points', name: 'Points', component: () => import('@/views/Points.vue') },
    { path: '/monitor', name: 'Monitor', component: () => import('@/views/Monitor.vue') },
    { path: '/history', name: 'History', component: () => import('@/views/History.vue') },
    { path: '/config/storage', name: 'StorageConfig', component: () => import('@/views/config/StorageConfig.vue') },
    { path: '/config/forward', name: 'ForwardConfig', component: () => import('@/views/config/ForwardConfig.vue') },
    { path: '/config/system', name: 'SystemConfig', component: () => import('@/views/config/SystemConfig.vue') },
    { path: '/logs', name: 'Logs', component: () => import('@/views/Logs.vue') },
  ],
})

export default router
