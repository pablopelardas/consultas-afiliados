<template>
  <div>
    <AvisoDeError :error="errorDeCarga" @reintentar="cargar" />
    <AvisoDeError :error="errorDeAccion" />

    <p class="conteo" v-if="!cargando">
      {{ afiliados.length }} {{ afiliados.length === 1 ? 'afiliado activo' : 'afiliados activos' }}
    </p>

    <div v-if="cargando" class="cargando">
      <span class="hilandero" />
      Cargando afiliados…
    </div>

    <div v-else-if="afiliados.length" class="tabla-envoltorio">
      <table>
        <thead>
          <tr>
            <th class="col-id">#</th>
            <th>Afiliado</th>
            <th>Contacto</th>
            <th>Plan</th>
            <th>Estado</th>
            <th class="col-acciones"></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="a in afiliados" :key="a.id">
            <tr>
              <td class="col-id">{{ a.id }}</td>
              <td>
                <span class="principal">{{ a.nombreCompleto }}</span><br>
                <span class="secundario">DNI {{ a.dni }}</span>
              </td>
              <td>
                <span>{{ a.email }}</span><br>
                <span class="secundario">{{ a.telefono }}</span>
              </td>
              <td>{{ a.plan }}</td>
              <td>
                <span :class="['badge', a.bloqueado ? 'badge-cancelado' : 'badge-atendido']">
                  {{ a.bloqueado ? 'Bloqueado' : 'Activo' }}
                </span>
              </td>
              <td class="col-acciones">
                <div class="acciones">
                  <button
                    class="btn-secundario btn-chico"
                    :disabled="cargandoCupo === a.id"
                    @click="alternarCupo(a.id)"
                  >
                    {{ etiquetaDeCupo(a.id) }}
                  </button>
                  <BotonConConfirmacion
                    etiqueta="Dar de baja"
                    pregunta="¿Dar de baja?"
                    peligro
                    chico
                    :cargando="dandoDeBaja === a.id"
                    :disabled="dandoDeBaja !== null"
                    @confirmado="darDeBaja(a.id)"
                  />
                </div>
              </td>
            </tr>
            <tr v-if="a.id in cupos" class="fila-cupo">
              <td colspan="6">
                <CupoNutricion :cupo="cupos[a.id]" :cargando="cargandoCupo === a.id" />
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <p v-else-if="!errorDeCarga" class="vacio">No hay afiliados registrados.</p>
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
.conteo {
  margin: 0 0 var(--e-md);
  color: var(--texto-apagado);
  font-size: var(--t-sm);
}

.acciones {
  display: flex;
  align-items: center;
  gap: var(--e-xs);
}

.fila-cupo td {
  padding: var(--e-sm) var(--e-md) 0;
  background: var(--superficie);
}

.fila-cupo:hover td { background: var(--superficie); }
</style>
