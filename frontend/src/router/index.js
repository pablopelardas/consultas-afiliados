import { createRouter, createWebHashHistory } from 'vue-router'
import ConsultasList from '../views/ConsultasList.vue'
import ConsultaDetalle from '../views/ConsultaDetalle.vue'
import AfiliadosList from '../views/AfiliadosList.vue'

// `/consultas/nuevo` monta la misma lista: el alta es un modal por encima, no
// otra pantalla. Sigue siendo una ruta para que el ítem del menú, el enlace
// directo y el botón "atrás" funcionen como con cualquier otra.
// Va antes de `/consultas/:id`, que si no se lo come como un id.
const routes = [
  { path: '/',                redirect: '/consultas'     },
  { path: '/consultas',       component: ConsultasList   },
  { path: '/consultas/nuevo', component: ConsultasList   },
  { path: '/consultas/:id',   component: ConsultaDetalle },
  { path: '/afiliados',       component: AfiliadosList   }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

export default router
