<template>
  <div>
    <AvisoDeError :error="error" @reintentar="cargar" />

    <div v-if="cargando" class="cargando">
      <span class="hilandero" />
      Cargando consulta…
    </div>

    <template v-else-if="consulta">
      <span :class="['badge', `badge-${consulta.estado?.toLowerCase()}`]">{{ consulta.estado }}</span>

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
        <button
          v-for="(op, i) in avance"
          :key="op.estado"
          :class="i > 0 && 'btn-secundario'"
          :disabled="ocupado"
          @click="ejecutar(op.estado)"
        >
          <span v-if="enCurso === op.estado" class="hilandero" />
          {{ enCurso === op.estado ? op.enCurso : op.etiqueta }}
        </button>

        <BotonConConfirmacion
          v-for="op in destructivas"
          :key="op.estado"
          class="aparte"
          :etiqueta="op.etiqueta"
          :pregunta="op.pregunta"
          peligro
          :cargando="enCurso === op.estado"
          :disabled="ocupado"
          @confirmado="ejecutar(op.estado)"
        />
      </div>
    </template>
  </div>
</template>

<script>
import { consultasApi } from '../services/api'
import { instante } from '../services/fechas'
import AvisoDeError from './AvisoDeError.vue'
import BotonConConfirmacion from './BotonConConfirmacion.vue'

/**
 * Las transiciones, nombradas por la operación de negocio.
 *
 * Tres tienen endpoint propio porque son hechos distintos —atender, no
 * presentarse, cancelar— y no asignaciones a un campo. Confirmar es la única
 * sin ruta dedicada y usa la genérica, que valida la misma tabla.
 */
// Orden de lectura de la pantalla: el avance del turno primero y lo
// irreversible al final.
const ORDEN = ['Confirmado', 'Atendido', 'Inasistencia', 'Cancelado']

const OPERACIONES = {
  Confirmado: {
    estado: 'Confirmado',
    enCurso: 'Confirmando…',
    etiqueta: 'Confirmar',
    pregunta: '¿Confirmar?',
    llamar: (id) => consultasApi.actualizarEstado(id, { estado: 'Confirmado' })
  },
  Atendido: {
    estado: 'Atendido',
    enCurso: 'Registrando…',
    etiqueta: 'Registrar atención',
    pregunta: '¿Registrar la atención?',
    llamar: (id) => consultasApi.registrarAtencion(id)
  },
  Inasistencia: {
    estado: 'Inasistencia',
    enCurso: 'Marcando…',
    etiqueta: 'Marcar inasistencia',
    pregunta: '¿Marcar inasistencia?',
    llamar: (id) => consultasApi.marcarInasistencia(id)
  },
  Cancelado: {
    estado: 'Cancelado',
    enCurso: 'Cancelando…',
    etiqueta: 'Cancelar consulta',
    pregunta: '¿Cancelar la consulta?',
    peligro: true,
    llamar: (id) => consultasApi.cancelar(id)
  }
}

export default {
  name: 'DetalleConsulta',
  components: { AvisoDeError, BotonConConfirmacion },
  props: {
    id: { type: [String, Number], required: true }
  },
  // `actualizada` lleva la consulta ya modificada: quien lo use decide si
  // refresca una fila, recarga todo o no hace nada.
  emits: ['actualizada'],
  data () {
    return {
      consulta: null,
      cargando: false,
      enCurso: null,
      error: null
    }
  },
  computed: {
    transiciones () {
      return this.consulta?.transicionesPermitidas ?? []
    },

    // El backend decide QUÉ acciones existen; el orden lo decide la interfaz.
    // `transicionesPermitidas` viene en el orden de la tabla del dominio, que
    // para una pantalla es arbitrario: dejaba "Cancelar consulta" en el medio,
    // entre dos acciones inofensivas y a un error de distancia.
    operaciones () {
      return ORDEN
        .filter(estado => this.transiciones.includes(estado))
        .map(estado => OPERACIONES[estado])
    },

    // El avance del turno. El primero es el paso natural desde el estado
    // actual, así que es el único con peso de acción principal.
    avance () {
      return this.operaciones.filter(op => !op.peligro)
    },

    // Separadas y al final: una acción irreversible no comparte grupo con las
    // que se pueden deshacer agendando de nuevo.
    destructivas () {
      return this.operaciones.filter(op => op.peligro)
    },

    ocupado () {
      return this.enCurso !== null
    }
  },
  mounted () {
    this.cargar()
  },
  // El componente se reusa si cambia el id sin desmontarse.
  watch: {
    id () {
      this.cargar()
    }
  },
  methods: {
    // La fecha del turno es un instante: se muestra en la hora local de quien
    // mira. El cupo usa el otro criterio (ver services/fechas.js).
    formatFecha: instante,

    async cargar () {
      this.cargando = true
      this.error = null
      try {
        this.aplicar(await consultasApi.getById(this.id))
      } catch (error) {
        this.error = error
      } finally {
        this.cargando = false
      }
    },

    aplicar (consulta, avisar = false) {
      this.consulta = consulta
      if (avisar) this.$emit('actualizada', consulta)
    },

    /**
     * Ejecuta la transición y aplica la consulta que devuelve.
     *
     * Las cuatro comparten la forma —una llamada, un resultado que reemplaza el
     * estado local, un error que se muestra— y el código base las tenía
     * repetidas cuatro veces, dos de ellas sin manejo de error.
     */
    async ejecutar (estado) {
      this.enCurso = estado
      this.error = null
      try {
        this.aplicar(await OPERACIONES[estado].llamar(this.consulta.id), true)
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
.datos {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--e-sm) var(--e-lg);
  margin: var(--e-md) 0 0;
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

.acciones {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--e-xs);
  margin-top: var(--e-md);
}

/* Empujada al extremo opuesto: para llegar hay que cruzar el grupo entero, que
   es justo la distancia que una acción irreversible necesita. */
.aparte { margin-left: auto; }

@media (max-width: 600px) {
  .aparte { margin-left: 0; width: 100%; }
}

.hilandero { border-top-color: currentColor; }

@media (max-width: 600px) {
  .datos { grid-template-columns: 1fr; }
}
</style>
