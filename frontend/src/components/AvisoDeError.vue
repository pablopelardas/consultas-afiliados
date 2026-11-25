<template>
  <div v-if="error" :class="['aviso', error.esDeNegocio ? 'aviso-regla' : 'aviso-falla']" role="alert">
    <span class="icono">{{ error.esDeNegocio ? '!' : '×' }}</span>
    <div class="cuerpo">
      <p class="mensaje">{{ error.message }}</p>
      <button v-if="reintentable" class="reintentar" @click="$emit('reintentar')">
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
  gap: 12px;
  align-items: flex-start;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 18px;
  font-size: 14px;
  border-left: 4px solid;
}
.aviso-regla {
  background: #fff8e1;
  border-color: #f9a825;
  color: #7a5900;
}
.aviso-falla {
  background: #fdecea;
  border-color: #d32f2f;
  color: #8c1d16;
}
.icono {
  font-weight: 700;
  line-height: 1.5;
  flex-shrink: 0;
}
.cuerpo { flex: 1 }
.mensaje { margin: 0; line-height: 1.5 }
.reintentar {
  margin-top: 10px;
  background: transparent;
  border: 1px solid currentColor;
  color: inherit;
  font-weight: 600;
}
.reintentar:hover { background: rgba(0, 0, 0, .06) }
</style>
