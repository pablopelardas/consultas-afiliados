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
        <BotonConConfirmacion
          v-for="op in operaciones"
          :key="op.estado"
          :etiqueta="op.etiqueta"
          :pregunta="op.pregunta"
          :peligro="op.peligro"
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
const OPERACIONES = {
  Confirmado: {
    estado: 'Confirmado',
    etiqueta: 'Confirmar',
    pregunta: '¿Confirmar?',
    llamar: (id) => consultasApi.actualizarEstado(id, { estado: 'Confirmado' })
  },
  Atendido: {
    estado: 'Atendido',
    etiqueta: 'Registrar atención',
    pregunta: '¿Registrar la atención?',
    llamar: (id) => consultasApi.registrarAtencion(id)
  },
  Inasistencia: {
    estado: 'Inasistencia',
    etiqueta: 'Marcar inasistencia',
    pregunta: '¿Marcar inasistencia?',
    llamar: (id) => consultasApi.marcarInasistencia(id)
  },
  Cancelado: {
    estado: 'Cancelado',
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

    // Una acción por transición posible, nombrada por lo que ocurre y no por
    // el campo que se escribe. El backend decide cuáles aparecen.
    operaciones () {
      return this.transiciones.map(estado => OPERACIONES[estado])
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

.hilandero { border-top-color: currentColor; }

@media (max-width: 600px) {
  .datos { grid-template-columns: 1fr; }
}
</style>
