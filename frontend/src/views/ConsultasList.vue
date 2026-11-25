<template>
  <div>
    <AvisoDeError :error="errorDeCarga" @reintentar="cargar" />
    <AvisoDeError :error="errorDeAccion" />

    <div class="encabezado-lista">
      <p class="conteo">
        <template v-if="!cargando">{{ consultas.length }} {{ consultas.length === 1 ? 'consulta' : 'consultas' }}</template>
      </p>
      <router-link to="/consultas/nuevo" class="boton">Nueva consulta</router-link>
    </div>

    <div v-if="cargando" class="cargando">
      <span class="hilandero" />
      Cargando consultas…
    </div>

    <div v-else-if="consultas.length" class="tabla-envoltorio">
      <table>
        <thead>
          <tr>
            <th class="col-id">#</th>
            <th>Afiliado</th>
            <th>Profesional</th>
            <th>Fecha y hora</th>
            <th>Modalidad</th>
            <th>Estado</th>
            <th>Motivo</th>
            <th class="col-acciones"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="consulta in consultas" :key="consulta.id">
            <td class="col-id">{{ consulta.id }}</td>
            <td class="principal">{{ consulta.afiliado?.nombreCompleto }}</td>
            <td>
              <span class="principal">{{ consulta.profesional?.nombreCompleto }}</span><br>
              <span class="secundario">{{ consulta.profesional?.especialidad }}</span>
            </td>
            <td>{{ formatFecha(consulta.fechaHora) }}</td>
            <td>{{ consulta.modalidad }}</td>
            <td>
              <span :class="['badge', `badge-${consulta.estado?.toLowerCase()}`]">{{ consulta.estado }}</span>
            </td>
            <td class="secundario">{{ consulta.motivo }}</td>
            <td class="col-acciones">
              <div class="acciones">
                <router-link :to="`/consultas/${consulta.id}`" class="boton btn-secundario btn-chico">Ver</router-link>
                <BotonConConfirmacion
                  v-if="puedeCancelar(consulta)"
                  etiqueta="Cancelar"
                  pregunta="¿Cancelar?"
                  peligro
                  chico
                  :cargando="cancelando === consulta.id"
                  :disabled="cancelando !== null"
                  @confirmado="cancelar(consulta)"
                />
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-else-if="!errorDeCarga" class="vacio">No hay consultas registradas.</p>
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
.encabezado-lista {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--e-md);
  margin-bottom: var(--e-md);
}

.conteo {
  margin: 0;
  color: var(--texto-apagado);
  font-size: var(--t-sm);
}

.acciones {
  display: flex;
  align-items: center;
  gap: var(--e-xs);
}

a.boton { text-decoration: none; }
</style>
