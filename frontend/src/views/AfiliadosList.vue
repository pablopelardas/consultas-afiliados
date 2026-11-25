<template>
  <div>
    <h2>Afiliados</h2>

    <AvisoDeError :error="errorDeCarga" @reintentar="cargar" />
    <AvisoDeError :error="errorDeAccion" />

    <p v-if="cargando">Cargando afiliados…</p>

    <table v-else-if="afiliados.length">
      <thead>
        <tr>
          <th>#</th>
          <th>Nombre</th>
          <th>DNI</th>
          <th>Email</th>
          <th>Teléfono</th>
          <th>Plan</th>
          <th>Bloqueado</th>
          <th>Acciones</th>
        </tr>
      </thead>
      <tbody>
        <template v-for="a in afiliados" :key="a.id">
          <tr>
            <td>{{ a.id }}</td>
            <td>{{ a.nombreCompleto }}</td>
            <td>{{ a.dni }}</td>
            <td>{{ a.email }}</td>
            <td>{{ a.telefono }}</td>
            <td>{{ a.plan }}</td>
            <td>
              <span v-if="a.bloqueado" style="color: #d32f2f; font-weight: 600">Sí</span>
              <span v-else style="color: #388e3c">No</span>
            </td>
            <td class="acciones">
              <button class="btn-neutro" :disabled="cargandoCupo === a.id" @click="alternarCupo(a.id)">
                {{ etiquetaDeCupo(a.id) }}
              </button>
              <BotonConConfirmacion
                etiqueta="Dar de baja"
                pregunta="¿Dar de baja?"
                peligro
                :cargando="dandoDeBaja === a.id"
                :disabled="dandoDeBaja !== null"
                @confirmado="darDeBaja(a.id)"
              />
            </td>
          </tr>
          <tr v-if="a.id in cupos">
            <td colspan="8" class="fila-cupo">
              <CupoNutricion :cupo="cupos[a.id]" :cargando="cargandoCupo === a.id" />
            </td>
          </tr>
        </template>
      </tbody>
    </table>

    <p v-else-if="!errorDeCarga">No hay afiliados registrados.</p>
  </div>
</template>

<script>
import { afiliadosApi } from '../services/api'
import AvisoDeError from '../components/AvisoDeError.vue'
import BotonConConfirmacion from '../components/BotonConConfirmacion.vue'
import CupoNutricion from '../components/CupoNutricion.vue'

export default {
  name: 'AfiliadosList',
  components: { AvisoDeError, BotonConConfirmacion, CupoNutricion },
  data () {
    return {
      afiliados: [],
      // El cupo se pide por afiliado y a demanda: una columna para todos serían
      // tantas llamadas como filas, para un dato que casi nunca se mira.
      cupos: {},
      cargando: false,
      cargandoCupo: null,
      dandoDeBaja: null,
      errorDeCarga: null,
      errorDeAccion: null
    }
  },
  mounted () {
    this.cargar()
  },
  methods: {
    etiquetaDeCupo (id) {
      if (this.cargandoCupo === id) return 'Consultando…'
      return id in this.cupos ? 'Ocultar cupo' : 'Ver cupo'
    },

    async cargar () {
      this.cargando = true
      this.errorDeCarga = null
      try {
        this.afiliados = await afiliadosApi.getAll()
      } catch (error) {
        this.errorDeCarga = error
      } finally {
        this.cargando = false
      }
    },

    async alternarCupo (id) {
      if (id in this.cupos) {
        delete this.cupos[id]
        return
      }

      this.cargandoCupo = id
      this.errorDeAccion = null
      // Se reserva la clave antes de la respuesta para que la fila se abra con
      // el indicador de carga en lugar de aparecer recién al final.
      this.cupos[id] = null
      try {
        this.cupos[id] = await afiliadosApi.getCupoNutricion(id)
      } catch (error) {
        delete this.cupos[id]
        this.errorDeAccion = error
      } finally {
        this.cargandoCupo = null
      }
    },

    // Baja lógica: el afiliado deja de listarse, pero su historial de consultas
    // se conserva. Por eso desaparece de la tabla sin recargar nada.
    async darDeBaja (id) {
      this.dandoDeBaja = id
      this.errorDeAccion = null
      try {
        await afiliadosApi.darDeBaja(id)
        this.afiliados = this.afiliados.filter(a => a.id !== id)
        delete this.cupos[id]
      } catch (error) {
        this.errorDeAccion = error
      } finally {
        this.dandoDeBaja = null
      }
    }
  }
}
</script>

<style scoped>
.acciones {
  display: flex;
  align-items: center;
  gap: 10px;
}
.btn-neutro { background: #e0e0e0; color: #333 }
.btn-neutro:hover { background: #cfcfcf }
.btn-neutro:disabled { background: #eee; color: #999; cursor: not-allowed }
.fila-cupo {
  background: #fafbfc;
  padding: 12px 16px 2px;
}
</style>
