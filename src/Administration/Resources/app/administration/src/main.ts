import { createApp } from 'vue'
import App from './App.vue'
import '@/core/design/tokens.scss'
import { router } from '@/app/router'

createApp(App).use(router).mount('#app')
