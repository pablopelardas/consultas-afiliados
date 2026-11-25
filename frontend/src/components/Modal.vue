<template>
  <Teleport to="body">
    <div class="fondo" @mousedown.self="$emit('cerrar')">
      <div
        ref="panel"
        class="panel"
        role="dialog"
        aria-modal="true"
        :aria-label="titulo"
        tabindex="-1"
        @keydown.esc="$emit('cerrar')"
      >
        <header class="cabecera">
          <h2>{{ titulo }}</h2>
          <button class="cerrar" type="button" aria-label="Cerrar" @click="$emit('cerrar')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"
                 stroke-linecap="round" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" />
            </svg>
          </button>
        </header>

        <div class="cuerpo">
          <slot />
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script>
/**
 * Diálogo modal. El contenido lo pone quien lo usa; acá vive solo el
 * comportamiento que un modal tiene que tener sí o sí y es fácil olvidarse:
 * cerrar con Escape, cerrar al hacer clic afuera y no dejar que el fondo
 * siga scrolleando.
 */
export default {
  name: 'Modal',
  props: {
    titulo: { type: String, required: true }
  },
  emits: ['cerrar'],
  mounted () {
    // Sin esto, el teclado sigue en la página de atrás y Escape no llega.
    this.$refs.panel?.focus()
    this.scrollPrevio = document.body.style.overflow
    document.body.style.overflow = 'hidden'
  },
  unmounted () {
    document.body.style.overflow = this.scrollPrevio ?? ''
  }
}
</script>

<style scoped>
.fondo {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: var(--e-xl) var(--e-md);
  overflow-y: auto;
  background: rgba(15, 20, 56, .45);
  animation: aparecer .12s ease;
}

.panel {
  width: 100%;
  max-width: 560px;
  background: var(--superficie);
  border-radius: var(--radio-panel);
  box-shadow: var(--sombra-elevada);
  outline: none;
  animation: subir .16s ease;
}

.cabecera {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--e-md);
  padding: var(--e-md) var(--e-lg);
  border-bottom: 1px solid var(--borde-sutil);
}

.cabecera h2 { font-size: var(--t-lg); }

.cerrar {
  width: 32px;
  height: 32px;
  padding: 0;
  flex-shrink: 0;
  background: transparent;
  color: var(--texto-apagado);
}
.cerrar:hover:not(:disabled) { background: var(--superficie-tenue); color: var(--texto); }
.cerrar svg { width: 18px; height: 18px; }

.cuerpo { padding: var(--e-lg); }

@keyframes aparecer { from { opacity: 0 } }
@keyframes subir { from { opacity: 0; transform: translateY(8px) } }

@media (prefers-reduced-motion: reduce) {
  .fondo, .panel { animation: none; }
}
</style>
