<template>
  <span class="envoltorio">
    <template v-if="!preguntando">
      <button
        :class="peligro && 'btn-danger'"
        :disabled="disabled || cargando"
        @click="preguntando = true"
      >
        {{ cargando ? 'Procesando…' : etiqueta }}
      </button>
    </template>

    <template v-else>
      <span class="pregunta">{{ pregunta }}</span>
      <button :class="peligro && 'btn-danger'" @click="confirmar">Sí</button>
      <button class="btn-neutro" @click="preguntando = false">No</button>
    </template>
  </span>
</template>

<script>
/**
 * Acción destructiva con confirmación en la propia fila.
 *
 * No usa `confirm()`: un diálogo nativo bloquea el hilo de la página, no se
 * puede estilar y no deja mostrar en el mismo lugar qué se está por hacer.
 */
export default {
  name: 'BotonConConfirmacion',
  props: {
    etiqueta: { type: String, required: true },
    pregunta: { type: String, default: '¿Confirmás?' },
    disabled: { type: Boolean, default: false },
    cargando: { type: Boolean, default: false },
    peligro: { type: Boolean, default: false }
  },
  emits: ['confirmado'],
  data () {
    return { preguntando: false }
  },
  methods: {
    confirmar () {
      this.preguntando = false
      this.$emit('confirmado')
    }
  }
}
</script>

<style scoped>
.envoltorio {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
.pregunta { font-size: 13px; color: #666 }
.btn-neutro { background: #e0e0e0; color: #333 }
.btn-neutro:hover { background: #cfcfcf }
button:disabled {
  background: #c5cdd6;
  cursor: not-allowed;
}
</style>
