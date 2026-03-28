import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/devices'
    },
    {
      path: '/devices',
      name: 'DeviceList',
      component: () => import('../views/DeviceList.vue')
    },
    {
      path: '/preview',
      name: 'LivePreview',
      component: () => import('../views/LivePreview.vue')
    },
    {
      path: '/diagnostic',
      name: 'DiagnosticPanel',
      component: () => import('../views/DiagnosticPanel.vue')
    },
    {
      path: '/ai-chat',
      name: 'AiChat',
      component: () => import('../views/AiChat.vue')
    }
  ]
})

export default router
