<template>
  <span class="envoltorio">
    <button
      v-if="!preguntando"
      :class="[...clasesBase, 'btn-secundario', peligro && 'texto-peligro']"
      :disabled="disabled || cargando"
      @click="preguntando = true"
    >
      <span v-if="cargando" class="hilandero" />
      {{ cargando ? 'Procesando…' : etiqueta }}
    </button>

    <template v-else>
      <span class="pregunta">{{ pregunta }}</span>
      <button :class="clases" @click="confirmar">Sí</button>
      <button :class="[...clasesBase, 'btn-secundario']" @click="preguntando = false">No</button>
    </template>
  </span>
</template>

<script>
/**
 * Acción destructiva con confirmación en la propia fila.
 *
 * No usa `confirm()`: un diálogo nativo bloquea el hilo de la página, no se
 * puede estilar y no deja mostrar en el mismo lugar qué se está por hacer.
 *
 * El rojo aparece recién al confirmar. Una columna de ocho botones rojos le da
 * el mayor peso visual de la tabla a la acción que menos conviene apurar.
 */
export default {
  name: 'BotonConConfirmacion',
  props: {
    etiqueta: { type: String, required: true },
    pregunta: { type: String, default: '¿Confirmás?' },
    disabled: { type: Boolean, default: false },
    cargando: { type: Boolean, default: false },
    peligro: { type: Boolean, default: false },
    chico: { type: Boolean, default: false }
  },
  emits: ['confirmado'],
  data () {
    return { preguntando: false }
  },
  computed: {
    clasesBase () {
      return this.chico ? ['btn-chico'] : []
    },
    clases () {
      return [...this.clasesBase, this.peligro && 'btn-danger']
    }
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
  gap: var(--e-xs);
  white-space: nowrap;
}

.pregunta {
  color: var(--texto-sutil);
  font-size: var(--t-xs);
  font-weight: 500;
}

.hilandero { border-top-color: currentColor; }

.texto-peligro { color: var(--texto-peligro); }
.texto-peligro:hover:not(:disabled) {
  background: var(--peligro-tenue);
  border-color: #f7a5a5;
}
</style>
