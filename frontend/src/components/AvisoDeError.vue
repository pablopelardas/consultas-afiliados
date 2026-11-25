<template>
  <div v-if="error" :class="['aviso', error.esDeNegocio ? 'aviso-regla' : 'aviso-falla']" role="alert">
    <svg class="icono" viewBox="0 0 24 24" fill="none" stroke="currentColor"
         stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path v-if="error.esDeNegocio" d="M12 8v5m0 3h.01" />
      <path v-else d="M15 9l-6 6m0-6l6 6" />
    </svg>

    <div class="cuerpo">
      <p class="mensaje">{{ error.message }}</p>
      <button v-if="reintentable" class="btn-chico btn-secundario" @click="$emit('reintentar')">
        Reintentar
      </button>
    </div>
  </div>
</template>

<script>
import { TipoDeError } from '../services/http'

/**
 * Muestra un error ya clasificado. No decide nada: el tipo lo trae el error.
 *
 * La distinción visual entre regla y falla no es decorativa. Un cupo agotado
 * mostrado en rojo manda al usuario a llamar a soporte; mostrado como aviso, lo
 * manda a leer el mensaje, que es donde está la respuesta.
 */
export default {
  name: 'AvisoDeError',
  props: {
    error: { type: Object, default: null }
  },
  emits: ['reintentar'],
  computed: {
    // Reintentar solo tiene sentido si el resultado puede cambiar sin que el
    // usuario cambie nada. Un 409 va a volver a fallar igual.
    reintentable () {
      return this.error?.tipo === TipoDeError.Red || this.error?.tipo === TipoDeError.Servidor
    }
  }
}
</script>

<style scoped>
.aviso {
  display: flex;
  gap: var(--e-sm);
  align-items: flex-start;
  margin-bottom: var(--e-md);
  padding: var(--e-sm) var(--e-md);
  border: 1px solid;
  border-radius: var(--radio-control);
  font-size: var(--t-sm);
}

.aviso-regla {
  background: var(--alerta-tenue);
  border-color: #fad08a;
  color: var(--texto-alerta);
}

.aviso-falla {
  background: var(--peligro-tenue);
  border-color: #f7a5a5;
  color: var(--texto-peligro);
}

.icono {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  margin-top: 1px;
}

.cuerpo { flex: 1; min-width: 0; }
.mensaje { margin: 0; line-height: 1.5; }

button {
  margin-top: var(--e-sm);
  background: transparent;
  border-color: currentColor;
  color: inherit;
}
button:hover { background: rgba(255, 255, 255, .55); }
</style>
