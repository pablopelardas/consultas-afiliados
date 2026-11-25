import { createRouter, createWebHashHistory } from 'vue-router'
import ConsultasList from '../views/ConsultasList.vue'
import AfiliadosList from '../views/AfiliadosList.vue'

// Consultas es una sola pantalla. El alta y el detalle son diálogos sobre la
// lista, pero siguen siendo rutas: así el enlace directo, el botón "atrás" y
// compartir una URL funcionan igual que con una pantalla aparte.
// `/consultas/nuevo` va antes de `/consultas/:id`, que si no se lo come como id.
const routes = [
  { path: '/',                redirect: '/consultas'   },
  { path: '/consultas',       component: ConsultasList },
  { path: '/consultas/nuevo', component: ConsultasList },
  { path: '/consultas/:id',   component: ConsultasList },
  { path: '/afiliados',       component: AfiliadosList }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

export default router
