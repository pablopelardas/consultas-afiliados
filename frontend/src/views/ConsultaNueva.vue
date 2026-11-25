<template>
  <div class="card" style="max-width: 560px">
    <h2>Nueva consulta</h2>

    <AvisoDeError :error="error" />

    <form novalidate @submit.prevent="guardar">
      <div class="form-group">
        <label>Afiliado</label>
        <select v-model="form.afiliadoId" :disabled="enviando">
          <option value="">Seleccioná un afiliado</option>
          <option v-for="a in afiliados" :key="a.id" :value="a.id">
            {{ a.nombreCompleto }} — DNI {{ a.dni }}
          </option>
        </select>
        <p v-if="errorDe('afiliadoId')" class="error-campo">{{ errorDe('afiliadoId') }}</p>
      </div>

      <div class="form-group">
        <label>Profesional</label>
        <select v-model="form.profesionalId" :disabled="enviando">
          <option value="">Seleccioná un profesional</option>
          <option v-for="p in profesionales" :key="p.id" :value="p.id">
            {{ p.nombreCompleto }} — {{ p.especialidad }}
          </option>
        </select>
        <p v-if="errorDe('profesionalId')" class="error-campo">{{ errorDe('profesionalId') }}</p>
      </div>

      <!-- El cupo se muestra antes de enviar: la política deja de ser un
           rechazo que aparece al confirmar y pasa a ser información previa. -->
      <CupoNutricion
        v-if="correspondeMostrarCupo"
        :cupo="cupo"
        :cargando="cargandoCupo"
      />

      <div class="form-group">
        <label>Modalidad</label>
        <select v-model="form.modalidad" :disabled="enviando">
          <option value="Presencial">Presencial</option>
          <option value="Virtual">Virtual</option>
        </select>
      </div>

      <div class="form-group">
        <label>Fecha y hora</label>
        <input type="datetime-local" v-model="form.fechaHora" :min="ahoraLocal" :disabled="enviando" />
        <p v-if="fechaEnElPasado" class="error-campo">La consulta tiene que ser a futuro.</p>
        <p v-else-if="errorDe('fechaHora')" class="error-campo">{{ errorDe('fechaHora') }}</p>
      </div>

      <div class="form-group">
        <label>Motivo</label>
        <input
          type="text"
          v-model="form.motivo"
          maxlength="500"
          placeholder="Motivo de la consulta"
          :disabled="enviando"
        />
        <p v-if="errorDe('motivo')" class="error-campo">{{ errorDe('motivo') }}</p>
      </div>

      <button type="submit" :disabled="!puedeEnviar">
        {{ enviando ? 'Confirmando…' : 'Confirmar consulta' }}
      </button>
    </form>
  </div>
</template>

<script>
import { consultasApi, afiliadosApi, profesionalesApi } from '../services/api'
import AvisoDeError from '../components/AvisoDeError.vue'
import CupoNutricion from '../components/CupoNutricion.vue'

export default {
  name: 'ConsultaNueva',
  components: { AvisoDeError, CupoNutricion },
  data () {
    return {
      form: {
        afiliadoId: '',
        profesionalId: '',
        modalidad: 'Presencial',
        fechaHora: '',
        motivo: ''
      },
      afiliados: [],
      profesionales: [],
      cupo: null,
      cargandoCupo: false,
      enviando: false,
      error: null
    }
  },
  computed: {
    // El atributo `min` del input necesita el formato local sin zona, que es el
    // mismo que entrega el control: "2026-12-01T10:00".
    ahoraLocal () {
      const ahora = new Date()
      const desfase = ahora.getTimezoneOffset() * 60000
      return new Date(ahora.getTime() - desfase).toISOString().slice(0, 16)
    },

    fechaEnElPasado () {
      return Boolean(this.form.fechaHora) && new Date(this.form.fechaHora) <= new Date()
    },

    completo () {
      const { afiliadoId, profesionalId, fechaHora, motivo } = this.form
      return Boolean(afiliadoId && profesionalId && fechaHora && motivo.trim())
    },

    puedeEnviar () {
      return this.completo && !this.fechaEnElPasado && !this.enviando
    },

    profesionalElegido () {
      return this.profesionales.find(p => p.id === Number(this.form.profesionalId)) ?? null
    },

    // `tieneCupo` lo resuelve el backend contra el código de la especialidad. El
    // cliente no compara nombres: renombrar "Nutrición" no apaga esta sección.
    correspondeMostrarCupo () {
      return Boolean(this.form.afiliadoId) && Boolean(this.profesionalElegido?.tieneCupo)
    }
  },
  watch: {
    correspondeMostrarCupo (corresponde) {
      if (corresponde) this.cargarCupo()
    },
    'form.afiliadoId' () {
      this.cupo = null
      if (this.correspondeMostrarCupo) this.cargarCupo()
    }
  },
  async mounted () {
    try {
      const [afiliados, profesionales] = await Promise.all([
        afiliadosApi.getAll(),
        profesionalesApi.getAll()
      ])
      this.afiliados = afiliados
      this.profesionales = profesionales
    } catch (error) {
      this.error = error
    }
  },
  methods: {
    errorDe (campo) {
      return this.error?.campos?.[campo] ?? null
    },

    // El input datetime-local entrega "2026-12-01T10:00", sin zona horaria.
    // Enviado así, el backend no puede saber a qué instante se refiere. Se
    // convierte a ISO-8601 con sufijo Z: el usuario elige una hora local y el
    // sistema persiste siempre el instante en UTC.
    aIsoUtc (fechaHoraLocal) {
      return new Date(fechaHoraLocal).toISOString()
    },

    async cargarCupo () {
      this.cargandoCupo = true
      try {
        this.cupo = await afiliadosApi.getCupoNutricion(Number(this.form.afiliadoId))
      } catch {
        // El cupo es información de apoyo: si no se puede leer, el formulario
        // sigue usable y el backend vuelve a validarlo al confirmar.
        this.cupo = null
      } finally {
        this.cargandoCupo = false
      }
    },

    async guardar () {
      this.enviando = true
      this.error = null
      try {
        await consultasApi.create({
          afiliadoId: Number(this.form.afiliadoId),
          profesionalId: Number(this.form.profesionalId),
          modalidad: this.form.modalidad,
          fechaHora: this.aIsoUtc(this.form.fechaHora),
          motivo: this.form.motivo
        })
        this.$router.push('/consultas')
      } catch (error) {
        this.error = error
        // El cupo pudo haber cambiado entre que se cargó y se confirmó: si el
        // rechazo fue por cupo, el panel tiene que reflejar el estado real.
        if (this.correspondeMostrarCupo) this.cargarCupo()
      } finally {
        this.enviando = false
      }
    }
  }
}
</script>

<style scoped>
.error-campo {
  margin: 6px 0 0;
  font-size: 12px;
  color: #c62828;
}
button:disabled {
  background: #c5cdd6;
  cursor: not-allowed;
}
</style>
