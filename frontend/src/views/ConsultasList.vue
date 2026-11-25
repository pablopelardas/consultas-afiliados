<template>
  <div>
    <h2>Consultas</h2>

    <AvisoDeError :error="errorDeCarga" @reintentar="cargar" />
    <AvisoDeError :error="errorDeAccion" />

    <p v-if="cargando">Cargando consultas…</p>

    <table v-else-if="consultas.length">
      <thead>
        <tr>
          <th>#</th>
          <th>Afiliado</th>
          <th>Profesional</th>
          <th>Especialidad</th>
          <th>Fecha y hora</th>
          <th>Modalidad</th>
          <th>Estado</th>
          <th>Motivo</th>
          <th>Acciones</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="consulta in consultas" :key="consulta.id">
          <td>{{ consulta.id }}</td>
          <td>{{ consulta.afiliado?.nombreCompleto }}</td>
          <td>{{ consulta.profesional?.nombreCompleto }}</td>
          <td>{{ consulta.profesional?.especialidad }}</td>
          <td>{{ formatFecha(consulta.fechaHora) }}</td>
          <td>{{ consulta.modalidad }}</td>
          <td>
            <span :class="['badge', `badge-${consulta.estado?.toLowerCase()}`]">{{ consulta.estado }}</span>
          </td>
          <td>{{ consulta.motivo }}</td>
          <td class="acciones">
            <router-link :to="`/consultas/${consulta.id}`">Ver</router-link>
            <BotonConConfirmacion
              v-if="puedeCancelar(consulta)"
              etiqueta="Cancelar"
              pregunta="¿Cancelar?"
              peligro
              :cargando="cancelando === consulta.id"
              :disabled="cancelando !== null"
              @confirmado="cancelar(consulta)"
            />
          </td>
        </tr>
      </tbody>
    </table>

    <p v-else-if="!errorDeCarga">No hay consultas registradas.</p>
  </div>
</template>

<script>
import { consultasApi } from '../services/api'
import { instante } from '../services/fechas'
import AvisoDeError from '../components/AvisoDeError.vue'
import BotonConConfirmacion from '../components/BotonConConfirmacion.vue'

export default {
  name: 'ConsultasList',
  components: { AvisoDeError, BotonConConfirmacion },
  data () {
    return {
      consultas: [],
      cargando: false,
      // Separados porque conviven: un rechazo al cancelar no debe borrar la
      // tabla, y un fallo de carga no se arregla reintentando la cancelación.
      errorDeCarga: null,
      errorDeAccion: null,
      cancelando: null
    }
  },
  mounted () {
    this.cargar()
  },
  methods: {
    // La fecha del turno es un instante: se muestra en la hora local de quien
    // mira. El cupo usa el otro criterio (ver services/fechas.js).
    formatFecha: instante,

    // La tabla de transiciones vive en el backend y llega en la respuesta. Una
    // copia acá se desincroniza del dominio sin que ningún test lo note.
    puedeCancelar (consulta) {
      return consulta.transicionesPermitidas?.includes('Cancelado')
    },

    async cargar () {
      this.cargando = true
      this.errorDeCarga = null
      try {
        this.consultas = await consultasApi.getAll()
      } catch (error) {
        this.errorDeCarga = error
      } finally {
        this.cargando = false
      }
    },

    async cancelar (consulta) {
      this.cancelando = consulta.id
      this.errorDeAccion = null
      try {
        // La respuesta trae la consulta ya actualizada, con su nuevo estado y
        // sus nuevas transiciones: reemplazarla evita volver a pedir la lista.
        const actualizada = await consultasApi.cancelar(consulta.id)
        const i = this.consultas.findIndex(c => c.id === consulta.id)
        if (i !== -1) this.consultas.splice(i, 1, actualizada)
      } catch (error) {
        this.errorDeAccion = error
      } finally {
        this.cancelando = null
      }
    }
  }
}
</script>

<style scoped>
.badge {
  padding: 3px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
}
.badge-pendiente    { background: #fff3cd; color: #856404; }
.badge-confirmado   { background: #d4edda; color: #155724; }
.badge-cancelado    { background: #f8d7da; color: #721c24; }
.badge-atendido     { background: #d1ecf1; color: #0c5460; }
.badge-inasistencia { background: #e2e3e5; color: #383d41; }
.acciones {
  display: flex;
  align-items: center;
  gap: 12px;
}
</style>
