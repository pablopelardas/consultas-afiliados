<template>
  <div class="hoja">
    <router-link to="/consultas" class="volver">← Volver a consultas</router-link>

    <AvisoDeError :error="error" @reintentar="cargar" />

    <div v-if="cargando" class="cargando">
      <span class="hilandero" />
      Cargando consulta…
    </div>

    <template v-else-if="consulta">
      <div class="cabecera">
        <h2>Consulta #{{ consulta.id }}</h2>
        <span :class="['badge', `badge-${consulta.estado?.toLowerCase()}`]">{{ consulta.estado }}</span>
      </div>

      <dl class="datos">
        <div><dt>Afiliado</dt><dd>{{ consulta.afiliado?.nombreCompleto }}</dd></div>
        <div><dt>DNI</dt><dd>{{ consulta.afiliado?.dni }}</dd></div>
        <div><dt>Profesional</dt><dd>{{ consulta.profesional?.nombreCompleto }}</dd></div>
        <div><dt>Especialidad</dt><dd>{{ consulta.profesional?.especialidad }}</dd></div>
        <div><dt>Fecha y hora</dt><dd>{{ formatFecha(consulta.fechaHora) }}</dd></div>
        <div><dt>Modalidad</dt><dd>{{ consulta.modalidad }}</dd></div>
        <div class="ancho"><dt>Motivo</dt><dd>{{ consulta.motivo }}</dd></div>
      </dl>

      <!-- Las acciones son las que el backend declara posibles. El selector
           ofrecía los cinco estados, incluidos los que la tabla de
           transiciones rechaza con un 409. -->
      <p v-if="!transiciones.length" class="sin-acciones">
        Una consulta en estado {{ consulta.estado }} no admite cambios de estado.
      </p>

      <div v-else class="acciones">
        <div class="cambio">
          <label for="estado">Cambiar estado</label>
          <div class="cambio-control">
            <select id="estado" v-model="nuevoEstado" :disabled="ocupado">
              <option v-for="e in transiciones" :key="e" :value="e">{{ e }}</option>
            </select>
            <button :disabled="ocupado" @click="ejecutar('estado')">
              <span v-if="enCurso === 'estado'" class="hilandero" />
              {{ enCurso === 'estado' ? 'Actualizando…' : 'Actualizar' }}
            </button>
          </div>
        </div>

        <div class="atajos">
          <button
            v-if="permite('Atendido')"
            class="btn-secundario"
            :disabled="ocupado"
            @click="ejecutar('atencion')"
          >
            {{ enCurso === 'atencion' ? 'Registrando…' : 'Registrar atención' }}
          </button>

          <button
            v-if="permite('Inasistencia')"
            class="btn-secundario"
            :disabled="ocupado"
            @click="ejecutar('inasistencia')"
          >
            {{ enCurso === 'inasistencia' ? 'Marcando…' : 'Marcar inasistencia' }}
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
        </div>
      </div>
    </template>
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
.hoja { max-width: 620px; }

.volver {
  display: inline-block;
  margin-bottom: var(--e-md);
  color: var(--texto-sutil);
  font-size: var(--t-sm);
  text-decoration: none;
}
.volver:hover { color: var(--marca); }

.cabecera {
  display: flex;
  align-items: center;
  gap: var(--e-sm);
  margin-bottom: var(--e-md);
}

.cabecera h2 { font-size: var(--t-xl); }

.datos {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--e-sm) var(--e-lg);
  margin: 0;
  padding: var(--e-md) 0;
  border-top: 1px solid var(--borde-sutil);
  border-bottom: 1px solid var(--borde-sutil);
}

.datos .ancho { grid-column: 1 / -1; }
.datos dt { color: var(--texto-apagado); font-size: var(--t-xs); font-weight: 600; }
.datos dd { margin: 2px 0 0; font-size: var(--t-sm); color: var(--texto-fuerte); }

.sin-acciones {
  margin: var(--e-md) 0 0;
  color: var(--texto-apagado);
  font-size: var(--t-sm);
  font-style: italic;
}

.acciones { margin-top: var(--e-lg); }

.cambio label {
  display: block;
  margin-bottom: var(--e-2xs);
  color: var(--texto-sutil);
  font-size: var(--t-xs);
  font-weight: 600;
}

.cambio-control { display: flex; gap: var(--e-xs); }
.cambio-control select { width: 220px; }
.cambio-control button { height: 40px; }

.atajos {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--e-xs);
  margin-top: var(--e-md);
  padding-top: var(--e-md);
  border-top: 1px solid var(--borde-sutil);
}

.hilandero { border-top-color: currentColor; }

@media (max-width: 600px) {
  .datos { grid-template-columns: 1fr; }
  .cambio-control select { width: 100%; }
}
</style>
