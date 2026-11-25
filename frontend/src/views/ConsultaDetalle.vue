<template>
  <div>
    <router-link to="/consultas" style="font-size:14px; color:#1a73e8">← Volver a consultas</router-link>

    <div style="margin-top: 20px; max-width: 560px">
      <AvisoDeError :error="error" @reintentar="cargar" />

      <p v-if="cargando">Cargando consulta…</p>

      <div v-else-if="consulta" class="card">
        <h2>Consulta #{{ consulta.id }}</h2>
        <div class="detail-row"><span class="label">Afiliado</span><span>{{ consulta.afiliado?.nombreCompleto }}</span></div>
        <div class="detail-row"><span class="label">DNI</span><span>{{ consulta.afiliado?.dni }}</span></div>
        <div class="detail-row"><span class="label">Profesional</span><span>{{ consulta.profesional?.nombreCompleto }}</span></div>
        <div class="detail-row"><span class="label">Especialidad</span><span>{{ consulta.profesional?.especialidad }}</span></div>
        <div class="detail-row"><span class="label">Fecha y hora</span><span>{{ formatFecha(consulta.fechaHora) }}</span></div>
        <div class="detail-row"><span class="label">Modalidad</span><span>{{ consulta.modalidad }}</span></div>
        <div class="detail-row"><span class="label">Estado</span><span>{{ consulta.estado }}</span></div>
        <div class="detail-row"><span class="label">Motivo</span><span>{{ consulta.motivo }}</span></div>

        <!-- Las acciones son las que el backend declara posibles. El dropdown
             ofrecía los cinco estados, incluidos los que la tabla de
             transiciones rechaza con un 409. -->
        <p v-if="!transiciones.length" class="sin-acciones">
          Una consulta en estado {{ consulta.estado }} no admite cambios de estado.
        </p>

        <template v-else>
          <div style="margin-top: 24px">
            <div class="form-group">
              <label>Cambiar estado</label>
              <select v-model="nuevoEstado" :disabled="ocupado">
                <option v-for="e in transiciones" :key="e" :value="e">{{ e }}</option>
              </select>
            </div>
            <button :disabled="ocupado" style="margin-bottom: 16px" @click="ejecutar('estado')">
              {{ enCurso === 'estado' ? 'Actualizando…' : 'Actualizar estado' }}
            </button>
          </div>

          <div class="acciones">
            <button
              v-if="permite('Atendido')"
              :disabled="ocupado"
              @click="ejecutar('atencion')"
            >
              {{ enCurso === 'atencion' ? 'Registrando…' : 'Registrar atención' }}
            </button>

            <BotonConConfirmacion
              v-if="permite('Cancelado')"
              etiqueta="Cancelar consulta"
              pregunta="¿Cancelar la consulta?"
              peligro
              :cargando="enCurso === 'cancelacion'"
              :disabled="ocupado"
              @confirmado="ejecutar('cancelacion')"
            />

            <button
              v-if="permite('Inasistencia')"
              :disabled="ocupado"
              @click="ejecutar('inasistencia')"
            >
              {{ enCurso === 'inasistencia' ? 'Marcando…' : 'Marcar inasistencia' }}
            </button>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script>
import { consultasApi } from '../services/api'
import { instante } from '../services/fechas'
import AvisoDeError from '../components/AvisoDeError.vue'
import BotonConConfirmacion from '../components/BotonConConfirmacion.vue'

export default {
  name: 'ConsultaDetalle',
  components: { AvisoDeError, BotonConConfirmacion },
  data () {
    return {
      consulta: null,
      nuevoEstado: null,
      cargando: false,
      enCurso: null,
      error: null
    }
  },
  computed: {
    transiciones () {
      return this.consulta?.transicionesPermitidas ?? []
    },
    ocupado () {
      return this.enCurso !== null
    }
  },
  mounted () {
    this.cargar()
  },
  // Vue Router reusa el componente cuando solo cambia el parámetro, así que
  // `mounted` no vuelve a correr: sin esto, ir de /consultas/2 a /consultas/1
  // deja en pantalla la consulta anterior.
  watch: {
    '$route.params.id' () {
      this.cargar()
    }
  },
  methods: {
    // La fecha del turno es un instante: se muestra en la hora local de quien
    // mira. El cupo usa el otro criterio (ver services/fechas.js).
    formatFecha: instante,

    permite (estado) {
      return this.transiciones.includes(estado)
    },

    async cargar () {
      this.cargando = true
      this.error = null
      try {
        this.aplicar(await consultasApi.getById(this.$route.params.id))
      } catch (error) {
        this.error = error
      } finally {
        this.cargando = false
      }
    },

    aplicar (consulta) {
      this.consulta = consulta
      // El estado elegido tiene que ser uno de los posibles, no el actual: el
      // actual ya no está entre las transiciones admitidas.
      this.nuevoEstado = consulta.transicionesPermitidas?.[0] ?? null
    },

    /**
     * Ejecuta la operación y aplica la consulta que devuelve.
     *
     * Las cuatro comparten la forma —una llamada, un resultado que reemplaza el
     * estado local, un error que se muestra— y el código base las tenía
     * repetidas cuatro veces, dos de ellas sin manejo de error.
     */
    async ejecutar (operacion) {
      const llamadas = {
        estado: () => consultasApi.actualizarEstado(this.consulta.id, { estado: this.nuevoEstado }),
        atencion: () => consultasApi.registrarAtencion(this.consulta.id),
        cancelacion: () => consultasApi.cancelar(this.consulta.id),
        inasistencia: () => consultasApi.marcarInasistencia(this.consulta.id)
      }

      this.enCurso = operacion
      this.error = null
      try {
        this.aplicar(await llamadas[operacion]())
      } catch (error) {
        this.error = error
      } finally {
        this.enCurso = null
      }
    }
  }
}
</script>

<style scoped>
.detail-row {
  display: flex;
  padding: 10px 0;
  border-bottom: 1px solid #f0f0f0;
  font-size: 14px;
}
.label {
  width: 130px;
  font-weight: 600;
  color: #666;
  flex-shrink: 0;
}
.acciones {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  align-items: center;
}
.sin-acciones {
  margin: 24px 0 0;
  font-size: 13px;
  color: #777;
  font-style: italic;
}
button:disabled {
  background: #c5cdd6;
  cursor: not-allowed;
}
</style>
